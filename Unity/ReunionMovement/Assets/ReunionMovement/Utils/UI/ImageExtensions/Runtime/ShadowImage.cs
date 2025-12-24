using UnityEngine;
using UnityEngine.UI;
using UnityEngine.UI.ImageExtensions;

namespace ReunionMovement.UI.ImageExtensions
{
    [AddComponentMenu("UI/ReunionMovement/ShadowImage")]
    public class ShadowImage : Image
    {
        [Tooltip("在生成的网格上追加一个偏移的阴影四边形（mesh shadow）")]
        public bool appendShadow = true;

        [Tooltip("阴影在本地坐标系下的偏移（单位为像素）")]
        public Vector2 shadowOffsetLocal = new Vector2(8, -8);

        protected override void Awake()
        {
            base.Awake();
            FixAdditionalShaderChannelsInCanvas();

            // Ensure default material uses the ShadowOnly shader if none assigned
            if (material == null)
            {
                var sh = Shader.Find("Hidden/UI/ShadowOnly");
                if (sh != null)
                {
                    material = new Material(sh);
                }
            }
        }

        void FixAdditionalShaderChannelsInCanvas()
        {
            var c = canvas;
            if (c == null) return;
            var additional = c.additionalShaderChannels;
            additional |= AdditionalCanvasShaderChannels.TexCoord1;
            additional |= AdditionalCanvasShaderChannels.TexCoord2;
            c.additionalShaderChannels = additional;
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            Sprite sp = overrideSprite != null ? overrideSprite : sprite;
            if (sp == null)
            {
                base.OnPopulateMesh(vh);
                return;
            }

            // Use our ImageHelper to generate the quad(s). When appendShadow is true
            // ImageHelper will append a duplicate offset quad marked as "shadow".
            if (type == Type.Simple)
            {
                ImageHelper.GenerateSimpleSprite(vh, preserveAspect, canvas, rectTransform, sp, (Color32)color, 0f, appendShadow, shadowOffsetLocal);
            }
            else if (type == Type.Filled)
            {
                // Fallback to default filled behavior for non-simple types
                base.OnPopulateMesh(vh);
            }
            else
            {
                base.OnPopulateMesh(vh);
            }
        }
    }
}
