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
        private ImageExPreset m_TransferPreset; // 复用的中转预设（避免每帧 CreateInstance/DestroyImmediate）
        private ImageExPreset m_LastApplied;    // 上次应用状态快照（内容级变化检测，无变化则跳过 Apply）

        /// <summary>自动同步节流间隔（秒）</summary>
        private const float AutoSyncInterval = 0.1f;
        private float m_NextSyncTime;

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
            set { m_SourcePreset = value; Apply(); }
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

        private void OnDestroy()
        {
            // 清理运行时创建的辅助预设
            if (m_TransferPreset != null)
            {
                if (Application.isPlaying) Destroy(m_TransferPreset);
                else DestroyImmediate(m_TransferPreset);
                m_TransferPreset = null;
            }
            if (m_LastApplied != null)
            {
                if (Application.isPlaying) Destroy(m_LastApplied);
                else DestroyImmediate(m_LastApplied);
                m_LastApplied = null;
            }
        }

        private void Update()
        {
            if (!m_AutoSync) return;
#if UNITY_EDITOR
            if (!m_SyncInEditMode && !Application.isPlaying) return;
#endif
            // 降频同步：即便 Apply 内部有内容级变化检测，每帧仍有 ReadFrom 全字段拷贝 + SameAs
            // 全字段比较的固定开销（多 Replica 时成倍）；0.1s 节流视觉无感知，需要即时刷新直接调 Apply()
            float now = Application.isPlaying ? Time.unscaledTime : Time.realtimeSinceStartup;
            if (m_NextSyncTime > now) return;
            m_NextSyncTime = now + AutoSyncInterval;
            Apply();
        }

        /// <summary>
        /// 立即将源效果复制到自身 ImageEx（含内容级变化检测，无变化时跳过）。
        /// </summary>
        [ContextMenu("Apply")]
        public void Apply()
        {
            if (m_Target == null) m_Target = GetComponent<ImageEx>();
            if (m_Target == null) return;

            // 复用辅助预设（创建一次，长期使用）
            if (m_TransferPreset == null) m_TransferPreset = ScriptableObject.CreateInstance<ImageExPreset>();
            if (m_LastApplied == null) m_LastApplied = ScriptableObject.CreateInstance<ImageExPreset>();

            if (m_SourceType == SourceType.Preset && m_SourcePreset != null)
            {
                // 预设内容未变化则跳过，避免每帧 SetMaterialDirty 重建 Canvas
                if (m_SourcePreset.SameAs(m_LastApplied)) return;
                m_SourcePreset.ApplyTo(m_Target);
                m_LastApplied.ReadFrom(m_Target);
            }
            else if (m_SourceType == SourceType.ImageEx && m_SourceImageEx != null)
            {
                if (m_SourceImageEx == m_Target) return; // 防止自引用

                // 读取源 → 中转预设，与上次应用快照对比，无变化则跳过
                m_TransferPreset.ReadFrom(m_SourceImageEx);
                if (m_TransferPreset.SameAs(m_LastApplied)) return;
                m_TransferPreset.ApplyTo(m_Target);
                m_LastApplied.ReadFrom(m_Target);
            }
        }
    }
}
