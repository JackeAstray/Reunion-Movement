using UnityEngine;

namespace ReunionMovement.UI.ImageExtensions
{
    /// <summary>
    /// ImageEx 效果复制器：从源 ImageEx 或预设复制所有效果参数到自身 ImageEx 组件。
    /// 支持自动同步（每帧）或手动触发。
    /// </summary>
    [AddComponentMenu("UI/ReunionMovement/ImageEx Replica")]
    [RequireComponent(typeof(ImageEx))]
    [ExecuteAlways]
    public class ImageExReplica : MonoBehaviour
    {
        public enum SourceType { ImageEx, Preset }

        [SerializeField] private SourceType m_SourceType = SourceType.ImageEx;
        [SerializeField] private ImageEx m_SourceImageEx;
        [SerializeField] private ImageExPreset m_SourcePreset;
        [SerializeField] private bool m_AutoSync = true;
        [SerializeField] private bool m_SyncInEditMode = true;

        private ImageEx m_Target;
        private ImageExPreset m_CachedPreset; // 用于检测预设变化

        public SourceType Source
        {
            get => m_SourceType;
            set { m_SourceType = value; Apply(); }
        }

        public ImageEx SourceImageEx
        {
            get => m_SourceImageEx;
            set { m_SourceImageEx = value; Apply(); }
        }

        public ImageExPreset SourcePreset
        {
            get => m_SourcePreset;
            set { m_SourcePreset = value; m_CachedPreset = value; Apply(); }
        }

        public bool AutoSync
        {
            get => m_AutoSync;
            set => m_AutoSync = value;
        }

        private void Awake()
        {
            m_Target = GetComponent<ImageEx>();
        }

        private void OnEnable()
        {
            if (m_Target == null) m_Target = GetComponent<ImageEx>();
            Apply();
        }

        private void Update()
        {
            if (!m_AutoSync) return;
#if UNITY_EDITOR
            if (!m_SyncInEditMode && !Application.isPlaying) return;
#endif
            // 仅在预设引用变化时重新应用（避免每帧 SetMaterialDirty）
            if (m_SourceType == SourceType.Preset && m_SourcePreset != m_CachedPreset)
            {
                m_CachedPreset = m_SourcePreset;
                Apply();
            }
            else if (m_SourceType == SourceType.ImageEx)
            {
                Apply();
            }
        }

        /// <summary>
        /// 立即将源效果复制到自身 ImageEx。
        /// </summary>
        [ContextMenu("Apply")]
        public void Apply()
        {
            if (m_Target == null) m_Target = GetComponent<ImageEx>();
            if (m_Target == null) return;

            if (m_SourceType == SourceType.Preset && m_SourcePreset != null)
            {
                m_SourcePreset.ApplyTo(m_Target);
            }
            else if (m_SourceType == SourceType.ImageEx && m_SourceImageEx != null)
            {
                // 使用临时预设作为中转
                if (m_SourceImageEx == m_Target) return; // 防止自引用

                var temp = ScriptableObject.CreateInstance<ImageExPreset>();
                temp.ReadFrom(m_SourceImageEx);
                temp.ApplyTo(m_Target);
                DestroyImmediate(temp);
            }
        }
    }
}
