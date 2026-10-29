using System.Text;
using UnityEngine;
using UnityEngine.UI;

using Astray.Common;
namespace Astray.Utilities.UI.ImageExtensions
{
    /// <summary>
    /// 图像辅助类（Private helpers）：四边形 / UV / 径向填充的具体实现。
    /// </summary>
    public static partial class ImageHelper
    {
        #region Private helpers

        /// <summary>
        /// 在 VertexHelper 中添加一个用于绘制的四边形（包含顶点数据、UV、额外的 effects UV 等）。
        /// 支持在原始四边形下方先添加一个阴影四边形（用于 shader 的阴影/外扩处理）。
        /// </summary>
        /// <param name="vertexHelper">目标 VertexHelper。</param>
        /// <param name="quadPositions">四边形在本地空间中的四个顶点位置（按顺序）。</param>
        /// <param name="color">顶点颜色。</param>
        /// <param name="quadUVs">对应的纹理 UV（用于采样）。</param>
        /// <param name="size">四边形的像素尺寸（宽, 高）。</param>
        /// <param name="bounds">绘制边界（用于归一化 uv1 计算）。</param>
        /// <param name="appendShadow">是否先附加阴影四边形。</param>
        /// <param name="shadowOffsetLocal">阴影本地偏移量（像素）。</param>
        private static void AddQuad(VertexHelper vertexHelper,
            Vector3[] quadPositions,
            Color32 color,
            Vector3[] quadUVs,
            Vector2 size,
            Vector4 bounds,
            bool appendShadow,
            Vector2 shadowOffsetLocal,
            float shadowScale = 1f,
            float shadowFade = 1f)
        {
            // 插入 UV 以避免边缘出现纹理包裹伪影
            float epsilon = 0.001f;

            // 可选：先为阴影添加四边形（渲染在原始四边形下方）
            if (appendShadow)
            {
                int startIndex2 = vertexHelper.currentVertCount;
                // 使用 tangent.w=1 标记阴影顶点，保持原始顶点颜色，避免黑色 Tint 与阴影标记冲突

                Vector2 shadowCenter = new Vector2((bounds.x + bounds.z) * 0.5f, (bounds.y + bounds.w) * 0.5f);
                float safeShadowScale = Mathf.Max(0.0001f, shadowScale);
                Color32 shadowColor = shadowFade < 0.999f
                    ? new Color32(color.r, color.g, color.b, (byte)(color.a * shadowFade))
                    : color;

                for (int i = 0; i < 4; ++i)
                {
                    Vector2 basePos = quadPositions[i];
                    Vector2 scaledPos = (basePos - shadowCenter) * safeShadowScale + shadowCenter;
                    Vector3 pos = (Vector3)scaledPos + new Vector3(shadowOffsetLocal.x, shadowOffsetLocal.y, 0);

                    float uBase = Mathf.InverseLerp(bounds.x, bounds.z, quadPositions[i].x);
                    float vBase = Mathf.InverseLerp(bounds.y, bounds.w, quadPositions[i].y);

                    float u = Mathf.Lerp(epsilon, 1 - epsilon, uBase);
                    float v = Mathf.Lerp(epsilon, 1 - epsilon, vBase);
                    Vector2 uv1 = new Vector2(u, v);

                    Vector2 sampleTexCoord = quadUVs[i];

                    vertexHelper.AddVert(pos, shadowColor, sampleTexCoord, uv1, size, Vector2.zero,
                        Vector3.zero, new Vector4(0, 0, 0, 1));
                }

                vertexHelper.AddTriangle(startIndex2, startIndex2 + 1, startIndex2 + 2);
                vertexHelper.AddTriangle(startIndex2 + 2, startIndex2 + 3, startIndex2);
            }

            // 添加原始四边形顶点（在阴影之上）
            int startIndex = vertexHelper.currentVertCount;
            for (int i = 0; i < 4; ++i)
            {
                float u = Mathf.InverseLerp(bounds.x, bounds.z, quadPositions[i].x);
                float v = Mathf.InverseLerp(bounds.y, bounds.w, quadPositions[i].y);

                u = Mathf.Lerp(epsilon, 1 - epsilon, u);
                v = Mathf.Lerp(epsilon, 1 - epsilon, v);

                Vector2 uv1 = new Vector2(u, v);

                vertexHelper.AddVert(quadPositions[i], color, quadUVs[i], uv1, size, Vector2.zero,
                    Vector3.zero, Vector4.zero);
            }

            vertexHelper.AddTriangle(startIndex, startIndex + 1, startIndex + 2);
            vertexHelper.AddTriangle(startIndex + 2, startIndex + 3, startIndex);
        }

        /// <summary>
        /// 计算绘制时使用的四边形在目标 Rect 中的位置与 UV 映射（包含 padding 的偏移）。
        /// 返回一个 Vector4 表示绘制矩形在本地空间中的 x, y, z, w（左，下，右，上）。
        /// </summary>
        /// <param name="shouldPreserveAspect">是否保持纵横比。</param>
        /// <param name="activeSprite">当前使用的精灵（可为 null）。</param>
        /// <param name="canvas">用于像素调整的 Canvas（可为 null）。</param>
        /// <param name="rectTransform">目标 RectTransform。</param>
        /// <returns>返回绘制矩形的边界（x, y, z, w）。</returns>
        private static Vector4 GetDrawingDimensions(bool shouldPreserveAspect,
            Sprite activeSprite,
            Canvas canvas,
            RectTransform rectTransform)
        {
            var padding = activeSprite == null ? Vector4.zero : UnityEngine.Sprites.DataUtility.GetPadding(activeSprite);
            var size = activeSprite == null
                ? new Vector2(rectTransform.rect.width, rectTransform.rect.height)
                : new Vector2(activeSprite.rect.width, activeSprite.rect.height);

            if (size.x <= 0) size.x = 1;
            if (size.y <= 0) size.y = 1;
            Rect r = GetPixelAdjustedRect(canvas, rectTransform);

            int spriteW = Mathf.RoundToInt(size.x);
            int spriteH = Mathf.RoundToInt(size.y);

            Vector4 v = new Vector4(
                padding.x / spriteW,
                padding.y / spriteH,
                (spriteW - padding.z) / spriteW,
                (spriteH - padding.w) / spriteH);

            if (shouldPreserveAspect && size.sqrMagnitude > 0.0f)
            {
                PreserveSpriteAspectRatio(ref r, rectTransform, size);
            }

            v = new Vector4(
                r.x + r.width * v.x,
                r.y + r.height * v.y,
                r.x + r.width * v.z,
                r.y + r.height * v.w
            );

            return v;
        }

        /// <summary>
        /// 根据 Canvas 与 RectTransform 返回像素调整后的矩形（当 Canvas 为 WorldSpace、scaleFactor 为 0 或 pixelPerfect 为 false 时返回 rectTransform.rect）。
        /// </summary>
        /// <param name="canvas">Canvas（可为 null）。</param>
        /// <param name="rectTransform">目标 RectTransform。</param>
        /// <returns>调整后的矩形。</returns>
        private static Rect GetPixelAdjustedRect(Canvas canvas, RectTransform rectTransform)
        {
            if (!canvas || canvas.renderMode == RenderMode.WorldSpace || canvas.scaleFactor == 0.0f ||
                !canvas.pixelPerfect)
            {
                return rectTransform.rect;
            }

            return RectTransformUtility.PixelAdjustRect(rectTransform, canvas);
        }

        /// <summary>
        /// 对给定的顶点与 UV 数组执行径向切割，支持旋转角与反转选项。
        /// 返回是否需要绘制（fill 大于最小阈值并且不被反转完全裁剪时返回 true）。
        /// </summary>
        /// <param name="xy">顶点坐标数组（长度为 4）。</param>
        /// <param name="uv">对应的 UV 坐标数组（长度为 4）。</param>
        /// <param name="fill">填充比例（0..1）。</param>
        /// <param name="invert">是否反转填充方向。</param>
        /// <param name="corner">起始角索引（0..3）。</param>
        /// <returns>是否需要绘制。</returns>
        private static bool RadialCut(Vector3[] xy, Vector3[] uv, float fill, bool invert, int corner)
        {
            if (fill < 0.001f) return false;

            if ((corner & 1) == 1) invert = !invert;

            if (!invert && fill > 0.999f) return true;

            float angle = Mathf.Clamp01(fill);
            if (invert) angle = 1f - angle;
            angle *= 90f * Mathf.Deg2Rad;

            float cos = Mathf.Cos(angle);
            float sin = Mathf.Sin(angle);

            RadialCut(xy, cos, sin, invert, corner);
            RadialCut(uv, cos, sin, invert, corner);
            return true;
        }

        /// <summary>
        /// 内部径向切割实现：根据 cos/sin 值修改传入数组的顶点以完成切割。
        /// </summary>
        /// <param name="xy">顶点或 UV 数组（长度 4）。</param>
        /// <param name="cos">角度的余弦。</param>
        /// <param name="sin">角度的正弦。</param>
        /// <param name="invert">是否反转。</param>
        /// <param name="corner">起始角索引。</param>
        private static void RadialCut(Vector3[] xy, float cos, float sin, bool invert, int corner)
        {
            int i0 = corner;
            int i1 = ((corner + 1) % 4);
            int i2 = ((corner + 2) % 4);
            int i3 = ((corner + 3) % 4);

            if ((corner & 1) == 1)
            {
                if (sin > cos)
                {
                    cos /= sin;
                    sin = 1f;

                    if (invert)
                    {
                        xy[i1].x = Mathf.Lerp(xy[i0].x, xy[i2].x, cos);
                        xy[i2].x = xy[i1].x;
                    }
                }
                else if (cos > sin)
                {
                    sin /= cos;
                    cos = 1f;

                    if (!invert)
                    {
                        xy[i2].y = Mathf.Lerp(xy[i0].y, xy[i2].y, sin);
                        xy[i3].y = xy[i2].y;
                    }
                }
                else
                {
                    cos = 1f;
                    sin = 1f;
                }

                if (!invert) xy[i3].x = Mathf.Lerp(xy[i0].x, xy[i2].x, cos);
                else xy[i1].y = Mathf.Lerp(xy[i0].y, xy[i2].y, sin);
            }
            else
            {
                if (cos > sin)
                {
                    sin /= cos;
                    cos = 1f;

                    if (!invert)
                    {
                        xy[i1].y = Mathf.Lerp(xy[i0].y, xy[i2].y, sin);
                        xy[i2].y = xy[i1].y;
                    }
                }
                else if (sin > cos)
                {
                    cos /= sin;
                    sin = 1f;

                    if (invert)
                    {
                        xy[i2].x = Mathf.Lerp(xy[i0].x, xy[i2].x, cos);
                        xy[i3].x = xy[i2].x;
                    }
                }
                else
                {
                    cos = 1f;
                    sin = 1f;
                }

                if (invert) xy[i3].y = Mathf.Lerp(xy[i0].y, xy[i2].y, sin);
                else xy[i1].x = Mathf.Lerp(xy[i0].x, xy[i2].x, cos);
            }
        }
        #endregion
    }
}
