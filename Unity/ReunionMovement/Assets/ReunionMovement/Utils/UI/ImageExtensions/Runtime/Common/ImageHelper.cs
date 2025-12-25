using System.Text;

namespace UnityEngine.UI.ImageExtensions
{
    /// <summary>
    /// 图像辅助类，提供用于生成精灵网格（包括填充与简单绘制）的实用函数。
    /// 这些方法用于构建 Unity UI 的 VertexHelper 顶点数据（支持可选阴影四边形、径向填充等）。
    /// </summary>
    public static class ImageHelper
    {
        #region Public API

        /// <summary>
        /// 生成一个简单的精灵网格（不带阴影），并填充到指定的 <see cref="VertexHelper"/> 中。
        /// </summary>
        /// <param name="vh">目标 VertexHelper，用于接收顶点与三角形。</param>
        /// <param name="preserveAspect">是否保持精灵纵横比。</param>
        /// <param name="canvas">用于像素调整的 Canvas（可为 null）。</param>
        /// <param name="rectTransform">目标 RectTransform，用于确定绘制区域。</param>
        /// <param name="activeSprite">用于绘制的精灵（可为 null，表示使用默认 UV）。</param>
        /// <param name="color">顶点颜色（Color32）。</param>
        /// <param name="falloffDistance">（保留参数）衰减距离（当前实现未直接使用此参数，但保留签名以兼容性）。
        /// </param>
        public static void GenerateSimpleSprite(VertexHelper vh, bool preserveAspect, Canvas canvas,
            RectTransform rectTransform, Sprite activeSprite, Color32 color, float falloffDistance)
        {
            GenerateSimpleSprite(vh, preserveAspect, canvas, rectTransform, activeSprite, color, falloffDistance, false, Vector2.zero);
        }

        /// <summary>
        /// 生成一个简单的精灵网格，并可选地在下方附加一个阴影四边形。
        /// 阴影四边形会以黑色（但保留原始透明度）添加在原始四边形之前，以便在后续 shader 中进行形状／SDF 效果处理。
        /// </summary>
        /// <param name="vh">目标 VertexHelper。</param>
        /// <param name="preserveAspect">是否保持精灵纵横比。</param>
        /// <param name="canvas">用于像素调整的 Canvas（可为 null）。</param>
        /// <param name="rectTransform">目标 RectTransform。</param>
        /// <param name="activeSprite">精灵（可为 null）。</param>
        /// <param name="color">顶点颜色。</param>
        /// <param name="falloffDistance">衰减距离（保留用）。</param>
        /// <param name="appendShadow">是否附加阴影四边形。</param>
        /// <param name="shadowOffsetLocal">阴影的本地偏移量（以像素为单位，局部空间）。</param>
        public static void GenerateSimpleSprite(VertexHelper vh, bool preserveAspect, Canvas canvas,
            RectTransform rectTransform, Sprite activeSprite, Color32 color, float falloffDistance, bool appendShadow, Vector2 shadowOffsetLocal)
        {
            vh.Clear();

            Vector4 v = GetDrawingDimensions(preserveAspect, activeSprite, canvas, rectTransform);
            Vector4 uv = (activeSprite != null)
                ? Sprites.DataUtility.GetOuterUV(activeSprite)
                : new Vector4(0, 0, 1, 1);

            Vector2 size = new Vector2(v.z - v.x, v.w - v.y);
            Vector4 fullBounds = v;

            Vector3[] sxy = new Vector3[4];
            sxy[0] = new Vector2(v.x, v.y);
            sxy[1] = new Vector2(v.x, v.w);
            sxy[2] = new Vector2(v.z, v.w);
            sxy[3] = new Vector2(v.z, v.y);

            Vector3[] suv = new Vector3[4];
            suv[0] = new Vector2(uv.x, uv.y);
            suv[1] = new Vector2(uv.x, uv.w);
            suv[2] = new Vector2(uv.z, uv.w);
            suv[3] = new Vector2(uv.z, uv.y);

            AddQuad(vh, sxy, color, suv, size, fullBounds, appendShadow, shadowOffsetLocal);
        }

        /// <summary>
        /// 生成带填充（包括水平、垂直、径向填充）的精灵网格。
        /// 支持 <see cref="Image.FillMethod"/> 的所有填充方式（Horizontal / Vertical / Radial90 / Radial180 / Radial360）。
        /// </summary>
        /// <param name="toFill">目标 VertexHelper。</param>
        /// <param name="preserveAspect">是否保持纵横比。</param>
        /// <param name="canvas">用于像素调整的 Canvas（可为 null）。</param>
        /// <param name="rectTransform">目标 RectTransform。</param>
        /// <param name="activeSprite">精灵（可为 null）。</param>
        /// <param name="color">顶点颜色。</param>
        /// <param name="fillMethod">填充方式。</param>
        /// <param name="fillAmount">填充量（0..1）。</param>
        /// <param name="fillOrigin">填充起点（取决于 fillMethod）。</param>
        /// <param name="fillClockwise">对于径向填充是否顺时针方向填充。</param>
        /// <param name="falloffDistance">衰减距离（保留参数）。</param>
        public static void GenerateFilledSprite(VertexHelper toFill,
            bool preserveAspect,
            Canvas canvas,
            RectTransform rectTransform,
            Sprite activeSprite,
            Color32 color,
            Image.FillMethod fillMethod,
            float fillAmount,
            int fillOrigin,
            bool fillClockwise,
            float falloffDistance)
        {
            toFill.Clear();

            if (fillAmount < 0.001f)
                return;

            Vector4 v = GetDrawingDimensions(preserveAspect, activeSprite, canvas, rectTransform);
            Vector2 size = new Vector2(v.z - v.x, v.w - v.y);
            Vector4 fullBounds = v;

            Vector4 outer = activeSprite != null
                ? Sprites.DataUtility.GetOuterUV(activeSprite)
                : new Vector4(0, 0, 1, 1);

            float tx0 = outer.x;
            float ty0 = outer.y;
            float tx1 = outer.z;
            float ty1 = outer.w;

            // 水平和垂直填充的处理
            if (fillMethod == Image.FillMethod.Horizontal || fillMethod == Image.FillMethod.Vertical)
            {
                if (fillMethod == Image.FillMethod.Horizontal)
                {
                    float fill = (tx1 - tx0) * fillAmount;

                    if (fillOrigin == 1)
                    {
                        v.x = v.z - (v.z - v.x) * fillAmount;
                        tx0 = tx1 - fill;
                    }
                    else
                    {
                        v.z = v.x + (v.z - v.x) * fillAmount;
                        tx1 = tx0 + fill;
                    }
                }
                else if (fillMethod == Image.FillMethod.Vertical)
                {
                    float fill = (ty1 - ty0) * fillAmount;

                    if (fillOrigin == 1)
                    {
                        v.y = v.w - (v.w - v.y) * fillAmount;
                        ty0 = ty1 - fill;
                    }
                    else
                    {
                        v.w = v.y + (v.w - v.y) * fillAmount;
                        ty1 = ty0 + fill;
                    }
                }
            }

            Vector3[] sxy = new Vector3[4];
            sxy[0] = new Vector2(v.x, v.y);
            sxy[1] = new Vector2(v.x, v.w);
            sxy[2] = new Vector2(v.z, v.w);
            sxy[3] = new Vector2(v.z, v.y);

            Vector3[] suv = new Vector3[4];
            suv[0] = new Vector2(tx0, ty0);
            suv[1] = new Vector2(tx0, ty1);
            suv[2] = new Vector2(tx1, ty1);
            suv[3] = new Vector2(tx1, ty0);


            if (fillAmount < 1f &&
                fillMethod != Image.FillMethod.Horizontal &&
                fillMethod != Image.FillMethod.Vertical)
            {
                if (fillMethod == Image.FillMethod.Radial90)
                {
                    if (RadialCut(sxy, suv, fillAmount, fillClockwise, fillOrigin))
                        AddQuad(toFill, sxy, color, suv, size, fullBounds, false, Vector2.zero);
                }
                else if (fillMethod == Image.FillMethod.Radial180)
                {
                    for (int side = 0; side < 2; ++side)
                    {
                        float fx0, fx1, fy0, fy1;
                        int even = fillOrigin > 1 ? 1 : 0;

                        if (fillOrigin == 0 || fillOrigin == 2)
                        {
                            fy0 = 0f;
                            fy1 = 1f;
                            if (side == even)
                            {
                                fx0 = 0f;
                                fx1 = 0.5f;
                            }
                            else
                            {
                                fx0 = 0.5f;
                                fx1 = 1f;
                            }
                        }
                        else
                        {
                            fx0 = 0f;
                            fx1 = 1f;
                            if (side == even)
                            {
                                fy0 = 0.5f;
                                fy1 = 1f;
                            }
                            else
                            {
                                fy0 = 0f;
                                fy1 = 0.5f;
                            }
                        }

                        sxy[0].x = Mathf.Lerp(v.x, v.z, fx0);
                        sxy[1].x = sxy[0].x;
                        sxy[2].x = Mathf.Lerp(v.x, v.z, fx1);
                        sxy[3].x = sxy[2].x;

                        sxy[0].y = Mathf.Lerp(v.y, v.w, fy0);
                        sxy[1].y = Mathf.Lerp(v.y, v.w, fy1);
                        sxy[2].y = sxy[1].y;
                        sxy[3].y = sxy[0].y;

                        suv[0].x = Mathf.Lerp(tx0, tx1, fx0);
                        suv[1].x = suv[0].x;
                        suv[2].x = Mathf.Lerp(tx0, tx1, fx1);
                        suv[3].x = suv[2].x;

                        suv[0].y = Mathf.Lerp(ty0, ty1, fy0);
                        suv[1].y = Mathf.Lerp(ty0, ty1, fy1);
                        suv[2].y = suv[1].y;
                        suv[3].y = suv[0].y;

                        float val = fillClockwise ? fillAmount * 2f - side : fillAmount * 2f - (1 - side);

                        if (RadialCut(sxy, suv, Mathf.Clamp01(val), fillClockwise,
                            ((side + fillOrigin + 3) % 4)))
                        {
                            AddQuad(toFill, sxy, color, suv, size, fullBounds, false, Vector2.zero);
                        }
                    }
                }
                else if (fillMethod == Image.FillMethod.Radial360)
                {
                    for (int corner = 0; corner < 4; ++corner)
                    {
                        float fx0, fx1, fy0, fy1;

                        if (corner < 2)
                        {
                            fx0 = 0f;
                            fx1 = 0.5f;
                        }
                        else
                        {
                            fx0 = 0.5f;
                            fx1 = 1f;
                        }

                        if (corner == 0 || corner == 3)
                        {
                            fy0 = 0f;
                            fy1 = 0.5f;
                        }
                        else
                        {
                            fy0 = 0.5f;
                            fy1 = 1f;
                        }

                        sxy[0].x = Mathf.Lerp(v.x, v.z, fx0);
                        sxy[1].x = sxy[0].x;
                        sxy[2].x = Mathf.Lerp(v.x, v.z, fx1);
                        sxy[3].x = sxy[2].x;

                        sxy[0].y = Mathf.Lerp(v.y, v.w, fy0);
                        sxy[1].y = Mathf.Lerp(v.y, v.w, fy1);
                        sxy[2].y = sxy[1].y;
                        sxy[3].y = sxy[0].y;

                        suv[0].x = Mathf.Lerp(tx0, tx1, fx0);
                        suv[1].x = suv[0].x;
                        suv[2].x = Mathf.Lerp(tx0, tx1, fx1);
                        suv[3].x = suv[2].x;

                        suv[0].y = Mathf.Lerp(ty0, ty1, fy0);
                        suv[1].y = Mathf.Lerp(ty0, ty1, fy1);
                        suv[2].y = suv[1].y;
                        suv[3].y = suv[0].y;

                        float val = fillClockwise
                            ? fillAmount * 4f - ((corner + fillOrigin) % 4)
                            : fillAmount * 4f - (3 - ((corner + fillOrigin) % 4));

                        if (RadialCut(sxy, suv, Mathf.Clamp01(val), fillClockwise, ((corner + 2) % 4)))
                            AddQuad(toFill, sxy, color, suv, size, fullBounds, false, Vector2.zero);
                    }
                }
            }
            else
            {
                AddQuad(toFill, sxy, color, suv, size, fullBounds, false, Vector2.zero);
            }
        }

        /// <summary>
        /// 保持精灵纵横比。会修改传入的 rect 以匹配精灵的纵横比，并根据 RectTransform 的 pivot 进行对齐。
        /// 该方法对外公开以便在其它 UI 布局逻辑中复用。
        /// </summary>
        /// <param name="rect">要调整的矩形（按引用传入）。</param>
        /// <param name="rectTransform">关联的 RectTransform（用于 pivot 对齐）。</param>
        /// <param name="spriteSize">精灵的像素尺寸。</param>
        public static void PreserveSpriteAspectRatio(ref Rect rect, RectTransform rectTransform, Vector2 spriteSize)
        {
            float spriteRatio = spriteSize.x / spriteSize.y;
            float rectRatio = rect.width / rect.height;

            if (spriteRatio > rectRatio)
            {
                float oldHeight = rect.height;
                rect.height = rect.width * (1.0f / spriteRatio);
                rect.y += (oldHeight - rect.height) * rectTransform.pivot.y;
            }
            else
            {
                float oldWidth = rect.width;
                rect.width = rect.height * spriteRatio;
                rect.x += (oldWidth - rect.width) * rectTransform.pivot.x;
            }
        }

        #endregion

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
            Vector2 shadowOffsetLocal)
        {
            // 插入 UV 以避免边缘出现纹理包裹伪影
            float epsilon = 0.001f;

            // 可选：先为阴影添加四边形（渲染在原始四边形下方）
            if (appendShadow)
            {
                int startIndex2 = vertexHelper.currentVertCount;
                // 使用黑色通道标记阴影顶点，保留原始透明度
                Color32 shadowColor = new Color32(0, 0, 0, color.a);

                // 预计算在四边形空间中的归一化偏移，以便 SDF UV 偏移保持一致
                float boundsWidth = bounds.z - bounds.x;
                float boundsHeight = bounds.w - bounds.y;
                float offsetU = boundsWidth != 0 ? shadowOffsetLocal.x / boundsWidth : 0f;
                float offsetV = boundsHeight != 0 ? shadowOffsetLocal.y / boundsHeight : 0f;

                for (int i = 0; i < 4; ++i)
                {
                    Vector3 pos = quadPositions[i] + new Vector3(shadowOffsetLocal.x, shadowOffsetLocal.y, 0);

                    float uBase = Mathf.InverseLerp(bounds.x, bounds.z, quadPositions[i].x);
                    float vBase = Mathf.InverseLerp(bounds.y, bounds.w, quadPositions[i].y);

                    float uShadow = Mathf.Clamp01(uBase + offsetU);
                    float vShadow = Mathf.Clamp01(vBase + offsetV);

                    float u = Mathf.Lerp(epsilon, 1 - epsilon, uShadow);
                    float v = Mathf.Lerp(epsilon, 1 - epsilon, vShadow);
                    Vector2 uv1 = new Vector2(u, v);

                    Vector2 sampleTexCoord = quadUVs[i];

                    vertexHelper.AddVert(pos, shadowColor, sampleTexCoord, uv1, size, Vector2.zero,
                        Vector3.zero, Vector4.zero);
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
            var padding = activeSprite == null ? Vector4.zero : Sprites.DataUtility.GetPadding(activeSprite);
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