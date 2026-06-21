using UnityEngine;

namespace ReunionMovement.Common.Util
{
    /// <summary>
    /// Texture2D 与 Sprite 互转工具（零 GC 设计）
    /// Rect 和 Vector2 是 struct，new 不会产生堆分配；
    /// 真正的 GC 仅来自 Sprite.Create() 返回的 Sprite 对象。
    /// 若需彻底零 GC，请使用方法三的 Sprite 池。
    /// </summary>
    public static class TextureToSpriteUtil
    {
        // ============ 预缓存常用 pivot，避免每次写 new Vector2 ============
        public static readonly Vector2 PivotCenter    = new Vector2(0.5f, 0.5f);
        public static readonly Vector2 PivotTopLeft   = new Vector2(0f, 1f);
        public static readonly Vector2 PivotTopCenter = new Vector2(0.5f, 1f);
        public static readonly Vector2 PivotTopRight  = new Vector2(1f, 1f);
        public static readonly Vector2 PivotBottomLeft  = new Vector2(0f, 0f);
        public static readonly Vector2 PivotBottomCenter = new Vector2(0.5f, 0f);
        public static readonly Vector2 PivotBottomRight  = new Vector2(1f, 0f);
        public static readonly Vector2 PivotMiddleLeft   = new Vector2(0f, 0.5f);
        public static readonly Vector2 PivotMiddleRight  = new Vector2(1f, 0.5f);

        // ============ 基础转换 ============

        /// <summary>
        /// 将 Texture2D 转为 Sprite。
        /// Rect/Vector2 是 struct，无 GC；仅 Sprite 对象本身有分配。
        /// </summary>
        public static Sprite ToSprite(this Texture2D texture, Vector2? pivot = null, float pixelsPerUnit = 100f, uint extrude = 0, SpriteMeshType meshType = SpriteMeshType.Tight)
        {
            if (texture == null) return null;

            Vector2 p = pivot ?? PivotCenter;
            // Rect 是 struct，不会产生 GC
            Rect rect = new Rect(0, 0, texture.width, texture.height);
            Sprite sprite = Sprite.Create(texture, rect, p, pixelsPerUnit, extrude, meshType);
            if (sprite != null)
            {
                sprite.name = texture.name;
            }
            return sprite;
        }

        /// <summary>
        /// 将 Texture2D 转为 Sprite，并传入自定义 Rect（如裁切子区域）。
        /// </summary>
        public static Sprite ToSpriteWithRect(this Texture2D texture, Rect rect, Vector2? pivot = null, float pixelsPerUnit = 100f)
        {
            if (texture == null) return null;
            return Sprite.Create(texture, rect, pivot ?? PivotCenter, pixelsPerUnit);
        }

        // ============ Sprite 缓存（避免重复 Create） ============

        /// <summary>
        /// 通过缓存获取 Sprite：同一 key 只 Create 一次，后续零 GC。
        /// </summary>
        public static Sprite GetCachedSprite(this Texture2D texture, SpriteCache cache, string key, Vector2? pivot = null, float pixelsPerUnit = 100f)
        {
            if (texture == null || cache == null) return null;
            return cache.GetOrCreate(key, texture, pivot ?? PivotCenter, pixelsPerUnit);
        }
    }
}
