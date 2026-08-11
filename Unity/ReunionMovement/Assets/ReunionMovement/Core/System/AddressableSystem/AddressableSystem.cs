using ReunionMovement.Common;
using ReunionMovement.Core.Base;
using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace ReunionMovement.Core.Resources
{
    /// <summary>
    /// Addressables 运行模式（由 GameConfig 配置）
    /// </summary>
    public enum AddressablesMode
    {
        /// <summary>完全关闭 Addressables（纯 Resources 降级模式）</summary>
        Off = 0,
        /// <summary>仅本地 Bundle（无远程更新）</summary>
        LocalOnly = 1,
        /// <summary>本地 + 远程（支持 CDN 热更）</summary>
        Remote = 2,
    }

    /// <summary>
    /// Addressables 远程更新检查结果
    /// </summary>
    public struct AddressableUpdateResult
    {
        /// <summary>是否存在可更新 Catalog</summary>
        public bool hasUpdate;
        /// <summary>可更新的 Catalog 列表（hasUpdate 为 true 时非空）</summary>
        public List<string> updatedCatalogs;
    }

    /// <summary>
    /// Addressables 统一封装 —— 受管资源异步加载/实例化/释放 + 远程更新。
    /// 全部基于 UniTask，支持进度与取消；失败可降级到 ResourcesSystem。
    /// 生命周期由 Addressables 自带引用计数管理，调用方须成对调用 Load / Release。
    /// 设计文档：仓库 Docs/Addressables/Addressables集成设计方案.md §5
    /// </summary>
    public class AddressableSystem : ICustomSystem, ISystemDisposable
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

        // 调试统计（Interlocked 保证线程安全，Addressables 回调可能跨线程）
        private int loadCount;      // 累计加载/实例化次数
        private int releaseCount;   // 累计释放次数
        private int activeCount;    // 当前未释放数（load - release）
        #endregion

        /// <summary>
        /// 初始化 Addressables。Off 模式直接跳过；Remote 模式附带 Catalog 更新检查（失败不阻断启动）。
        /// 初始化失败不抛出 —— 业务层可通过 LoadWithFallbackAsync 降级到 ResourcesSystem。
        /// </summary>
        public async UniTask Init()
        {
            initProgress = 0;
            isInited = false;

            if (Mode == AddressablesMode.Off)
            {
                initProgress = 100;
                Log.Debug("AddressableSystem 已关闭（AddressablesMode.Off），跳过初始化");
                return;
            }

            try
            {
                var handle = Addressables.InitializeAsync();
                await handle.ToUniTask();
                initProgress = 50;

                // 远程模式：检查 Catalog 更新（仅告警，不阻断启动）
                if (Mode == AddressablesMode.Remote)
                {
                    try
                    {
                        var result = await CheckUpdateAsync();
                        Log.Debug("AddressableSystem 远程更新检查: {0}", result.hasUpdate ? "有更新待下载" : "无更新");
                    }
                    catch (Exception ex)
                    {
                        Log.Warning("AddressableSystem 远程更新检查失败（继续启动）: {0}", ex.Message);
                    }
                }

                initProgress = 100;
                isInited = true;
                Log.Debug("AddressableSystem 初始化完成 (Mode={0})", Mode);
            }
            catch (Exception ex)
            {
                Log.Error("AddressableSystem 初始化失败（业务层将降级 Resources）: {0}", ex.Message);
                isInited = false;
            }
        }

        public void Clear()
        {
            Log.Debug("AddressableSystem 清除数据");
            pendingUpdateCatalogs.Clear();
            loadCount = 0;
            releaseCount = 0;
            activeCount = 0;
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

            try
            {
                var handle = Addressables.LoadAssetAsync<T>(key);
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
            catch (Exception ex)
            {
                Log.Error("Addressables 加载异常: {0}, {1}", key, ex.Message);
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

            try
            {
                var handle = Addressables.InstantiateAsync(key, parent);
                var go = await handle.ToUniTask(progress, cancellationToken: ct, autoReleaseWhenCanceled: true);
                if (handle.Status != AsyncOperationStatus.Succeeded || go == null)
                {
                    Log.Error("Addressables 实例化失败: {0}, Status: {1}", key, handle.Status);
                    return null;
                }
                Interlocked.Increment(ref loadCount);
                Interlocked.Increment(ref activeCount);
                return go;
            }
            catch (Exception ex)
            {
                Log.Error("Addressables 实例化异常: {0}, {1}", key, ex.Message);
                return null;
            }
        }

        /// <summary>释放 Addressables 加载的资源（与 LoadAssetAsync 成对）</summary>
        public void ReleaseAsset<T>(T asset) where T : Object
        {
            if (asset == null) return;
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
            if (instance == null) return;
            try
            {
                Addressables.ReleaseInstance(instance);
                Interlocked.Increment(ref releaseCount);
                Interlocked.Decrement(ref activeCount);
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
            return await ResourcesSystem.Instance.LoadAsync<T>(resourcePath);
        }
        #endregion

        #region 场景
        /// <summary>
        /// 异步加载场景（Addressable 场景）。返回的 SceneInstance 通过 <see cref="UnloadSceneAsync"/> 卸载。
        /// </summary>
        public async UniTask<SceneInstance> LoadSceneAsync(object key, LoadSceneMode mode = LoadSceneMode.Single, IProgress<float> progress = null, CancellationToken ct = default)
        {
            if (Mode == AddressablesMode.Off)
            {
                Log.Warning("AddressableSystem 处于 Off 模式，无法加载场景: {0}", key);
                return default;
            }
            if (!isInited)
            {
                Log.Warning("AddressableSystem 未初始化，自动执行 Init()");
                await Init();
                if (!isInited) return default;
            }

            try
            {
                var handle = Addressables.LoadSceneAsync(key, mode);
                var scene = await handle.ToUniTask(progress, cancellationToken: ct, autoReleaseWhenCanceled: true);
                if (handle.Status != AsyncOperationStatus.Succeeded)
                {
                    Log.Error("Addressables 场景加载失败: {0}, Status: {1}", key, handle.Status);
                    return default;
                }
                Interlocked.Increment(ref loadCount);
                Interlocked.Increment(ref activeCount);
                return scene;
            }
            catch (Exception ex)
            {
                Log.Error("Addressables 场景加载异常: {0}, {1}", key, ex.Message);
                return default;
            }
        }

        /// <summary>卸载 Addressable 场景（与 LoadSceneAsync 成对）</summary>
        public async UniTask UnloadSceneAsync(SceneInstance sceneInstance, IProgress<float> progress = null, CancellationToken ct = default)
        {
            try
            {
                var handle = Addressables.UnloadSceneAsync(sceneInstance);
                await handle.ToUniTask(progress, cancellationToken: ct, autoReleaseWhenCanceled: true);
                Interlocked.Increment(ref releaseCount);
                Interlocked.Decrement(ref activeCount);
            }
            catch (Exception ex)
            {
                Log.Error("Addressables 场景卸载异常: {0}", ex.Message);
            }
        }
        #endregion

        #region 远程更新
        /// <summary>
        /// 检查远程 Catalog 是否有更新（仅 Remote 模式有效）。有更新时内部记录待更新列表，供 UpdateContentAsync 使用。
        /// </summary>
        /// <returns>检查结果；失败返回 hasUpdate=false（仅告警）</returns>
        public async UniTask<AddressableUpdateResult> CheckUpdateAsync(CancellationToken ct = default)
        {
            var result = new AddressableUpdateResult();
            if (Mode != AddressablesMode.Remote) return result;

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

            var catalogs = result.updatedCatalogs ?? pendingUpdateCatalogs;
            if (catalogs == null || catalogs.Count == 0)
            {
                Log.Warning("AddressableSystem 无待更新 Catalog，跳过更新");
                return false;
            }

            try
            {
                var handle = Addressables.UpdateCatalogs(catalogs, true);
                var locators = await handle.ToUniTask(progress, cancellationToken: ct, autoReleaseWhenCanceled: true);
                if (locators == null || locators.Count == 0)
                {
                    Log.Warning("AddressableSystem 更新完成但无新 Locator");
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

        /// <summary>调试统计（活跃句柄数 / 累计加载释放数），用于泄漏排查</summary>
        public string GetDebugStats()
        {
            return string.Format("AddressableSystem | Mode={0} | 累计加载={1} | 累计释放={2} | 未释放={3} | 待更新Catalog={4}",
                Mode, loadCount, releaseCount, activeCount, pendingUpdateCatalogs.Count);
        }
        #endregion
    }
}
