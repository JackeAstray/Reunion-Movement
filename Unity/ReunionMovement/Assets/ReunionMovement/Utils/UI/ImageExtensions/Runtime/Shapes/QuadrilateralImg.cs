using System;
using UnityEngine;

namespace ReunionMovement.UI.ImageExtensions
{
    /// <summary>
    /// 四边形（支持梯形、平行四边形、风筝形等任意凸四边形）。
    ///
    /// 每个角的偏移值为矩形宽/高的比例（-0.5 ~ 0.5），
    /// 含义是该角相对原位置的位移：x 向右为正、y 向上为正。
    /// （正值整体 = 该角向右/上移动，负值 = 向左/下移动）
    ///
    /// 常用效果示例（偏移为矩形宽/高的比例）：
    ///   - 平行四边形（上边右移）：TopLeft = (0.2, 0)，TopRight = (0.2, 0)
    ///   - 等腰梯形(上窄下宽)：TopLeft = (0.2, 0)，TopRight = (-0.2, 0)  —— 两个上角同时向中间内缩
    ///   - 直角梯形：TopLeft = (0.3, 0)，TopRight = (0, 0)
    ///   - 倒梯形(下窄上宽)：BottomLeft = (0.2, 0)，BottomRight = (-0.2, 0)
    ///   - 风筝形：TopLeft = (0.5, 0)，TopRight = (-0.5, 0)（上边缩成一个点）
    /// </summary>
    [Serializable]
    public class QuadrilateralImg : UIImgComponent
    {
        [SerializeField] private Vector2 topLeftOffset;     // 左上角偏移（宽/高比例）
        [SerializeField] private Vector2 topRightOffset;    // 右上角偏移（宽/高比例）
        [SerializeField] private Vector2 bottomLeftOffset;  // 左下角偏移（宽/高比例）
        [SerializeField] private Vector2 bottomRightOffset; // 右下角偏移（宽/高比例）

        private static readonly int topLeft_Sp = Shader.PropertyToID("_QuadTopLeft");
        private static readonly int topRight_Sp = Shader.PropertyToID("_QuadTopRight");
        private static readonly int bottomLeft_Sp = Shader.PropertyToID("_QuadBottomLeft");
        private static readonly int bottomRight_Sp = Shader.PropertyToID("_QuadBottomRight");

        public Material sharedMat { get; set; }
        public bool shouldModifySharedMat { get; set; }
        public RectTransform rectTransform { get; set; }

        public event EventHandler onComponentSettingsChanged;

        /// <summary>左上角偏移（-0.5 ~ 0.5，占矩形宽/高比例）</summary>
        public Vector2 TopLeftOffset
        {
            get => topLeftOffset;
            set
            {
                topLeftOffset = Clamp(value);
                OnChanged();
            }
        }

        /// <summary>右上角偏移（-0.5 ~ 0.5，占矩形宽/高比例）</summary>
        public Vector2 TopRightOffset
        {
            get => topRightOffset;
            set
            {
                topRightOffset = Clamp(value);
                OnChanged();
            }
        }

        /// <summary>左下角偏移（-0.5 ~ 0.5，占矩形宽/高比例）</summary>
        public Vector2 BottomLeftOffset
        {
            get => bottomLeftOffset;
            set
            {
                bottomLeftOffset = Clamp(value);
                OnChanged();
            }
        }

        /// <summary>右下角偏移（-0.5 ~ 0.5，占矩形宽/高比例）</summary>
        public Vector2 BottomRightOffset
        {
            get => bottomRightOffset;
            set
            {
                bottomRightOffset = Clamp(value);
                OnChanged();
            }
        }

        private static Vector2 Clamp(Vector2 v)
        {
            return new Vector2(Mathf.Clamp(v.x, -0.5f, 0.5f), Mathf.Clamp(v.y, -0.5f, 0.5f));
        }

        private void OnChanged()
        {
            ApplyToShared();
            onComponentSettingsChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// 初始化
        /// </summary>
        public void Init(Material sharedMat, Material renderMat, RectTransform rectTransform)
        {
            this.sharedMat = sharedMat;
            shouldModifySharedMat = sharedMat == renderMat;
            this.rectTransform = rectTransform;

            ApplyToShared();
        }

        public void OnValidate()
        {
            ApplyToShared();
        }

        /// <summary>
        /// 材质的初始化值
        /// </summary>
        public void InitValuesFromMaterial(ref Material material)
        {
            topLeftOffset = material.GetVector(topLeft_Sp);
            topRightOffset = material.GetVector(topRight_Sp);
            bottomLeftOffset = material.GetVector(bottomLeft_Sp);
            bottomRightOffset = material.GetVector(bottomRight_Sp);
        }

        /// <summary>
        /// 修改材质
        /// </summary>
        public void ModifyMaterial(ref Material material, params object[] otherProperties)
        {
            material.SetVector(topLeft_Sp, topLeftOffset);
            material.SetVector(topRight_Sp, topRightOffset);
            material.SetVector(bottomLeft_Sp, bottomLeftOffset);
            material.SetVector(bottomRight_Sp, bottomRightOffset);
        }

        private void ApplyToShared()
        {
            if (!shouldModifySharedMat || sharedMat == null) return;
            sharedMat.SetVector(topLeft_Sp, topLeftOffset);
            sharedMat.SetVector(topRight_Sp, topRightOffset);
            sharedMat.SetVector(bottomLeft_Sp, bottomLeftOffset);
            sharedMat.SetVector(bottomRight_Sp, bottomRightOffset);
        }
    }
}
