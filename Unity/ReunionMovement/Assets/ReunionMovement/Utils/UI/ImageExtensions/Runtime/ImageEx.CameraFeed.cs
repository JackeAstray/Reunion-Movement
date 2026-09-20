using System;
using ReunionMovement.Common;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace ReunionMovement.UI.ImageExtensions
{
    /// <summary>
    /// ImageEx partial part: Camera Feed (same class, no behavior/serialization change)
    /// </summary>
    public partial class ImageEx
    {
        #region 相机画面（Camera Feed）

        /// <summary>
        /// 动态画面纹理（RenderTexture / WebCamTexture / 视频纹理等）。
        /// 赋值后 ImageEx 的 _MainTex 将采样该纹理，形状 SDF、模糊、过渡等特效仍然生效，
        /// 即“变形的 Image 显示相机画面”。
        /// </summary>
        public Texture CameraTexture
        {
            get => cameraTexture;
            set
            {
                if (cameraTexture == value) return;
                cameraTexture = value;
                base.SetMaterialDirty();
            }
        }

        /// <summary>
        /// 重写 mainTexture：把动态画面作为主纹理绑定到 _MainTex，
        /// CanvasRenderer 会把该纹理传给 ImageEx 着色器采样。
        /// 注意：没有精灵也没有相机纹理时必须显式返回纯白纹理，
        /// 否则若动态材质的 _MainTex 被污染（绑定到透明/异常纹理），
        /// 整个 ImageEx 会呈半透明/不可见，且改颜色、赋精灵都无效。
        /// </summary>
        public override Texture mainTexture
        {
            get
            {
                if (cameraTexture != null) return cameraTexture;
                Sprite s = ActiveSprite;
                if (s != null && s.texture != null) return s.texture;
                return Texture2D.whiteTexture;
            }
        }

        #endregion
    }
}
