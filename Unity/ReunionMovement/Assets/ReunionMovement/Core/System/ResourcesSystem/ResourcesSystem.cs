using ReunionMovement.Common;
using ReunionMovement.Core.Base;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.U2D;
using Object = UnityEngine.Object;

namespace ReunionMovement.Core.Resources
{
    public class ResourcesSystem : ICustommSystem
    {
        #region 单例与初始化
        private static readonly Lazy<ResourcesSystem> instance = new(() => new ResourcesSystem());
        public static ResourcesSystem Instance => instance.Value;
        public bool IsInited { get; private set; }
        private double initProgress = 0;
        public double InitProgress { get { return initProgress; } }
        #endregion

        private Dictionary<string, Object> resourceTable = new Dictionary<string, Object>();
        private Dictionary<string, int> resourceRefCount = new Dictionary<string, int>();

        public async Task Init()
        {
            initProgress = 100;
            IsInited = true;
            Log.Debug("ResourcesSystem 初始化完成");
        }

        public void Update(float logicTime, float realTime)
        {

        }

        public void Clear()
        {
            Log.Debug("ResourcesSystem 清除数据");
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
                Log.Error($"资源没有找到,路径为:{assetPath}");
                return null;
            }

            if (isCache)
            {
                resourceTable[assetPath] = assets;
                // 增加引用计数
                IncrementRefCount(assetPath);
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
        public async Task<T> LoadAsync<T>(string assetPath, bool isCache = true, UnityAction<T> callback = null) where T : Object
        {
            if (resourceTable.TryGetValue(assetPath, out var cachedAsset))
            {
                // 增加引用计数
                IncrementRefCount(assetPath);
                return cachedAsset as T;
            }

            var assets = await ResourcesUtil.LoadAsync<T>(assetPath, callback);
            if (assets == null)
            {
                Log.Error($"资源没有找到,路径为:{assetPath}");
                return null;
            }

            if (isCache)
            {
                resourceTable[assetPath] = assets;
                // 增加引用计数
                IncrementRefCount(assetPath);
            }
            return assets;
        }
        #endregion

        #region 功能
        /// <summary>
        /// 从图集加载精灵
        /// </summary>
        /// <param name="atlasName">图集路径名称</param>
        /// <param name="spriteName">精灵路径名称 </param>
        /// <returns></returns>
        public Sprite GetAtlasSprite(string atlasName, string spriteName)
        {
            var atlas = UnityEngine.Resources.Load<SpriteAtlas>(atlasName);
            if (atlas is null)
            {
                Log.Error($"图集：{atlasName}不存在，请检查！");
                return null;
            }
            var sprite = atlas.GetSprite(spriteName);
            if (sprite is null)
            {
                Log.Error($"{atlasName} 图集中Sprite:{spriteName} 不存在，请检查！");
            }
            return sprite;
        }

        /// <summary>
        /// 实例化资源
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="path"></param>
        /// <returns></returns>
        public T InstantiateAsset<T>(string path) where T : Object
        {
            var obj = Load<T>(path);
            var go = GameObject.Instantiate(obj);
            if (go == null)
            {
                Log.Error($"实例化 {path} 失败!");
            }
            return go;
        }

        /// <summary>
        /// 移除单个数据缓存
        /// </summary>
        /// <param name="path"></param>
        public void DeleteAssetCache(string path)
        {
            if (resourceRefCount.ContainsKey(path))
            {
                resourceRefCount[path]--;
                if (resourceRefCount[path] <= 0)
                {
                    resourceRefCount.Remove(path);
                    if (resourceTable.ContainsKey(path))
                    {
                        Object.Destroy(resourceTable[path]);
                        resourceTable.Remove(path);
                    }
                }
            }
        }

        /// <summary>
        /// 清除资源缓存
        /// </summary>
        public void ClearAssetsCache()
        {
            foreach (var kvp in resourceTable)
            {
                DecrementRefCount(kvp.Key);
                if (!resourceRefCount.ContainsKey(kvp.Key) || resourceRefCount[kvp.Key] <= 0)
                {
                    Object.Destroy(kvp.Value);
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
            if (resourceRefCount.ContainsKey(path))
            {
                resourceRefCount[path]++;
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
            if (resourceRefCount.ContainsKey(path))
            {
                resourceRefCount[path]--;
                if (resourceRefCount[path] <= 0)
                {
                    resourceRefCount.Remove(path);
                    DeleteAssetCache(path);
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
                GameObject.Destroy(obj);
                // 减少引用计数
                DecrementRefCount(path);
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

        public static async Task<T> LoadAsync<T>(string assetPath, UnityAction<T> callback = null) where T : Object
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
                Log.Error($"异步加载资源失败: {assetPath}, {ex.Message}");
                callback?.Invoke(null);
                return null;
            }
        }
    }
}
