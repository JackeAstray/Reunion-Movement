using ReunionMovement.Common;
using ReunionMovement.Core.Base;
using ReunionMovement.Common.Util;
using ReunionMovement.Common.Util.Download;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using Object = UnityEngine.Object;
using System.Linq;
using ReunionMovement.Common.Util.HttpService;
using ReunionMovement.Common.Util.Manager;

namespace ReunionMovement.Core.Resources
{
    /// <summary>
    /// 简易 AssetBundle 热更/加载管理（优先使用本框架下载模块）
    /// 支持：先从本地/下载的 AssetBundle 加载资源，或回退到 ResourcesSystem
    /// 注意：这是一个精简实现，用于快速集成与测试，不包括依赖管理/manifest解析等
    /// </summary>
    public class AssetBundlesSystem : ICustommSystem
    {
        #region 单例与初始化
        private static readonly Lazy<AssetBundlesSystem> instance = new(() => new AssetBundlesSystem());
        public static AssetBundlesSystem Instance => instance.Value;

        public bool isInited { get; private set; }
        private double initProgress = 0;
        public double InitProgress => initProgress;
        #endregion

        // 已加载的 AssetBundle（key: 本地路径 或 bundle 标识）
        private readonly Dictionary<string, AssetBundle> bundleTable = new Dictionary<string, AssetBundle>(StringComparer.OrdinalIgnoreCase);
        // 缓存已加载的资源（key: bundleKey + "::" + assetName 或 resources path）
        private readonly Dictionary<string, Object> assetTable = new Dictionary<string, Object>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, int> assetRefCount = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        // 正在进行的 Bundle 加载任务，防止并发重复加载
        private readonly Dictionary<string, Task<AssetBundle>> loadingBundles = new Dictionary<string, Task<AssetBundle>>(StringComparer.OrdinalIgnoreCase);

        // 用于线程安全的锁
        private readonly object bundleLock = new object();
        private readonly object assetLock = new object();
        private readonly object dependencyLock = new object();

        private readonly BundleVersionManager versionManager = new BundleVersionManager();

        // 新增事件
        public event Action<string> OnBundleDownloaded;
        public event Action<string> OnBundleLoadFailed;

        // 依赖缓存：bundleName -> list of 本地路径（已解析和下载）
        private readonly Dictionary<string, List<string>> dependencyCache = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        // 并发下载去重/队列：url -> Lazy<Task<string>> 确保仅创建一次下载任务
        private readonly Dictionary<string, Lazy<Task<string>>> ongoingDownloads = new Dictionary<string, Lazy<Task<string>>>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// 初始化
        /// </summary>
        /// <returns></returns>
        public Task Init()
        {
            initProgress = 0;

            // 尝试加载本地 manifest
            string manifestLocal = Path.Combine(PathUtil.GetLocalPath(DownloadType.PersistentAssets), "bundle_manifest.json");
            versionManager.LoadLocalManifest(manifestLocal);

            initProgress = 100;
            isInited = true;
            Log.Debug("AssetBundlesSystem 初始化完成");
            return Task.CompletedTask;
        }

        /// <summary>
        /// 定期更新
        /// </summary>
        /// <param name="logicTime"></param>
        /// <param name="realTime"></param>
        public void Update(float logicTime, float realTime)
        {
            // 无需定期逻辑，下载由 DownloadMgr 负责
        }

        /// <summary>
        /// 清除所有已加载的 Bundle 和 资源缓存
        /// </summary>
        public void Clear()
        {
            Log.Debug("AssetBundlesSystem 清除数据");

            lock (assetLock)
            {
                foreach (var kvp in assetTable)
                {
                    if (kvp.Value != null)
                    {
                        try { UnityMainThreadDispatcher.RunOnMainThread(() => Object.Destroy(kvp.Value)); } catch { }
                    }
                }
                assetTable.Clear();
                assetRefCount.Clear();
            }

            lock (bundleLock)
            {
                // 注意：无法取消正在进行的 loadingBundles 任务，它们完成后会尝试加入 bundleTable
                // 但这里我们先清空当前表
                loadingBundles.Clear(); // 清空加载任务记录
                foreach (var kvp in bundleTable)
                {
                    try { kvp.Value.Unload(true); } catch { }
                }
                bundleTable.Clear();
            }

            lock (dependencyLock)
            {
                dependencyCache.Clear();
            }

            lock (ongoingDownloads)
            {
                ongoingDownloads.Clear();
            }

            isInited = false;
        }

        #region 帮助方法
        /// <summary>
        /// 等待 AsyncOperation 完成
        /// </summary>
        /// <param name="op"></param>
        /// <returns></returns>
        private static Task AwaitAsyncOperation(AsyncOperation op)
        {
            var tcs = new TaskCompletionSource<bool>();
            if (op == null || op.isDone)
            {
                tcs.TrySetResult(true);
                return tcs.Task;
            }
            op.completed += _ => tcs.TrySetResult(true);
            return tcs.Task;
        }
        /// <summary>
        /// 在主线程运行无返回值的函数
        /// </summary>
        /// <param name="action"></param>
        /// <returns></returns>
        private static Task RunOnMainThread(Action action)
        {
            var tcs = new TaskCompletionSource<bool>();
            try
            {
                UnityMainThreadDispatcher.RunOnMainThread(() =>
                {
                    try
                    {
                        action();
                        tcs.TrySetResult(true);
                    }
                    catch (Exception ex)
                    {
                        tcs.TrySetException(ex);
                    }
                });
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
            return tcs.Task;
        }

        /// <summary>
        /// 在主线程运行有返回值的函数
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="func"></param>
        /// <returns></returns>
        private static Task<T> RunOnMainThread<T>(Func<T> func)
        {
            var tcs = new TaskCompletionSource<T>();
            try
            {
                UnityMainThreadDispatcher.RunOnMainThread(() =>
                {
                    try
                    {
                        var r = func();
                        tcs.TrySetResult(r);
                    }
                    catch (Exception ex)
                    {
                        tcs.TrySetException(ex);
                    }
                });
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
            return tcs.Task;
        }

        /// <summary>
        /// 生成资源缓存 Key
        /// </summary>
        /// <param name="bundleKey"></param>
        /// <param name="assetName"></param>
        /// <returns></returns>
        private string MakeAssetKey(string bundleKey, string assetName)
        {
            return string.IsNullOrEmpty(bundleKey) ? assetName : bundleKey + "::" + assetName;
        }

        /// <summary>
        /// 尝试更健壮地根据 URL 或名字找到 manifest 中的 BundleInfo
        /// </summary>
        /// <param name="identifierOrUrl"></param>
        /// <returns></returns>
        private BundleInfo FindManifestInfoFor(string identifierOrUrl)
        {
            if (versionManager.Manifest == null) return null;

            // 先按直接匹配 URL
            foreach (var b in versionManager.Manifest.bundles)
            {
                if (!string.IsNullOrEmpty(b.url) && string.Equals(b.url, identifierOrUrl, StringComparison.OrdinalIgnoreCase))
                    return b;
            }

            // 尝试按传入的 identifier 是否等于 fileName 或 name
            var byName = versionManager.Manifest.GetBundle(identifierOrUrl);
            if (byName != null) return byName;

            // 如果传入是 URL，尝试用 URL 最后的文件名去匹配 manifest.fileName
            try
            {
                var uri = new Uri(identifierOrUrl);
                var last = Path.GetFileName(uri.AbsolutePath);
                if (!string.IsNullOrEmpty(last))
                {
                    var byFile = versionManager.Manifest.GetBundle(last);
                    if (byFile != null) return byFile;
                }
            }
            catch { }

            // 最后按 fileName 字符串相等（忽略大小写）查找
            var fallback = versionManager.Manifest.bundles.FirstOrDefault(b => string.Equals(b.fileName, identifierOrUrl, StringComparison.OrdinalIgnoreCase));
            return fallback;
        }
        #endregion

        #region 下载与加载 Bundle
        /// <summary>
        /// 下载远程 bundle（如果已经存在则直接返回本地路径）
        /// 返回本地文件完整路径
        /// 实现：并发去重 + 原子替换（先下载到临时目录，成功后替换），失败时回滚（旧文件保留）
        /// </summary>
        /// <param name="bundleUrl"></param>
        /// <returns></returns>
        public async Task<string> DownloadBundleIfNeeded(string bundleUrl)
        {
            if (string.IsNullOrEmpty(bundleUrl)) return string.Empty;

            // 如果是 bundle 名称（无 http 前缀），尝试从 manifest 获取 url
            if (!bundleUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                !bundleUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase) &&
                !Path.IsPathRooted(bundleUrl))
            {
                var info = versionManager.Manifest?.GetBundle(bundleUrl);
                if (info != null && !string.IsNullOrEmpty(info.url))
                {
                    bundleUrl = info.url;
                }
            }

            // 如果已经是本地路径或 streamingAssets，则直接返回
            if (!bundleUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                !bundleUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                // 视为本地路径，尝试计算完整路径
                if (PathUtil.GetFullPath(bundleUrl, out var full) && !string.IsNullOrEmpty(full))
                {
                    return full;
                }
                return bundleUrl;
            }

            Lazy<Task<string>> lazyTask;
            lock (ongoingDownloads)
            {
                if (!ongoingDownloads.TryGetValue(bundleUrl, out lazyTask))
                {
                    // 创建单个 Lazy Task，保证只有一个 InternalDownloadBundle 在运行
                    lazyTask = new Lazy<Task<string>>(() => InternalDownloadBundle(bundleUrl), true);
                    ongoingDownloads[bundleUrl] = lazyTask;
                }
            }

            try
            {
                var result = await lazyTask.Value;
                return result;
            }
            finally
            {
                lock (ongoingDownloads)
                {
                    // 移除已完成的任务
                    if (ongoingDownloads.TryGetValue(bundleUrl, out var current) && current == lazyTask)
                    {
                        ongoingDownloads.Remove(bundleUrl);
                    }
                }
            }
        }

        /// <summary>
        /// 实际的下载实现
        /// </summary>
        /// <param name="bundleUrl"></param>
        /// <returns></returns>
        /// <exception cref="FileNotFoundException"></exception>
        private async Task<string> InternalDownloadBundle(string bundleUrl)
        {
            // 1. 优先使用 Manifest 中的 fileName 以保持目录结构（避免不同目录下同名文件冲突）
            var infoFromManifest = FindManifestInfoFor(bundleUrl);
            string relPath = (infoFromManifest != null && !string.IsNullOrEmpty(infoFromManifest.fileName))
                             ? infoFromManifest.fileName
                             : PathUtil.GetFileNameByUrl(bundleUrl);

            string saveDir = PathUtil.GetLocalPath(DownloadType.PersistentAssets);
            string localPath = Path.Combine(saveDir, relPath);
            string localVersionFile = localPath + ".version"; // 版本文件跟随 Bundle

            // 检查版本
            if (infoFromManifest != null && !versionManager.NeedsUpdate(infoFromManifest.fileName, localVersionFile) && File.Exists(localPath))
            {
                return localPath;
            }

            // 用于后续在 tempRoot 查找文件的简单的文件名
            string fileName = Path.GetFileName(relPath);

            if (File.Exists(localPath))
            {
                // 如果没有 manifest 信息，只要文件存在就直接返回
                if (infoFromManifest == null)
                {
                    return localPath;
                }
            }

            // 下载到临时目录，下载成功后再替换到目标目录（保证回滚）
            string tempRoot = Path.Combine(saveDir, ".tmp_download_") + Guid.NewGuid().ToString("N");
            try
            {
                if (!Directory.Exists(tempRoot)) Directory.CreateDirectory(tempRoot);

                var tcs = new TaskCompletionSource<bool>();

                // 使用 DownloadMgr 下载到临时目录，关闭 MD5 命名以保留原始文件名（便于匹配 manifest）
                try
                {
                    DownloadMgr.Instance.DownloadFiles(new List<string> { bundleUrl }, tempRoot,
                        progress: null,
                        action: () => tcs.TrySetResult(true),
                        error: (err) => tcs.TrySetException(new Exception(err)),
                        skipIfExists: false,
                        deleteIfExists: false,
                        useMd5Name: false);
                }
                catch (Exception ex)
                {
                    // 确保在主线程触发失败事件
                    UnityMainThreadDispatcher.RunOnMainThread(() => OnBundleLoadFailed?.Invoke(bundleUrl));
                    Log.Error($"开始下载 bundle 失败: {bundleUrl}, {ex}");
                    throw;
                }

                await tcs.Task; // 等待下载

                // 目标文件可能包含基于 URL 的子目录；计算相对文件路径
                string downloadedFilePath = Path.Combine(tempRoot, ReunionMovement.Common.Util.Download.HTTPHelper.GetRelativePathFromUri(bundleUrl));

                // 回退：如果未找到，尝试按文件名查找
                if (!File.Exists(downloadedFilePath))
                {
                    // 在 tempRoot 中按文件名查找
                    var found = Directory.GetFiles(tempRoot, "*", SearchOption.AllDirectories)
                        .FirstOrDefault(f => Path.GetFileName(f).Equals(fileName, StringComparison.OrdinalIgnoreCase));
                    if (!string.IsNullOrEmpty(found)) downloadedFilePath = found;
                }

                if (!File.Exists(downloadedFilePath))
                {
                    // 作为最终回退，再尝试以 manifest 中的 fileName 查找
                    if (infoFromManifest != null)
                    {
                        var candidate = Directory.GetFiles(tempRoot, "*", SearchOption.AllDirectories)
                            .FirstOrDefault(f => Path.GetFileName(f).Equals(infoFromManifest.fileName, StringComparison.OrdinalIgnoreCase));
                        if (!string.IsNullOrEmpty(candidate)) downloadedFilePath = candidate;
                    }
                }

                if (!File.Exists(downloadedFilePath))
                {
                    UnityMainThreadDispatcher.RunOnMainThread(() => OnBundleLoadFailed?.Invoke(bundleUrl));
                    throw new FileNotFoundException("下载后未找到 bundle 文件", downloadedFilePath);
                }

                // 备份旧文件（如果存在）到 .bak
                string backupPath = null;
                if (File.Exists(localPath))
                {
                    backupPath = localPath + ".bak_" + DateTime.UtcNow.ToString("yyyyMMddHHmmss");
                    try
                    {
                        File.Copy(localPath, backupPath, true);
                    }
                    catch (Exception ex)
                    {
                        Log.Warning("创建旧 bundle 备份失败: " + ex.Message);
                        backupPath = null;
                    }
                }

                // 覆盖/移动新文件到目标位置
                try
                {
                    // 确保目标目录存在
                    var dir = Path.GetDirectoryName(localPath);
                    if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);

                    // 修复：检查是否正在加载中，需等待其完成，防止文件占用冲突
                    Task<AssetBundle> pendingLoadTask = null;
                    lock (bundleLock)
                    {
                        loadingBundles.TryGetValue(localPath, out pendingLoadTask);
                    }
                    if (pendingLoadTask != null)
                    {
                        try { await pendingLoadTask; } catch { }
                    }

                    // 如果 Bundle 已加载，必须先卸载，否则 Windows 下无法覆盖文件 (共享冲突)
                    bool isLoaded = false;
                    lock (bundleLock)
                    {
                        isLoaded = bundleTable.ContainsKey(localPath);
                    }
                    if (isLoaded)
                    {
                        // 切换到主线程卸载
                        await RunOnMainThread(() =>
                        {
                            // 强制卸载 Header 以释放文件句柄 
                            // 注意：使用 skipDestroyObjects=true 防止销毁正在使用的资源导致报错（允许内存泄漏直到场景切换或引用释放），
                            // 但我们会从 assetTable 移除它们，确保下次加载获取新资源。
                            UnloadBundle(localPath, unloadAllLoadedObjects: false, force: true, skipDestroyObjects: true);
                        });
                    }

                    File.Copy(downloadedFilePath, localPath, true);

                    // 保存版本信息
                    if (infoFromManifest != null)
                    {
                        versionManager.SaveLocalVersion(localVersionFile, infoFromManifest.version);
                    }

                    // 在主线程触发已下载事件
                    UnityMainThreadDispatcher.RunOnMainThread(() => OnBundleDownloaded?.Invoke(localPath));

                    // 删除备份
                    if (backupPath != null)
                    {
                        try { File.Delete(backupPath); } catch { }
                    }

                    return localPath;
                }
                catch (Exception ex)
                {
                    // 回滚：如果备份存在，尝试恢复
                    if (backupPath != null && File.Exists(backupPath))
                    {
                        try
                        {
                            File.Copy(backupPath, localPath, true);
                        }
                        catch (Exception ex2)
                        {
                            Log.Error("回滚旧 bundle 失败: " + ex2.Message);
                        }
                    }

                    UnityMainThreadDispatcher.RunOnMainThread(() => OnBundleLoadFailed?.Invoke(bundleUrl));
                    Log.Error($"替换 bundle 失败: {bundleUrl}, {ex}");
                    throw;
                }
            }
            finally
            {
                // 清理临时目录
                try
                {
                    if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, true);
                }
                catch (Exception ex)
                {
                    Log.Warning($"清理临时目录失败: {tempRoot} -> {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 同步加载本地 bundle 并缓存
        /// </summary>
        /// <param name="localPath"></param>
        /// <returns></returns>
        public AssetBundle LoadBundleFromFile(string localPath)
        {
            if (string.IsNullOrEmpty(localPath)) return null;

            lock (bundleLock)
            {
                if (bundleTable.TryGetValue(localPath, out var exist))
                {
                    return exist;
                }

                // 如果正在异步加载，同步加载无法安全进行（会导致 Unity 报错），必须阻塞或报错
                // 由于是在主线程，阻塞等待异步任务完成会导致死锁（因为异步任务回调通常也在主线程）
                // 这里选择报错并返回 null，或者考虑回退
                if (loadingBundles.ContainsKey(localPath))
                {
                    Log.Error($"无法同步加载 Bundle，因为该 Bundle 正在异步加载中: {localPath}");
                    return null;
                }

                if (!File.Exists(localPath))
                {
                    Log.Error($"Bundle 文件不存在: {localPath}");
                    return null;
                }

                var ab = AssetBundle.LoadFromFile(localPath);
                if (ab == null)
                {
                    Log.Error($"加载 AssetBundle 失败: {localPath}");
                    return null;
                }

                bundleTable[localPath] = ab;
                return ab;
            }
        }

        /// <summary>
        /// 异步加载本地 bundle 并缓存
        /// </summary>
        /// <param name="localPath"></param>
        /// <returns></returns>
        public Task<AssetBundle> LoadBundleFromFileAsync(string localPath)
        {
            if (string.IsNullOrEmpty(localPath)) return Task.FromResult<AssetBundle>(null);

            Task<AssetBundle> task;
            lock (bundleLock)
            {
                if (bundleTable.TryGetValue(localPath, out var exist))
                {
                    return Task.FromResult(exist);
                }

                // 检查是否已经在加载中
                if (loadingBundles.TryGetValue(localPath, out var loadingTask))
                {
                    return loadingTask;
                }

                // 创建新的加载任务并缓存
                task = InternalLoadBundleFromFileAsync(localPath);
                loadingBundles[localPath] = task;
            }

            return task;
        }

        /// <summary>
        /// 实际的异步加载实现
        /// </summary>
        /// <param name="localPath"></param>
        /// <returns></returns>
        private async Task<AssetBundle> InternalLoadBundleFromFileAsync(string localPath)
        {
            try
            {
                if (!File.Exists(localPath))
                {
                    Log.Error($"Bundle 文件不存在: {localPath}");
                    return null;
                }

                AssetBundleCreateRequest req = null;
                // 修复：必须在主线程发起异步加载请求
                await RunOnMainThread(() => req = AssetBundle.LoadFromFileAsync(localPath));

                if (req == null)
                {
                    Log.Error($"调用 AssetBundle.LoadFromFileAsync 失败 (null request): {localPath}");
                    return null;
                }

                await AwaitAsyncOperation(req);

                // 在主线程访问 Unity 的 AssetBundle 对象
                var ab = await RunOnMainThread(() => req.assetBundle);
                if (ab == null)
                {
                    Log.Error($"异步加载 AssetBundle 失败: {localPath}");
                    return null;
                }

                lock (bundleLock)
                {
                    // 二次检查，确保没有由于并发逻辑导致的重复 (虽然 loadingBundles 应该已经阻止了)
                    if (!bundleTable.ContainsKey(localPath))
                    {
                        bundleTable[localPath] = ab;
                    }
                    else
                    {
                        // 理论上不应该发生，但如果发生，卸载刚才加载的副本以防内存泄漏
                        if (bundleTable[localPath] != ab)
                        {
                            ab.Unload(true);
                            ab = bundleTable[localPath];
                        }
                    }
                }
                return ab;
            }
            catch (Exception ex)
            {
                Log.Error($"InternalLoadBundleFromFileAsync 异常: {localPath}, {ex}");
                return null;
            }
            finally
            {
                lock (bundleLock)
                {
                    loadingBundles.Remove(localPath);
                }
            }
        }
        #endregion

        #region 资源加载 API
        /// <summary>
        /// 首选从 bundle 加载（支持远端url或本地路径），失败则回退到 Resources
        /// bundlePath 可为本地路径或远端 url
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="bundlePathOrUrl"></param>
        /// <param name="assetName"></param>
        /// <param name="isCache"></param>
        /// <returns></returns>
        public async Task<T> LoadFromBundleAsync<T>(string bundlePathOrUrl, string assetName, bool isCache = true) where T : Object
        {
            string bundleKey;
            try
            {
                // 下载（如果需要）并得到本地路径
                string localPath = await DownloadBundleIfNeeded(bundlePathOrUrl);
                bundleKey = localPath;
                var ab = await LoadBundleFromFileAsync(localPath);
                if (ab == null)
                {
                    Log.Warning($"Bundle 加载失败，尝试回退到 Resources: {bundlePathOrUrl}");
                    return ResourcesSystem.Instance.Load<T>(assetName, isCache);
                }

                string assetKey = MakeAssetKey(bundleKey, assetName);
                lock (assetLock)
                {
                    if (assetTable.TryGetValue(assetKey, out var cached) && cached != null)
                    {
                        IncrementRefCount(assetKey);
                        return cached as T;
                    }
                }

                // 在主线程调用 LoadAssetAsync
                AssetBundleRequest req = null;
                await RunOnMainThread(() => { req = ab.LoadAssetAsync<T>(assetName); });
                await AwaitAsyncOperation(req);

                // 在主线程访问已加载的资源
                var asset = await RunOnMainThread(() => req.asset as T);
                if (asset == null)
                {
                    Log.Error($"Bundle 中未找到资源: {assetName} in {bundlePathOrUrl}");
                    return null;
                }

                if (isCache)
                {
                    lock (assetLock)
                    {
                        // 二次检查：在异步加载期间可能已被其他请求加载并缓存
                        if (assetTable.TryGetValue(assetKey, out var cached) && cached != null)
                        {
                            // 如果已存在，使用已缓存的（通常也是同一个对象引用），并增加引用计数
                            IncrementRefCount(assetKey);
                            // 这里返回缓存的对象可能更安全，虽然 req.asset 应该是同一个
                            return cached as T;
                        }

                        assetTable[assetKey] = asset;
                        assetRefCount[assetKey] = 1;
                    }
                }

                return asset;
            }
            catch (Exception ex)
            {
                Log.Error($"LoadFromBundleAsync 错误: {ex.Message}");
                return ResourcesSystem.Instance.Load<T>(assetName, isCache);
            }
        }

        /// <summary>
        /// 同步加载：仅支持本地 bundle 路径或已存在的已下载 bundle 文件
        /// 若 bundlePath 是远端 URL，会尝试解析本地已有文件，否则失败
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="bundlePathOrLocalPath"></param>
        /// <param name="assetName"></param>
        /// <param name="isCache"></param>
        /// <returns></returns>
        public T LoadFromBundle<T>(string bundlePathOrLocalPath, string assetName, bool isCache = true) where T : Object
        {
            string localPath = bundlePathOrLocalPath;
            // 如果是 URL，尝试对应的本地保存位置
            if (bundlePathOrLocalPath.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                bundlePathOrLocalPath.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                string fileName = PathUtil.GetFileNameByUrl(bundlePathOrLocalPath);
                string saveDir = PathUtil.GetLocalPath(DownloadType.PersistentAssets);
                localPath = Path.Combine(saveDir, fileName);
            }

            if (!File.Exists(localPath))
            {
                Log.Warning($"本地 bundle 未找到，尝试使用 Resources 加载: {bundlePathOrLocalPath}");
                return ResourcesSystem.Instance.Load<T>(assetName, isCache);
            }

            var ab = LoadBundleFromFile(localPath);
            if (ab == null)
            {
                Log.Warning("AssetBundle 加载失败，回退 Resources");
                return ResourcesSystem.Instance.Load<T>(assetName, isCache);
            }

            string assetKey = MakeAssetKey(localPath, assetName);
            lock (assetLock)
            {
                if (assetTable.TryGetValue(assetKey, out var cached) && cached != null)
                {
                    IncrementRefCount(assetKey);
                    return cached as T;
                }
            }

            // 同步加载：假设调用者在主线程。如果不是，可能会失败。
            var asset = ab.LoadAsset<T>(assetName);
            if (asset == null)
            {
                Log.Error($"AssetBundle 中未找到资源 {assetName} in {localPath}");
                return null;
            }

            if (isCache)
            {
                lock (assetLock)
                {
                    // 二次检查
                    if (assetTable.TryGetValue(assetKey, out var cached) && cached != null)
                    {
                        IncrementRefCount(assetKey);
                        return cached as T;
                    }

                    assetTable[assetKey] = asset;
                    assetRefCount[assetKey] = 1;
                }
            }

            return asset;
        }

        /// <summary>
        /// 加载 Resources（直接委托到 ResourcesSystem）
        /// </summary>
        public T LoadFromResources<T>(string path, bool isCache = true) where T : Object
        {
            return ResourcesSystem.Instance.Load<T>(path, isCache);
        }
        #endregion

        #region 卸载/引用计数
        /// <summary>
        /// 增加引用计数
        /// </summary>
        /// <param name="key"></param>
        public void IncrementRefCount(string key)
        {
            lock (assetLock)
            {
                if (assetRefCount.TryGetValue(key, out var count))
                {
                    assetRefCount[key] = count + 1;
                }
                else
                {
                    assetRefCount[key] = 1;
                }
            }
        }
        /// <summary>
        /// 减少引用计数，若计数 <= 0 则销毁资源
        /// </summary>
        /// <param name="key"></param>
        public void DecrementRefCount(string key)
        {
            lock (assetLock)
            {
                if (assetRefCount.TryGetValue(key, out var count))
                {
                    count--;
                    if (count <= 0)
                    {
                        assetRefCount.Remove(key);
                        if (assetTable.TryGetValue(key, out var obj))
                        {
                            try { UnityMainThreadDispatcher.RunOnMainThread(() => Object.Destroy(obj)); } catch { }
                            assetTable.Remove(key);
                        }
                    }
                    else
                    {
                        assetRefCount[key] = count;
                    }
                }
            }
        }

        /// <summary>
        /// 卸载 bundle（可选是否同时卸载已加载对象, 可选择强制卸载忽略引用计数）
        /// </summary>
        /// <param name="bundleLocalPath"></param>
        /// <param name="unloadAllLoadedObjects"></param>
        /// <param name="force"></param>
        /// <param name="skipDestroyObjects"></param>
        public void UnloadBundle(string bundleLocalPath, bool unloadAllLoadedObjects = false, bool force = false, bool skipDestroyObjects = false)
        {
            if (string.IsNullOrEmpty(bundleLocalPath)) return;

            AssetBundle ab = null;
            lock (bundleLock)
            {
                if (bundleTable.TryGetValue(bundleLocalPath, out ab))
                {
                    // 如果不是强制并且 caller 请求卸载已加载对象，出于安全性我们不对 AssetBundle 调用带 true 的 unload
                    bool abUnloadAll = unloadAllLoadedObjects && force;
                    try { ab.Unload(abUnloadAll); } catch (Exception ex) { Log.Error(ex.Message); }
                    bundleTable.Remove(bundleLocalPath);
                }
            }

            // 清理相关 assetTable 条目
            var keysToRemove = new List<string>();
            lock (assetLock)
            {
                foreach (var kvp in assetTable)
                {
                    if (kvp.Key.StartsWith(bundleLocalPath + "::", StringComparison.OrdinalIgnoreCase))
                    {
                        var key = kvp.Key;

                        if (force)
                        {
                            // 强制卸载：直接销毁并移除引用计数
                            // skipDestroyObjects 为 true 时仅从表移除引用，不销毁实际对象（用于热更文件替换场景）
                            if (!skipDestroyObjects)
                            {
                                try { UnityMainThreadDispatcher.RunOnMainThread(() => Object.Destroy(kvp.Value)); } catch { }
                            }
                            keysToRemove.Add(key);
                        }
                        else
                        {
                            // 安全卸载：尊重引用计数，只有当引用计数 <= 0 或不存在时才销毁
                            if (!assetRefCount.TryGetValue(key, out var count) || count <= 0)
                            {
                                try { UnityMainThreadDispatcher.RunOnMainThread(() => Object.Destroy(kvp.Value)); } catch { }
                                keysToRemove.Add(key);
                            }
                            else
                            {
                                // 仍有外部引用，保留该资源并记录提示
                                Log.Warning($"UnloadBundle: 资源仍被引用，跳过销毁: {key} (refCount={count})");
                            }
                        }
                    }
                }

                foreach (var k in keysToRemove)
                {
                    assetTable.Remove(k);
                    assetRefCount.Remove(k);
                }
            }

            // 如果存在依赖缓存，移除与该 bundle 相关的缓存项（这里只移除完全匹配的 bundle 名称键）
            lock (dependencyLock)
            {
                if (dependencyCache.ContainsKey(bundleLocalPath))
                {
                    dependencyCache.Remove(bundleLocalPath);
                }
            }
        }
        #endregion

        /// <summary>
        /// 更新指定 bundle（根据 manifest 检查版本并下载）
        /// </summary>
        /// <param name="bundleName"></param>
        /// <returns></returns>
        public async Task<bool> UpdateBundle(string bundleName)
        {
            if (versionManager.Manifest == null)
            {
                Log.Error("UpdateBundle: Manifest 未加载");
                return false;
            }

            var info = versionManager.Manifest.GetBundle(bundleName);
            if (info == null)
            {
                Log.Error($"UpdateBundle: 未找到 bundle 信息: {bundleName}");
                return false;
            }

            try
            {
                // DownloadBundleIfNeeded 会处理版本检查、下载、和文件覆盖
                var newLocal = await DownloadBundleIfNeeded(info.url);

                // 如果 DownloadBundleIfNeeded 返回有效路径，说明 Bundle 存在（可能是刚下载的，也可能是旧的）
                if (!string.IsNullOrEmpty(newLocal))
                {
                    Log.Info($"UpdateBundle: bundle 检查完成: {bundleName}");
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                Log.Error($"UpdateBundle 错误: {ex}");
                return false;
            }
        }

        /// <summary>
        /// 加载指定 bundle 及其所有依赖项
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="bundleName"></param>
        /// <param name="assetName"></param>
        /// <param name="isCache"></param>
        /// <returns></returns>
        public async Task<T> LoadWithDependenciesAsync<T>(string bundleName, string assetName, bool isCache = true) where T : Object
        {
            var info = versionManager.Manifest?.GetBundle(bundleName);
            if (info == null)
            {
                // 如果找不到 manifest 信息，尝试直接按 URL 或本地加载
                return await LoadFromBundleAsync<T>(bundleName, assetName, isCache);
            }

            // 修复：获取主 Bundle 的本地路径，以统一使用路径作为 Key（与 UnloadBundle 保持一致）
            string mainUrlOrName = !string.IsNullOrEmpty(info.url) ? info.url : info.fileName;
            string mainLocalPath = await DownloadBundleIfNeeded(mainUrlOrName);
            if (string.IsNullOrEmpty(mainLocalPath))
            {
                Log.Error($"LoadWithDependenciesAsync: 无法定位主 bundle: {bundleName}");
                return null;
            }

            // 先检查缓存 (使用 mainLocalPath 作为 Key)
            List<string> cachedDeps = null;
            lock (dependencyLock)
            {
                if (dependencyCache.TryGetValue(mainLocalPath, out var tmp) && tmp != null)
                    cachedDeps = new List<string>(tmp);
            }

            if (cachedDeps != null)
            {
                // 确保依赖已经下载并加载
                foreach (var depLocal in cachedDeps)
                {
                    if (File.Exists(depLocal))
                    {
                        // 依赖通常无需加载其中的 Asset，只需加载 bundle 到内存即可
                        await LoadBundleFromFileAsync(depLocal);
                    }
                }
            }
            else
            {
                // 递归收集所有依赖（防止 Manifest 仅提供直接依赖导致遗漏）
                var allDependencies = new HashSet<string>();
                CollectDependenciesRecursive(bundleName, allDependencies);

                var resolvedDeps = new List<string>();

                foreach (var dep in allDependencies)
                {
                    var depInfo = versionManager.Manifest.GetBundle(dep);
                    // 依赖可能是 bundleName，也可能是 URL
                    string depPath = depInfo != null && !string.IsNullOrEmpty(depInfo.url) ? depInfo.url : dep;
                    try
                    {
                        var local = await DownloadBundleIfNeeded(depPath);
                        if (!string.IsNullOrEmpty(local))
                        {
                            resolvedDeps.Add(local);
                            await LoadBundleFromFileAsync(local);
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Warning($"依赖下载失败: {dep} -> {ex.Message}");
                        // 依赖失败可能导致主资源加载异常，记录错误
                        UnityMainThreadDispatcher.RunOnMainThread(() => OnBundleLoadFailed?.Invoke(depPath));
                    }
                }

                lock (dependencyLock)
                {
                    dependencyCache[mainLocalPath] = resolvedDeps;
                }
            }

            // 主 bundle (已经下载过了，直接加载)
            return await LoadFromBundleAsync<T>(mainLocalPath, assetName, isCache);
        }

        /// <summary>
        /// 递归收集所有依赖项
        /// </summary>
        /// <param name="bundleName"></param>
        /// <param name="visited"></param>
        private void CollectDependenciesRecursive(string bundleName, HashSet<string> visited)
        {
            var info = versionManager.Manifest?.GetBundle(bundleName);
            if (info == null || info.dependencies == null) return;

            foreach (var dep in info.dependencies)
            {
                if (!visited.Contains(dep))
                {
                    visited.Add(dep);
                    CollectDependenciesRecursive(dep, visited);
                }
            }
        }
    }
}
