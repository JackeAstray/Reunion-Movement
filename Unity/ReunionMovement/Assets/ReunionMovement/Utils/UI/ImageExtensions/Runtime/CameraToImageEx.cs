using ReunionMovement.Common;
using UnityEngine;

namespace ReunionMovement.UI.ImageExtensions
{
    /// <summary>
    /// 把相机画面显示到“变形”的 ImageEx 上（配合形状 SDF / 模糊 / 过渡等特效）。
    ///
    /// 原理：
    ///   1. 把相机的 targetTexture 指向一张 RenderTexture（相机渲染到纹理）；
    ///   2. 把该纹理赋给 ImageEx.CameraTexture（ImageEx 重写了 mainTexture，
    ///      着色器直接以它作为 _MainTex 采样，形状裁剪仍然生效）。
    ///
    /// 注意：
    ///   - ImageEx 的着色器用主纹理 alpha 与形状 SDF 相乘，若 RenderTexture 是透明的，
    ///     画面会不可见。因此默认开启 ForceOpaque，渲染后把 alpha 强制为 1。
    ///   - 未指定外部 RenderTexture 时，本组件会自动创建并按需（UI 矩形变化）重建。
    /// </summary>
    [AddComponentMenu("UI/ReunionMovement/CameraToImageEx")]
    [RequireComponent(typeof(ImageEx))]
    [DisallowMultipleComponent]
    public class CameraToImageEx : MonoBehaviour
    {
        #region 序列化字段

        [Tooltip("要显示画面的相机。")]
        [SerializeField] private Camera sourceCamera;

        [Tooltip("外部指定的 RenderTexture（可留空，由本组件自动创建）。")]
        [SerializeField] private RenderTexture externalRenderTexture;

        [Tooltip("未指定外部 RenderTexture 时，是否自动创建。")]
        [SerializeField] private bool autoCreateRenderTexture = true;

        [Tooltip("自动创建时的分辨率（MatchAspectToRect 开启时由 UI 矩形决定）。")]
        [SerializeField] private int renderTextureWidth = 1024;
        [SerializeField] private int renderTextureHeight = 1024;

        [Tooltip("渲染后强制 alpha=1，避免相机画面因透明在 ImageEx 中不可见。")]
        [SerializeField] private bool forceOpaque = true;

        [Tooltip("自动把画面赋给 ImageEx.CameraTexture。")]
        [SerializeField] private bool autoAssignToImage = true;

        [Tooltip("把 RenderTexture 纵横比对齐到 ImageEx 矩形，避免拉伸变形。")]
        [SerializeField] private bool matchAspectToRect = true;

        [Tooltip("禁用/销毁时释放自动创建的资源，并还原相机的 targetTexture。")]
        [SerializeField] private bool cleanupOnDisable = true;

        #endregion

        #region 私有变量

        private ImageEx image;
        private RectTransform rectTransform;

        private RenderTexture ownedRT;   // 自动创建的 RT（相机直接渲染到此）
        private RenderTexture opaqueRT;  // forceOpaque 时：把 alpha 置 1 后的显示纹理
        private Material opaqueMaterial; // 强制 alpha=1 的 blit 材质

        private Camera boundCamera;
        private Vector2 lastRectSize;

        private const string opaqueShaderName = "Hidden/ReunionMovement/ForceAlpha";

        #endregion

        #region 公共属性

        /// <summary>要显示画面的相机，运行时可动态更换。</summary>
        public Camera SourceCamera
        {
            get => sourceCamera;
            set
            {
                if (sourceCamera == value) return;
                UnbindCamera();
                sourceCamera = value;
                if (isActiveAndEnabled)
                {
                    BindCamera();
                    ApplyTexture();
                }
            }
        }

        /// <summary>当前显示到 ImageEx 上的纹理（null 表示尚未就绪）。</summary>
        public Texture DisplayTexture
        {
            get
            {
                if (forceOpaque) return opaqueRT;
                return ownedRT != null ? ownedRT : externalRenderTexture;
            }
        }

        /// <summary>目标 ImageEx。</summary>
        public ImageEx TargetImage => image;

        #endregion

        #region Unity 生命周期

        private void Awake()
        {
            image = GetComponent<ImageEx>();
            rectTransform = transform as RectTransform;
        }

        private void OnEnable()
        {
            if (image == null)
                image = GetComponent<ImageEx>();

            lastRectSize = rectTransform != null ? rectTransform.rect.size : Vector2.zero;
            EnsureRenderTextures();
            BindCamera();
            ApplyTexture();
        }

        private void Update()
        {
            // UI 矩形尺寸变化时重建 RT，保持纵横比一致、不拉伸
            if (!matchAspectToRect || externalRenderTexture != null || rectTransform == null) return;

            Vector2 size = rectTransform.rect.size;
            if ((size - lastRectSize).sqrMagnitude > 1f)
            {
                lastRectSize = size;
                EnsureRenderTextures();
            }
        }

        private void OnDisable()
        {
            UnbindCamera();
            if (cleanupOnDisable) ReleaseResources();
            if (autoAssignToImage && image != null) image.CameraTexture = null;
        }

        private void OnDestroy()
        {
            UnbindCamera();
            ReleaseResources();
        }

        #endregion

        #region 渲染纹理管理

        private void EnsureRenderTextures()
        {
            if (externalRenderTexture != null)
            {
                EnsureOpaqueTexture(externalRenderTexture.width, externalRenderTexture.height);
                return;
            }

            if (!autoCreateRenderTexture) return;

            Vector2Int size = GetAutoTextureSize();
            if (ownedRT == null || ownedRT.width != size.x || ownedRT.height != size.y)
            {
                ReleaseOwnedRenderTextures();
                ownedRT = new RenderTexture(size.x, size.y, 24, RenderTextureFormat.ARGB32)
                {
                    name = "CameraToImageEx_RT",
                    antiAliasing = Mathf.Max(1, QualitySettings.antiAliasing)
                };
                ownedRT.Create();
            }

            EnsureOpaqueTexture(size.x, size.y);

            // 纹理重建后重新绑定相机与 ImageEx
            BindCamera();
            ApplyTexture();
        }

        private Vector2Int GetAutoTextureSize()
        {
            if (matchAspectToRect && rectTransform != null)
            {
                Vector2 size = rectTransform.rect.size;
                lastRectSize = size;
                return new Vector2Int(
                    Mathf.Max(2, Mathf.RoundToInt(size.x)),
                    Mathf.Max(2, Mathf.RoundToInt(size.y)));
            }

            return new Vector2Int(
                Mathf.Max(2, renderTextureWidth),
                Mathf.Max(2, renderTextureHeight));
        }

        private void EnsureOpaqueTexture(int width, int height)
        {
            if (!forceOpaque) return;
            if (opaqueRT != null && opaqueRT.width == width && opaqueRT.height == height) return;

            if (opaqueRT != null)
            {
                opaqueRT.Release();
                Destroy(opaqueRT);
                opaqueRT = null;
            }

            opaqueRT = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32)
            {
                name = "CameraToImageEx_Opaque"
            };
            opaqueRT.Create();
            EnsureOpaqueMaterial();
        }

        private void EnsureOpaqueMaterial()
        {
            if (opaqueMaterial != null) return;

            Shader shader = Shader.Find(opaqueShaderName);
            if (shader == null)
            {
                Log.Warning("CameraToImageEx：未找到着色器 '{0}'，相机画面 alpha 将保持原样（透明区域可能不可见）。", opaqueShaderName);
                return;
            }

            opaqueMaterial = new Material(shader) { name = "CameraToImageEx_ForceAlpha" };
        }

        private void ReleaseOwnedRenderTextures()
        {
            if (ownedRT != null)
            {
                ownedRT.Release();
                Destroy(ownedRT);
                ownedRT = null;
            }
        }

        private void ReleaseResources()
        {
            ReleaseOwnedRenderTextures();

            if (opaqueRT != null)
            {
                opaqueRT.Release();
                Destroy(opaqueRT);
                opaqueRT = null;
            }

            if (opaqueMaterial != null)
            {
                Destroy(opaqueMaterial);
                opaqueMaterial = null;
            }
        }

        #endregion

        #region 相机绑定

        private void BindCamera()
        {
            if (sourceCamera == null) return;

            UnbindCamera();
            boundCamera = sourceCamera;
            boundCamera.targetTexture = ownedRT != null ? ownedRT : externalRenderTexture;

            if (forceOpaque)
                Camera.onPostRender += OnCameraPostRenderHandler;
        }

        private void UnbindCamera()
        {
            if (boundCamera == null) return;

            if (forceOpaque)
                Camera.onPostRender -= OnCameraPostRenderHandler;

            // 只还原本组件绑定的 targetTexture，避免误清用户手动设置的 RT
            if (boundCamera.targetTexture == ownedRT || boundCamera.targetTexture == externalRenderTexture)
                boundCamera.targetTexture = null;

            boundCamera = null;
        }

        /// <summary>
        /// 相机渲染完成后触发：把画面 blit 到 opaqueRT 并强制 alpha=1，
        /// 保证 ImageEx 的“alpha × 形状 SDF”不会把画面裁掉。
        /// </summary>
        private void OnCameraPostRenderHandler(Camera cam)
        {
            if (cam != boundCamera) return;
            if (opaqueRT == null || opaqueMaterial == null || cam.targetTexture == null) return;

            Graphics.Blit(cam.targetTexture, opaqueRT, opaqueMaterial);
        }

        private void ApplyTexture()
        {
            if (image == null || !autoAssignToImage) return;

            Texture display = DisplayTexture;
            if (display != null)
                image.CameraTexture = display;
        }

        #endregion
    }
}
