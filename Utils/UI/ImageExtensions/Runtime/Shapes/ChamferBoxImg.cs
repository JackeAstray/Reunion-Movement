using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace ReunionMovement.UI.ImageExtensions
{
    /// <summary>
    /// 倒角长方形
    /// </summary>
    [Serializable]
    public class ChamferBoxImg : UIImgComponent
    {
        [SerializeField] private Vector2 chamferBoxSize;
        [SerializeField] private Vector4 chamferBoxRadius;

        private static readonly int chamferBoxSize_Sp = Shader.PropertyToID("_ChamferBoxSize");
        private static readonly int chamferBoxRadius_Sp = Shader.PropertyToID("_ChamferBoxRadius");

        public Material sharedMat { get; set; }
        public bool shouldModifySharedMat { get; set; }
        public RectTransform rectTransform { get; set; }

        public event EventHandler onComponentSettingsChanged;

        public Vector2 ChamferBoxSize
        {
            get => chamferBoxSize;
            set
            {
                chamferBoxSize = Vector2.Max(value, Vector2.one);
                if (shouldModifySharedMat)
                {
                    sharedMat.SetVector(chamferBoxSize_Sp, chamferBoxSize);
                }
                onComponentSettingsChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public Vector4 ChamferBoxRadius
        {
            get => chamferBoxRadius;
            set
            {
                // Clamp每个分量在0~1
                chamferBoxRadius = new Vector4(
                    Mathf.Clamp01(value.x),
                    Mathf.Clamp01(value.y),
                    Mathf.Clamp01(value.z),
                    Mathf.Clamp01(value.w)
                );
                if (shouldModifySharedMat)
                {
                    sharedMat.SetVector(chamferBoxRadius_Sp, chamferBoxRadius);
                }
                onComponentSettingsChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// 初始化
        /// </summary>
        /// <param name="sharedMat"></param>
        /// <param name="renderMat"></param>
        /// <param name="rectTransform"></param>
        public void Init(Material sharedMat, Material renderMat, RectTransform rectTransform)
        {
            this.sharedMat = sharedMat;
            shouldModifySharedMat = sharedMat == renderMat;
            this.rectTransform = rectTransform;

            ChamferBoxSize = chamferBoxSize;
            ChamferBoxRadius = chamferBoxRadius;
        }

        public void OnValidate()
        {
            ChamferBoxSize = chamferBoxSize;
            ChamferBoxRadius = chamferBoxRadius;
        }

        /// <summary>
        /// 材质的初始化值
        /// </summary>
        /// <param name="material"></param>
        public void InitValuesFromMaterial(ref Material material)
        {
            chamferBoxSize = material.GetVector(chamferBoxSize_Sp);
            chamferBoxRadius = material.GetVector(chamferBoxRadius_Sp);
        }

        /// <summary>
        /// 修改材质
        /// </summary>
        /// <param name="material"></param>
        /// <param name="otherProperties"></param>
        public void ModifyMaterial(ref Material material, params object[] otherProperties)
        {
            material.SetVector(chamferBoxSize_Sp, chamferBoxSize);
            material.SetVector(chamferBoxRadius_Sp, chamferBoxRadius);
        }
    }
}