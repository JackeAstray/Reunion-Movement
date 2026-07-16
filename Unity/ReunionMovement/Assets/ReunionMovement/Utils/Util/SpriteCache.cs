using System.Collections.Generic;
using UnityEngine;

namespace ReunionMovement.Common.Util
{
    /// <summary>
    /// Sprite 缓存 —— 避免重复调用 Sprite.Create() 产生 GC。
    /// 内置 LRU 淘汰策略，防止内存无限增长。
    ///
    /// 原理：Sprite.texture 是只读的（创建时确定），
    /// 无法像 GameObject 那样替换内部引用后复用。
    /// 因此"池化"策略是：以 URL/Key 为维度缓存 Sprite，
    /// 同一张图只 Create 一次，后续直接返回缓存。
    /// </summary>
    public class SpriteCache
    {
        private readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();
        private readonly LinkedList<string> lruOrder = new LinkedList<string>();

        /// <summary>缓存最大容量（超出时淘汰最久未使用的条目）</summary>
        public int MaxCapacity { get; set; } = 100;

        /// <summary>缓存数量</summary>
        public int Count => cache.Count;

        /// <summary>
        /// 获取或创建 Sprite（同一 key 只 Create 一次）。
        /// </summary>
        /// <param name="key">缓存键（建议用 URL 或文件路径）</param>
        /// <param name="texture">源纹理</param>
        /// <param name="pivot">锚点</param>
        /// <param name="pixelsPerUnit">像素单位比</param>
        /// <returns>缓存的或新创建的 Sprite</returns>
        public Sprite GetOrCreate(string key, Texture2D texture, Vector2? pivot = null, float pixelsPerUnit = 100f)
        {
            if (string.IsNullOrEmpty(key) || texture == null) return null;

            if (cache.TryGetValue(key, out Sprite cached) && cached != null)
            {
                // LRU: 移动到链表尾部表示最近使用
                lruOrder.Remove(key);
                lruOrder.AddLast(key);
                return cached;
            }

            // LRU 淘汰：超出容量时移除最久未使用的条目
            while (cache.Count >= MaxCapacity && lruOrder.First != null)
            {
                string oldestKey = lruOrder.First.Value;
                lruOrder.RemoveFirst();
                if (cache.TryGetValue(oldestKey, out Sprite oldSprite))
                {
                    if (oldSprite != null) UnityEngine.Object.Destroy(oldSprite);
                    cache.Remove(oldestKey);
                }
            }

            Vector2 p = pivot ?? TextureToSpriteUtil.PivotCenter;
            Rect rect = new Rect(0, 0, texture.width, texture.height);
            Sprite sprite = Sprite.Create(texture, rect, p, pixelsPerUnit);
            if (sprite != null)
            {
                sprite.name = texture.name ?? key;
                cache[key] = sprite;
                lruOrder.AddLast(key);
            }
            return sprite;
        }

        /// <summary>
        /// 尝试从缓存获取 Sprite。
        /// </summary>
        public bool TryGet(string key, out Sprite sprite)
        {
            return cache.TryGetValue(key, out sprite) && sprite != null;
        }

        /// <summary>
        /// 移除指定缓存。
        /// </summary>
        public void Remove(string key)
        {
            lruOrder.Remove(key);
            if (cache.TryGetValue(key, out Sprite sprite))
            {
                if (sprite != null) UnityEngine.Object.Destroy(sprite);
                cache.Remove(key);
            }
        }

        /// <summary>
        /// 清空所有缓存并销毁 Sprite。
        /// </summary>
        public void Clear()
        {
            foreach (var sprite in cache.Values)
            {
                if (sprite != null)
                    UnityEngine.Object.Destroy(sprite);
            }
            cache.Clear();
            lruOrder.Clear();
        }
    }
}
