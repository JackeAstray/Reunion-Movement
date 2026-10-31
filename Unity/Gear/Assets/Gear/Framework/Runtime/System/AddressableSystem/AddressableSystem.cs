using Gear.Common;
using Gear.Framework.Runtime.Base;
using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Networking;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Gear.Framework.Runtime.Resources
{
    /// <summary>
    /// Addressables 远程更新检查结果
    /// </summary>
    public struct AddressableUpdateResult
    {
        /// <summary>是否存在可更新 Catalog</summary>
        public bool hasUpdate;
        /// <summary>可更新的 Catalog 列表（hasUpdate 为 true 时非空）</summary>
        public List<string> updatedCatalogs;
        /// <summary>
        /// 检查过程是否失败（网络/服务端错误、锚点校验失败等）。
        /// 失败时 hasUpdate 也为 false，但语义是"未知"而不是"已是最新"——
        /// 热更流程必须据此报 Failed，否则会把检查失败静默显示成"已是最新版本"。
        /// </summary>
        public bool checkFailed;
    }

    /// <summary>
    /// Addressables 统一封装 —— 受管资源异步加载/实例化/释放 + 远程更新。
    /// 全部基于 UniTask，支持进度与取消；失败可降级到 ResourcesSystem。
    /// 生命周期由 Addressables 自带引用计数管理，调用方须成对调用 Load / Release。
    /// 设计文档：仓库 Docs/Addressables/Addressables集成设计方案.md §5
    /// </summary>
    [ModuleOrder(20, typeof(Gear.Framework.Runtime.Resources.ResourcesSystem))]
    public partial class AddressableSystem : ICustomSystem, ISystemDisposable, ICustomSystemCancellableInit
    {
        #region 单例与初始化
        private static readonly Lazy<AddressableSystem> instance = new(() => new AddressableSystem());
        public static AddressableSystem Instance => instance.Value;

        public bool isInited { get; private set; }

        private double initProgress = 0;
        public double InitProgress { get { return initProgress; } }
        #endregion

        #region 数据
        /// <summary>当前运行模式（从 Config 读取）</summary>
        public AddressablesMode Mode => Config.AddressablesMode;

        /// <summary>CheckUpdateAsync 找到的待更新 Catalog 列表</summary>
        private readonly List<string> pendingUpdateCatalogs = new List<string>();

        /// <summary>
        /// Catalog 锚点校验失败标记：内嵌 version.json 的 catalogHash 与远程 Catalog 不一致
        /// （疑似中间人篡改）。置位后拒绝应用远程更新，仅使用内嵌/本地内容。
        /// </summary>
        private bool catalogAnchorFailed;

        // 调试统计（Interlocked 保证线程安全，Addressables 回调可能跨线程）
        private int loadCount;      // 累计加载/实例化次数
        private int releaseCount;   // 累计释放次数
        private int activeCount;    // 当前未释放数（load - release）

        // 实例句柄表：InstantiateAsync 成功后登记，ReleaseInstance 优先按句柄释放。
        // 目的：实例已被 Destroy 时 Unity 的 == null 短路会让 ReleaseInstance 无法查找句柄，
        // 通过本表仍可正确释放 Addressables 引用计数（对象池销毁/场景卸载免底路径）。
        private readonly Dictionary<GameObject, AsyncOperationHandle<GameObject>> instanceHandles
            = new Dictionary<GameObject, AsyncOperationHandle<GameObject>>();
        private readonly object instanceHandleLock = new object();
        #endregion

        /// <summary>
        /// 初始化 Addressables。Off 模式直接跳过；Remote 模式附带 Catalog 更新检查（失败不阻断启动）。
        /// 初始化失败不抛出 —— 业务层可通过 LoadWithFallbackAsync 降级到 ResourcesSystem。
        /// </summary>
        public UniTask Init() => InitCore(default);

        async UniTask ICustomSystemCancellableInit.Init(CancellationToken ct) => await InitCore(ct);

        private async UniTask InitCore(CancellationToken ct)
        {
            initProgress = 0;
            isInited = false;
            int myGeneration = ++initGeneration;

            // 失败冷却：Init 失败后，每个 Load/Instantiate 入口都会再次触发 Init，
            // 若 Addressables 服务不可用，会形成"每次加载都全量重试初始化"的风暴。
            // 冷却期内直接返回未就绪，让调用方走 Resources 降级。
            if (lastInitFailureTime >= 0f
                && Time.realtimeSinceStartup - lastInitFailureTime < InitRetryCooldownSeconds)
            {
                Log.Warning("AddressableSystem 初始化失败冷却中（{0:F1}s 后可重试），本次跳过", 
                    InitRetryCooldownSeconds - (Time.realtimeSinceStartup - lastInitFailureTime));
                return;
            }

            if (Mode == AddressablesMode.Off)
            {
                initProgress = 100;
                // Off 模式即"已就绪"（跳过 Addressables）：置 isInited=true 保持"Init 成功即就绪"约定，
                // 否则后续按 isInited 判断可用性的调用方会误判为未初始化而触发重复 Init 或错误降级
                isInited = true;
                Log.Debug("AddressableSystem 已关闭（AddressablesMode.Off），跳过初始化");
                return;
            }

            try
            {
                // Remote 模式：在 InitializeAsync 之前配置远程 URL 重写
                // （GameConfig.remoteBundleUrl/remoteCatalogUrl 覆盖构建时烘焙地址；远程 catalog 在初始化时加载，故须提前）
                if (Mode == AddressablesMode.Remote)
                {
                    ConfigureRemoteUrlRewrite();
                }

                var handle = Addressables.InitializeAsync();
                await handle.ToUniTask(cancellationToken: ct);
                if (myGeneration != initGeneration) return; // 初始化期间已被 Clear/重启
                initProgress = 50;

                // 远程模式：检查 Catalog 更新（仅告警，不阻断启动）
                if (Mode == AddressablesMode.Remote)
                {
                    try
                    {
                        // 首次下载的远程 Catalog 与内嵌 version.json 锚点核对
                        // （无内嵌锚点或本地已有缓存 Catalog 时跳过，详见 VerifyCatalogAnchor）
                        await VerifyCatalogAnchorAsync();
                        ct.ThrowIfCancellationRequested();
                        if (catalogAnchorFailed)
                        {
                            Log.Error("AddressableSystem 远程 Catalog 与内嵌锚点不一致，已拒绝远程更新（仅使用内嵌/本地内容）");
                        }
                        var result = await CheckUpdateAsync(ct);
                        ct.ThrowIfCancellationRequested();
                        Log.Debug("AddressableSystem 远程更新检查: {0}", result.hasUpdate ? "有更新待下载" : "无更新");
                    }
                    catch (OperationCanceledException) when (ct.IsCancellationRequested)
                    {
                        throw; // 引擎取消/超时：不要被"远程检查失败不阻断启动"吞掉
                    }
                    catch (Exception ex)
                    {
                        Log.Warning("AddressableSystem 远程更新检查失败（继续启动）: {0}", ex.Message);
                    }
                }

                if (myGeneration != initGeneration) return; // 远程检查期间已被 Clear/重启
                initProgress = 100;
                isInited = true;
                lastInitFailureTime = -1f;
                Log.Debug("AddressableSystem 初始化完成 (Mode={0})", Mode);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                // 引擎取消/超时：交给 LaunchAsync 统一处理，不写失败冷却/不置 isInited
                throw;
            }
            catch (Exception ex)
            {
                Log.Error("AddressableSystem 初始化失败（业务层将降级 Resources）: {0}", ex.Message);
                // 已被 Clear/重启的过期初始化不再回写 isInited/冷却时间
                if (myGeneration != initGeneration) return;
                isInited = false;
                lastInitFailureTime = Time.realtimeSinceStartup;
            }
        }

        /// <summary>上次初始化失败的时间（realtimeSinceStartup；&lt;0 表示从未失败）</summary>
        private float lastInitFailureTime = -1f;

        /// <summary>初始化代次：Clear/重启递增，在途 Init 的 await 续体恢复后据此放弃，避免僵尸初始化</summary>
        private int initGeneration;
        /// <summary>初始化失败后的重试冷却（秒），避免每个加载入口都触发一次全量重试</summary>
        private const float InitRetryCooldownSeconds = 5f;

        /// <summary>是否仅允许 https 远程地址（拒绝明文 http，防止 CDN 热更被中间人替换）</summary>
        private static bool s_plaintextWarned;

        /// <summary>
        /// 配置远程 URL 重写：用 GameConfig 的 remoteBundleUrl / remoteCatalogUrl 覆盖构建时
        /// 烘焙的远程地址（Profile Remote.LoadPath），使同一构建产物可部署到任意 CDN。
        /// 必须在 Addressables.InitializeAsync 之前调用（远程 catalog 在初始化时加载）。
        /// 规则：仅重写 https 远程地址（明文 http 一律拒绝并告警）；catalog_* 文件优先用
        /// remoteCatalogUrl，其余用 remoteBundleUrl；目标 URL 视为“最终目录 URL”（含平台目录），
        /// 如 https://cdn.example.com/gear/0.2.0/WebGL。
        /// </summary>
        private void ConfigureRemoteUrlRewrite()
        {
            string bundleRoot = Config.RemoteBundleUrl;
            string catalogRoot = Config.RemoteCatalogUrl;

            // 明文 http 根地址拒绝参与重写：热更内容可被中间人替换，属高危配置
            if (IsPlaintextHttp(bundleRoot))
            {
                Log.Error("AddressableSystem 拒绝使用明文 HTTP Bundle 根地址（请改用 https）：{0}", bundleRoot);
                bundleRoot = null;
            }
            if (IsPlaintextHttp(catalogRoot))
            {
                Log.Error("AddressableSystem 拒绝使用明文 HTTP Catalog 根地址（请改用 https）：{0}", catalogRoot);
                catalogRoot = null;
            }

            if (string.IsNullOrEmpty(bundleRoot) && string.IsNullOrEmpty(catalogRoot))
            {
                // 未配置（或全部被拒）：使用构建时烘焙地址，清除可能残留的重写
                Addressables.ResourceManager.InternalIdTransformFunc = null;
                return;
            }

            Addressables.ResourceManager.InternalIdTransformFunc = location =>
            {
                string id = location.InternalId;
                if (string.IsNullOrEmpty(id)) return id;

                // 仅重写 https 远程地址（本地 file:// / StreamingAssets 路径不受影响）
                bool isHttps = id.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
                if (!isHttps)
                {
                    // 明文 http 远程地址：拒绝重写并告警（首次触发一次），
                    // 残留的烘焙 http 地址仅在开发环境（DevLocal）出现
                    if (id.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && !s_plaintextWarned)
                    {
                        s_plaintextWarned = true;
                        Log.Error("AddressableSystem 检测到明文 http 远程资源地址（{0}），生产环境必须使用 https", id);
                    }
                    return id;
                }

                string root = IsRemoteCatalogId(id) && !string.IsNullOrEmpty(catalogRoot) ? catalogRoot : bundleRoot;
                if (string.IsNullOrEmpty(root)) return id;

                return ReplaceRemoteRoot(id, root);
            };
            Log.Debug("AddressableSystem 已配置远程 URL 重写: bundle={0}, catalog={1}", bundleRoot, catalogRoot);
        }

        private static bool IsPlaintextHttp(string url)
        {
            return !string.IsNullOrEmpty(url)
                && url.StartsWith("http://", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>判断 InternalId 是否为远程 Catalog 文件（catalog_*.bin/.hash/.json）</summary>
        private static bool IsRemoteCatalogId(string id)
        {
            int slash = id.LastIndexOf('/');
            string name = slash >= 0 ? id.Substring(slash + 1) : id;
            return name.StartsWith("catalog_", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>把 InternalId 的远程目录部分（最后一个 / 之前）替换为目标根 URL（含平台目录）</summary>
        private static string ReplaceRemoteRoot(string internalId, string newRoot)
        {
            int lastSlash = internalId.LastIndexOf('/');
            if (lastSlash <= 0) return internalId;
            return newRoot.TrimEnd('/') + "/" + internalId.Substring(lastSlash + 1);
        }

        public void Clear()
        {
            Log.Debug("AddressableSystem 清除数据");
            // 使在途 Init 的续体失效：它是异步的，不会随 Clear 一起停下
            initGeneration++;
            pendingUpdateCatalogs.Clear();

            // 释放所有未配对释放的实例句柄，避免直接清零统计掩盖泄漏
            lock (instanceHandleLock)
            {
                foreach (var kv in instanceHandles)
                {
                    if (kv.Value.IsValid())
                    {
                        try { Addressables.Release(kv.Value); } catch { }
                        releaseCount++;
                    }
                }
                instanceHandles.Clear();
            }

            // 加载/释放不平衡告警（正常应为 0；>0 说明存在泄漏调用方）
            if (loadCount > releaseCount)
            {
                Log.Warning("AddressableSystem Clear: 存在 {0} 个未释放的资源（load={1}, release={2}）",
                    loadCount - releaseCount, loadCount, releaseCount);
            }

            loadCount = 0;
            releaseCount = 0;
            activeCount = 0;
            // 失败冷却与锚点失败标记都是"本次引擎会话"的状态：
            // Clear 后 Time.realtimeSinceStartup 可能归零，若不清空冷却时间，
            // 下一次会话会在头 5 秒内被误判为"仍在冷却"而拒绝初始化；
            // 锚点失败标记同理，否则重启后永久拒绝远程更新。
            lastInitFailureTime = -1f;
            catalogAnchorFailed = false;
            isInited = false;
        }

        #region 加载
        /// <summary>
        /// 异步加载受管资源。返回的资源通过 <see cref="ReleaseAsset{T}"/> 成对释放。
        /// </summary>
        /// <typeparam name="T">资源类型</typeparam>
        /// <param name="key">Addressables key（路径常量 / Label / AssetReference）</param>
        /// <param name="progress">进度回调（0~1）</param>
        /// <param name="ct">取消令牌（取消时自动释放句柄，防泄漏）</param>
        /// <returns>资源对象，失败返回 null</returns>
        public async UniTask<T> LoadAssetAsync<T>(object key, IProgress<float> progress = null, CancellationToken ct = default) where T : Object
        {
            if (Mode == AddressablesMode.Off)
            {
                Log.Warning("AddressableSystem 处于 Off 模式，无法加载: {0}", key);
                return null;
            }
            if (!isInited)
            {
                Log.Warning("AddressableSystem 未初始化，自动执行 Init()");
                await Init();
                if (!isInited) return null;
            }

            AsyncOperationHandle<T> handle = default;
            try
            {
                handle = Addressables.LoadAssetAsync<T>(key);
                var asset = await handle.ToUniTask(progress, cancellationToken: ct, autoReleaseWhenCanceled: true);
                if (handle.Status != AsyncOperationStatus.Succeeded || asset == null)
                {
                    Log.Error("Addressables 加载失败: {0}, Status: {1}", key, handle.Status);
                    if (handle.IsValid()) Addressables.Release(handle);
                    return null;
                }
                Interlocked.Increment(ref loadCount);
                Interlocked.Increment(ref activeCount);
                return asset;
            }
            catch (OperationCanceledException)
            {
                // 取消路径：autoReleaseWhenCanceled 已自动释放句柄，无需重复释放
                return null;
            }
            catch (Exception ex)
            {
                Log.Error("Addressables 加载异常: {0}, {1}", key, ex.Message);
                // 异常路径也必须释放句柄，否则每次失败泄漏一个 AsyncOperationHandle
                if (handle.IsValid()) Addressables.Release(handle);
                return null;
            }
        }

        /// <summary>
        /// 异步实例化 GameObject（Addressables 自动管理句柄，通过 <see cref="ReleaseInstance"/> 释放）。
        /// </summary>
        /// <param name="key">Addressables key</param>
        /// <param name="parent">父 Transform（可选）</param>
        /// <param name="progress">进度回调（0~1）</param>
        /// <param name="ct">取消令牌</param>
        /// <returns>实例化结果，失败返回 null</returns>
        public async UniTask<GameObject> InstantiateAsync(object key, Transform parent = null, IProgress<float> progress = null, CancellationToken ct = default)
        {
            if (Mode == AddressablesMode.Off)
            {
                Log.Warning("AddressableSystem 处于 Off 模式，无法实例化: {0}", key);
                return null;
            }
            if (!isInited)
            {
                Log.Warning("AddressableSystem 未初始化，自动执行 Init()");
                await Init();
                if (!isInited) return null;
            }

            AsyncOperationHandle<GameObject> handle = default;
            try
            {
                handle = Addressables.InstantiateAsync(key, parent);
                var go = await handle.ToUniTask(progress, cancellationToken: ct, autoReleaseWhenCanceled: true);
                if (handle.Status != AsyncOperationStatus.Succeeded || go == null)
                {
                    Log.Error("Addressables 实例化失败: {0}, Status: {1}", key, handle.Status);
                    if (handle.IsValid()) Addressables.Release(handle);
                    return null;
                }
                Interlocked.Increment(ref loadCount);
                Interlocked.Increment(ref activeCount);
                lock (instanceHandleLock)
                {
                    instanceHandles[go] = handle;
                }
                return go;
            }
            catch (OperationCanceledException)
            {
                // 取消路径：autoReleaseWhenCanceled 已自动释放句柄
                return null;
            }
            catch (Exception ex)
            {
                Log.Error("Addressables 实例化异常: {0}, {1}", key, ex.Message);
                // 异常路径也必须释放句柄（含已创建实例的引用）
                if (handle.IsValid()) Addressables.Release(handle);
                return null;
            }
        }

        /// <summary>释放 Addressables 加载的资源（与 LoadAssetAsync 成对）</summary>
        public void ReleaseAsset<T>(T asset) where T : Object
        {
            // 仅真 null 跳过（(object) 判空绕开 Unity == 重载）：
            // 已 Destroy 的资产也尝试释放，避免假 null 短路导致引用计数永久泄漏
            if ((object)asset == null) return;
            try
            {
                Addressables.Release(asset);
                Interlocked.Increment(ref releaseCount);
                Interlocked.Decrement(ref activeCount);
            }
            catch (Exception ex)
            {
                Log.Warning("Addressables 释放资源异常: {0}", ex.Message);
            }
        }

        /// <summary>释放 Addressables 实例化的 GameObject（与 InstantiateAsync 成对）</summary>
        public void ReleaseInstance(GameObject instance)
        {
            // 仅真 null 跳过：已 Destroy 的实例（假 null）仍需释放句柄，否则引用计数永久泄漏
            if ((object)instance == null) return;

            AsyncOperationHandle<GameObject> handle = default;
            bool hasTracked = false;
            lock (instanceHandleLock)
            {
                hasTracked = instanceHandles.TryGetValue(instance, out handle);
                if (hasTracked) instanceHandles.Remove(instance);
            }

            try
            {
                if (hasTracked)
                {
                    // 优先按句柄释放：实例已被 Destroy 也能正确释放（表项在 Destroy 前登记）
                    if (handle.IsValid()) Addressables.Release(handle);
                    Interlocked.Increment(ref releaseCount);
                    Interlocked.Decrement(ref activeCount);
                }
                else if (instance != null)
                {
                    // 未经本系统 InstantiateAsync 创建（旧调用方）：回退标准释放
                    Addressables.ReleaseInstance(instance);
                    Interlocked.Increment(ref releaseCount);
                    Interlocked.Decrement(ref activeCount);
                }
                else
                {
                    // 已销毁且无句柄记录：无法匹配句柄，仅告警（避免静默泄漏）
                    Log.Warning("AddressableSystem.ReleaseInstance: 实例已销毁且无句柄记录，无法释放（请确保实例经 InstantiateAsync 创建）");
                }
            }
            catch (Exception ex)
            {
                Log.Warning("Addressables 释放实例异常: {0}", ex.Message);
            }
        }

        /// <summary>
        /// 双轨加载：Addressables 优先，失败自动降级 ResourcesSystem（双轨迁移期推荐入口）。
        /// 注意：仅降级返回的资源走 ResourcesSystem 缓存体系；Addressables 成功时按本系统 Release 规则释放。
        /// </summary>
        /// <typeparam name="T">资源类型</typeparam>
        /// <param name="addrKey">Addressables key</param>
        /// <param name="resourcePath">Resources 路径（降级用）</param>
        /// <param name="progress">进度回调</param>
        /// <param name="ct">取消令牌</param>
        /// <returns>资源对象，均失败返回 null</returns>
        public async UniTask<T> LoadWithFallbackAsync<T>(string addrKey, string resourcePath, IProgress<float> progress = null, CancellationToken ct = default) where T : Object
        {
            if (Mode != AddressablesMode.Off)
            {
                var asset = await LoadAssetAsync<T>(addrKey, progress, ct);
                if (asset != null) return asset;
                Log.Warning("Addressables 加载失败，降级 Resources: {0} -> {1}", addrKey, resourcePath);
            }
            return await ResourcesSystem.GetOrInstance().LoadAsync<T>(resourcePath);
        }
        #endregion

        // 场景加载/卸载 API 已拆分到 AddressableSystem.Scene.cs（partial），
        // 避免主文件继续膨胀；句柄重载与释放语义见该文件。

        // 场景 API 见 AddressableSystem.Scene.cs


        #region 远程更新
        /// <summary>
        /// 检查远程 Catalog 是否有更新（仅 Remote 模式有效）。有更新时内部记录待更新列表，供 UpdateContentAsync 使用。
        /// </summary>
        /// <returns>检查结果；失败返回 hasUpdate=false（仅告警）</returns>
        public async UniTask<AddressableUpdateResult> CheckUpdateAsync(CancellationToken ct = default)
        {
            var result = new AddressableUpdateResult();
            if (Mode != AddressablesMode.Remote) return result;

            // Catalog 锚点校验失败：拒绝检查远程更新（防止基于被篡改 Catalog 的热更）
            if (catalogAnchorFailed)
            {
                Log.Error("AddressableSystem Catalog 锚点校验失败，已跳过远程更新检查");
                result.checkFailed = true;
                return result;
            }

            // 进入即清空：否则上一轮检测到更新、本轮无更新时旧列表残留，
            // UpdateContentAsync(default) 会重新下载并应用已应用的旧 Catalog（重复热更）
            pendingUpdateCatalogs.Clear();

            try
            {
                // autoReleaseHandle=true：句柄完成自动释放，防泄漏
                var handle = Addressables.CheckForCatalogUpdates(true);
                var catalogs = await handle.ToUniTask(cancellationToken: ct, autoReleaseWhenCanceled: true);
                if (catalogs != null && catalogs.Count > 0)
                {
                    result.hasUpdate = true;
                    result.updatedCatalogs = catalogs;
                    pendingUpdateCatalogs.Clear();
                    pendingUpdateCatalogs.AddRange(catalogs);
                    Log.Debug("AddressableSystem 检测到可更新 Catalog: {0}", catalogs.Count);
                }
            }
            catch (Exception ex)
            {
                Log.Warning("AddressableSystem 检查更新失败: {0}", ex.Message);
                // 区分"检查失败"与"确认无更新"：否则上层会把网络故障显示成已是最新
                result.checkFailed = true;
            }
            return result;
        }

        /// <summary>
        /// 下载并应用远程更新（仅 Remote 模式有效）。可传入 CheckUpdateAsync 的结果，否则使用内部记录。
        /// </summary>
        /// <returns>是否更新成功；失败返回 false（已记录 Error 日志）</returns>
        public async UniTask<bool> UpdateContentAsync(AddressableUpdateResult result, IProgress<float> progress = null, CancellationToken ct = default)
        {
            if (Mode != AddressablesMode.Remote) return false;

            // Catalog 锚点校验失败：拒绝应用远程更新（仅用内嵌/本地内容）
            if (catalogAnchorFailed)
            {
                Log.Error("AddressableSystem Catalog 锚点校验失败，已拒绝应用远程更新");
                return false;
            }

            var catalogs = result.updatedCatalogs ?? pendingUpdateCatalogs;
            if (catalogs == null || catalogs.Count == 0)
            {
                Log.Warning("AddressableSystem 无待更新 Catalog，跳过更新");
                return false;
            }

            try
            {
                // 首次远程 Catalog 下载（本地缓存无 Catalog）：下载后需与内嵌锚点核对，
                // 堵住"首次安装无信任锚点"的中间人替换面；后续更新由 Addressables 内部 hash 锚定
                bool firstRemoteCatalog = FindCachedCatalogFile() == null;

                var handle = Addressables.UpdateCatalogs(catalogs, true);
                var locators = await handle.ToUniTask(progress, cancellationToken: ct, autoReleaseWhenCanceled: true);
                if (locators == null || locators.Count == 0)
                {
                    Log.Warning("AddressableSystem 更新完成但无新 Locator");
                    return false;
                }

                // 首次下载校验：Catalog md5 与内嵌锚点不一致 → 判定被篡改，拒绝应用并清除缓存
                if (firstRemoteCatalog && !await VerifyFirstCatalogAgainstAnchorAsync())
                {
                    Log.Error("AddressableSystem 首次远程 Catalog 与内嵌锚点不一致，已拒绝应用并清除缓存");
                    pendingUpdateCatalogs.Clear();
                    return false;
                }

                pendingUpdateCatalogs.Clear();
                Log.Debug("AddressableSystem 内容更新成功: {0} Locator", locators.Count);
                return true;
            }
            catch (Exception ex)
            {
                Log.Error("AddressableSystem 内容更新失败: {0}", ex.Message);
                return false;
            }
        }

        #region Catalog 锚点校验
        /// <summary>内嵌锚点清单（StreamingAssets/version.json，构建时由 AddressablesBuildWindow 烘焙）</summary>
        [Serializable]
        private class BakedVersionManifest
        {
            public string catalogHash;
        }

        private const string AnchorFileName = "version.json";

        /// <summary>
        /// 校验首次下载的远程 Catalog 与内嵌锚点（StreamingAssets/version.json）的 catalogHash 是否一致。
        /// 仅当存在内嵌锚点且本地尚无 Addressables Catalog 缓存（首次启动）时校验 ——
        /// 后续更新的完整性由 Addressables 内部 hash 对比（本地缓存锚点）保证，此处不重复校验避免误报。
        /// 不一致时置 catalogAnchorFailed（拒绝远程更新，仅用内嵌/本地内容）。
        /// </summary>
        private async UniTask VerifyCatalogAnchorAsync()
        {
            // 本地已有 Catalog 缓存：后续更新由 Addressables 内部 hash 机制锚定，跳过
            if (FindCachedCatalogFile() != null) return;

            string anchorHash = await LoadAnchorCatalogHashAsync();
            if (string.IsNullOrEmpty(anchorHash))
            {
                // 无内嵌锚点：首次安装的远程 Catalog 无信任锚点，提示部署方烘焙 version.json
                Log.Warning("AddressableSystem 未找到内嵌锚点（StreamingAssets/version.json），首次远程 Catalog 无法校验。建议构建时烘焙 version.json（含 catalogHash）");
                return;
            }

            var cachedCatalog = FindCachedCatalogFile();
            if (cachedCatalog == null)
            {
                // 远程 Catalog 尚未下载完成（首次启动也可能走内嵌 Catalog），本次跳过
                return;
            }

            string actualHash = Md5File(cachedCatalog);
            if (string.IsNullOrEmpty(actualHash)
                || !string.Equals(actualHash, anchorHash, StringComparison.OrdinalIgnoreCase))
            {
                catalogAnchorFailed = true;
                Log.Error("AddressableSystem Catalog 锚点校验失败：本地 Catalog(md5={0}) ≠ 内嵌锚点({1})，疑似被篡改，已拒绝远程更新",
                    actualHash ?? "N/A", anchorHash);
            }
        }

        /// <summary>
        /// 首次 Catalog 下载后的校验（UpdateContentAsync 使用，跨平台支持 Android StreamingAssets）：
        /// md5 与内嵌锚点不一致时置失败标记并删除被篡改的 Catalog 缓存，返回 false。
        /// </summary>
        private async UniTask<bool> VerifyFirstCatalogAgainstAnchorAsync()
        {
            string anchorHash = await LoadAnchorCatalogHashAsync();
            if (string.IsNullOrEmpty(anchorHash))
            {
                Log.Warning("AddressableSystem 未找到内嵌锚点（StreamingAssets/version.json），首次远程 Catalog 无法校验。建议构建时烘焙 version.json（含 catalogHash）");
                return true; // 无锚点不阻断（保持向后兼容），仅告警
            }

            var cachedCatalog = FindCachedCatalogFile();
            if (cachedCatalog == null) return true; // 未落盘缓存（可能走内存），本次跳过

            string actualHash = Md5File(cachedCatalog);
            if (string.IsNullOrEmpty(actualHash)
                || !string.Equals(actualHash, anchorHash, StringComparison.OrdinalIgnoreCase))
            {
                catalogAnchorFailed = true;
                Log.Error("AddressableSystem 首次远程 Catalog 校验失败：本地(md5={0}) ≠ 锚点({1})，疑似被篡改", actualHash ?? "N/A", anchorHash);
                try { File.Delete(cachedCatalog); } catch { /* 清理失败忽略 */ }
                return false;
            }
            return true;
        }

        /// <summary>读取内嵌锚点的 catalogHash（Android 的 StreamingAssets 位于 APK 内，需经 UnityWebRequest）</summary>
        private static UniTask<string> LoadAnchorCatalogHashAsync()
        {
            try
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                return LoadAnchorHashViaWebRequestAsync();
#else
                string anchorPath = Path.Combine(Application.streamingAssetsPath, AnchorFileName);
                string json = File.Exists(anchorPath) ? File.ReadAllText(anchorPath) : null;
                return UniTask.FromResult(ParseCatalogHash(json));
#endif
            }
            catch (Exception ex)
            {
                Log.Warning("AddressableSystem 读取内嵌锚点失败: {0}", ex.Message);
                return UniTask.FromResult<string>(null);
            }
        }

        /// <summary>解析内嵌锚点 JSON 中的 catalogHash</summary>
        private static string ParseCatalogHash(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            var manifest = JsonUtility.FromJson<BakedVersionManifest>(json);
            return manifest?.catalogHash;
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        /// <summary>Android：StreamingAssets 位于 APK 内，经 UnityWebRequest 异步读取</summary>
        private static async UniTask<string> LoadAnchorHashViaWebRequestAsync()
        {
            using (var uwr = UnityWebRequest.Get(Application.streamingAssetsPath + "/" + AnchorFileName))
            {
                await uwr.SendWebRequest().ToUniTask();
                if (uwr.result == UnityWebRequest.Result.Success)
                {
                    return ParseCatalogHash(uwr.downloadHandler?.text);
                }
            }
            return null;
        }
#endif

        /// <summary>在 Addressables 缓存目录查找已下载的远程 Catalog 文件（catalog_*.bin）</summary>
        private static string FindCachedCatalogFile()
        {
            try
            {
                if (!Directory.Exists(Application.persistentDataPath)) return null;
                var files = Directory.GetFiles(Application.persistentDataPath, "catalog_*.bin", SearchOption.AllDirectories);
                if (files == null || files.Length == 0) return null;
                // 多个缓存取最新修改的（当前生效版本）
                string newest = null;
                DateTime newestTime = DateTime.MinValue;
                foreach (var f in files)
                {
                    var info = new FileInfo(f);
                    if (info.LastWriteTimeUtc > newestTime)
                    {
                        newestTime = info.LastWriteTimeUtc;
                        newest = f;
                    }
                }
                return newest;
            }
            catch (Exception ex)
            {
                Log.Debug("AddressableSystem 查找缓存 Catalog 失败: {0}", ex.Message);
                return null;
            }
        }

        /// <summary>计算文件 MD5（与编辑器 version.json 生成逻辑一致）</summary>
        private static string Md5File(string path)
        {
            try
            {
                using var md5 = MD5.Create();
                using var stream = File.OpenRead(path);
                var hash = md5.ComputeHash(stream);
                var sb = new StringBuilder(hash.Length * 2);
                foreach (var b in hash) sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
            catch
            {
                return null;
            }
        }
        #endregion
        #endregion

        #region 内存 / 统计
        /// <summary>清理 Addressables 不再引用的 Bundle 缓存（WebGL 下为 IndexedDB 缓存）</summary>
        public async UniTask CleanBundleCache()
        {
            try
            {
                var handle = Addressables.CleanBundleCache();
                await handle.ToUniTask();
                Log.Debug("AddressableSystem Bundle 缓存清理完成");
            }
            catch (Exception ex)
            {
                Log.Warning("AddressableSystem Bundle 缓存清理失败: {0}", ex.Message);
            }
        }

        /// <summary>卸载未使用资源（合并调用 UnityEngine.Resources.UnloadUnusedAssets）</summary>
        public async UniTask CleanUnusedAsync()
        {
            var request = UnityEngine.Resources.UnloadUnusedAssets();
            await request;
        }

        /// <summary>当前未释放句柄数（线程安全读：写侧 Interlocked）</summary>
        public int ActiveHandles => Volatile.Read(ref activeCount);

        /// <summary>调试统计（活跃句柄数 / 累计加载释放数），用于泄漏排查</summary>
        public string GetDebugStats()
        {
            return string.Format("AddressableSystem | Mode={0} | 累计加载={1} | 累计释放={2} | 未释放={3} | 待更新Catalog={4}",
                Mode, Volatile.Read(ref loadCount), Volatile.Read(ref releaseCount), ActiveHandles, pendingUpdateCatalogs.Count);
        }
        #endregion
    }
}
