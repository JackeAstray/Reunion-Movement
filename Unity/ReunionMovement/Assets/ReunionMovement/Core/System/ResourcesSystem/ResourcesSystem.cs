using ReunionMovement.Common;
using ReunionMovement.Core.Base;
using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Events;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.U2D;
using Object = UnityEngine.Object;

namespace ReunionMovement.Core.Resources
{
    /// <summary>
    /// 资源系统
    /// </summary>
    public class ResourcesSystem : ICustomSystem
    {
        #region 单例与初始化
        private static readonly Lazy<ResourcesSystem> instance = new(() => new ResourcesSystem());
        public static ResourcesSystem Instance => instance.Value;

        public bool isInited { get; private set; }

        private double initProgress = 0;
        public double InitProgress { get { return initProgress; } }
        #endregion

        private Dictionary<string, Object> resourceTable = new Dictionary<string, Object>();
        private Dictionary<string, int> resourceRefCount = new Dictionary<string, int>();
        // 图集缓存，避免每次 GetAtlasSprite 重复 Resources.Load
        private Dictionary<string, SpriteAtlas> atlasCache = new Dictionary<string, SpriteAtlas>();

        public UniTask Init()
        {
            initProgress = 100;
            isInited = true;
            Log.Debug("ResourcesSystem 初始化完成");
            return UniTask.CompletedTask;
        }

        public void Update(float logicTime, float realTime)
        {

        }

        public void Clear()
        {
            Log.Debug("ResourcesSystem 清除数据");
            resourceTable.Clear();
            resourceRefCount.Clear();
            atlasCache.Clear();
            isInited = false;
        }

        #region 加载
        /// <summary>
        /// 同步加载Resources下资源
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="assetPath">路径</param>
        /// <param name="isCache">是否缓存</param>
        /// <returns></returns>
        public T Load<T>(string assetPath, bool isCache = true) where T : Object
        {
            if (resourceTable.TryGetValue(assetPath, out var asset))
            {
                // 增加引用计数
                IncrementRefCount(assetPath);
                return asset as T;
            }

            var assets = UnityEngine.Resources.Load<T>(assetPath);
            if (assets == null)
            {
                Log.Error("资源没有找到,路径为:{0}", assetPath);
                return null;
            }

            if (isCache)
            {
                // TryGetValue 已在上方判定为 false，此处直接添加
                resourceTable[assetPath] = assets;
                resourceRefCount[assetPath] = 1;
            }
            return assets;
        }

        /// <summary>
        /// 异步加载Resources下资源
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="assetPath"></param>
        /// <param name="callback"></param>
        /// <returns></returns>
        public async UniTask<T> LoadAsync<T>(string assetPath, bool isCache = true, UnityAction<T> callback = null) where T : Object
        {
            if (resourceTable.TryGetValue(assetPath, out var cachedAsset))
            {
                // 增加引用计数
                IncrementRefCount(assetPath);
                callback?.Invoke(cachedAsset as T);
                return cachedAsset as T;
            }

            var assets = await ResourcesUtil.LoadAsync<T>(assetPath, callback);
            if (assets == null)
            {
                Log.Error("资源没有找到,路径为:{0}", assetPath);
                return null;
            }

            if (isCache)
            {
                if (!resourceTable.ContainsKey(assetPath))
                {
                    resourceTable[assetPath] = assets;
                    resourceRefCount[assetPath] = 1;
                }
                else
                {
                    // 解决并发await时同一个path的重复缓存覆盖导致的引用计数丢失问题
                    IncrementRefCount(assetPath);
                }
            }
            return assets;
        }
        #endregion

        #region 功能
        /// <summary>
        /// 从图集加载精灵（带缓存）
        /// </summary>
        /// <param name="atlasName">图集路径名称</param>
        /// <param name="spriteName">精灵路径名称 </param>
        /// <returns></returns>
        public Sprite GetAtlasSprite(string atlasName, string spriteName)
        {
            if (!atlasCache.TryGetValue(atlasName, out var atlas))
            {
                atlas = UnityEngine.Resources.Load<SpriteAtlas>(atlasName);
                if (atlas != null)
                {
                    atlasCache[atlasName] = atlas;
                }
            }
            if (atlas is null)
            {
                Log.Error("图集：{0}不存在，请检查！", atlasName);
                return null;
            }
            var sprite = atlas.GetSprite(spriteName);
            if (sprite is null)
            {
                Log.Error("{0} 图集中Sprite:{1} 不存在，请检查！", atlasName, spriteName);
            }
            return sprite;
        }

        /// <summary>
        /// 实例化资源
        /// 注意：直接使用 Unity Resources.Load，不经过缓存层，
        /// 避免源资源的引用计数泄漏（实例化出的 GameObject 销毁时无人递减计数）
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="path"></param>
        /// <returns></returns>
        public T InstantiateAsset<T>(string path) where T : Object
        {
            var obj = UnityEngine.Resources.Load<T>(path);
            if (obj == null)
            {
                Log.Error("资源没有找到,路径为:{0}", path);
                return null;
            }
            var go = GameObject.Instantiate(obj);
            if (go == null)
            {
                Log.Error("实例化 {0} 失败!", path);
            }
            return go;
        }

        /// <summary>
        /// 移除单个数据缓存
        /// </summary>
        /// <param name="path"></param>
        public void DeleteAssetCache(string path)
        {
            DecrementRefCount(path);
        }

        /// <summary>
        /// 清除资源缓存
        /// </summary>
        public void ClearAssetsCache()
        {
            foreach (var kvp in resourceTable)
            {
                var obj = kvp.Value;
                if (obj != null && !(obj is GameObject || obj is Component))
                {
                    UnityEngine.Resources.UnloadAsset(obj);
                }
            }
            resourceTable.Clear();
            resourceRefCount.Clear();
        }

        /// <summary>
        /// 增加引用计数
        /// </summary>
        /// <param name="path"></param>
        public void IncrementRefCount(string path)
        {
            if (resourceRefCount.TryGetValue(path, out var count))
            {
                resourceRefCount[path] = count + 1;
            }
            else
            {
                resourceRefCount[path] = 1;
            }
        }

        /// <summary>
        /// 减少引用计数
        /// </summary>
        /// <param name="path"></param>
        public void DecrementRefCount(string path)
        {
            if (resourceRefCount.TryGetValue(path, out var count))
            {
                count--;
                if (count <= 0)
                {
                    resourceRefCount.Remove(path);
                    if (resourceTable.TryGetValue(path, out var obj))
                    {
                        if (!(obj is GameObject || obj is Component))
                        {
                            UnityEngine.Resources.UnloadAsset(obj);
                        }
                        resourceTable.Remove(path);
                    }
                }
                else
                {
                    resourceRefCount[path] = count;
                }
            }
        }

        /// <summary>
        /// 销毁资源
        /// </summary>
        /// <param name="path"></param>
        /// <param name="obj"></param>
        public void DestroyAsset(string path, Object obj)
        {
            if (obj != null)
            {
                Object.Destroy(obj);
                DecrementRefCount(path);
            }
        }
        #endregion

        #region Addressables 集成（推荐用于 WebGL 远程更新和大型项目资源管理）
        /// <summary>
        /// 通过 Addressables 异步加载资源（推荐方式，支持远程更新和内存跟踪）。
        /// 加载的资源不经过 ResourcesSystem 的引用计数缓存（Addressables 自行管理生命周期）。
        /// </summary>
        /// <typeparam name="T">资源类型</typeparam>
        /// <param name="key">Addressables key（路径或 Label）</param>
        /// <returns>加载的资源，失败返回 null</returns>
        public async UniTask<T> LoadAddressableAsync<T>(string key) where T : Object
        {
            try
            {
                var handle = Addressables.LoadAssetAsync<T>(key);
                await handle.Task;
                if (handle.Status == AsyncOperationStatus.Succeeded)
                {
                    return handle.Result;
                }
                Log.Error("Addressables 加载失败: {0}, Status: {1}", key, handle.Status);
                return null;
            }
            catch (Exception ex)
            {
                Log.Error("Addressables 加载异常: {0}, {1}", key, ex.Message);
                return null;
            }
        }

        /// <summary>
        /// 通过 Addressables 异步实例化 GameObject（自动处理引用计数）。
        /// </summary>
        /// <param name="key">Addressables key</param>
        /// <param name="parent">父 Transform（可选）</param>
        /// <returns>实例化的 GameObject，失败返回 null</returns>
        public async UniTask<GameObject> InstantiateAddressableAsync(string key, Transform parent = null)
        {
            try
            {
                var handle = Addressables.InstantiateAsync(key, parent);
                await handle.Task;
                if (handle.Status == AsyncOperationStatus.Succeeded)
                {
                    return handle.Result;
                }
                Log.Error("Addressables 实例化失败: {0}, Status: {1}", key, handle.Status);
                return null;
            }
            catch (Exception ex)
            {
                Log.Error("Addressables 实例化异常: {0}, {1}", key, ex.Message);
                return null;
            }
        }

        /// <summary>
        /// 释放 Addressables 实例化的 GameObject。
        /// </summary>
        public void ReleaseAddressableInstance(GameObject instance)
        {
            if (instance != null)
            {
                Addressables.ReleaseInstance(instance);
            }
        }

        /// <summary>
        /// 释放 Addressables 加载的资源。
        /// </summary>
        public void ReleaseAddressableAsset<T>(T asset) where T : Object
        {
            if (asset != null)
            {
                Addressables.Release(asset);
            }
        }
        #endregion
    }

    /// <summary>
    /// 资源请求等待器
    /// </summary>
    public class ResourceRequestAwaiter : INotifyCompletion
    {
        private Action continuation;
        private readonly ResourceRequest resourceRequest;
        public bool IsCompleted => resourceRequest.isDone;

        public ResourceRequestAwaiter(ResourceRequest resourceRequest)
        {
            this.resourceRequest = resourceRequest;
            this.resourceRequest.completed += Accomplish;
        }

        public void OnCompleted(Action continuation)
        {
            this.continuation = continuation;
        }

        private void Accomplish(AsyncOperation asyncOperation)
        {
            continuation?.Invoke();
            // 移除委托，避免内存泄漏
            resourceRequest.completed -= Accomplish;
        }

        public void GetResult() { /* 无需返回值 */ }
    }

    /// <summary>
    /// 资源扩展
    /// </summary>
    public static class ResourcesUtil
    {
        public static ResourceRequestAwaiter GetAwaiter(this ResourceRequest request) => new ResourceRequestAwaiter(request);

        public static async UniTask<T> LoadAsync<T>(string assetPath, UnityAction<T> callback = null) where T : Object
        {
            try
            {
                var request = UnityEngine.Resources.LoadAsync<T>(assetPath);
                await request;
                var asset = request.asset as T;
                callback?.Invoke(asset);
                return asset;
            }
            catch (Exception ex)
            {
                Log.Error("异步加载资源失败: {0}, {1}", assetPath, ex.Message);
                callback?.Invoke(null);
                return null;
            }
        }
    }
}
