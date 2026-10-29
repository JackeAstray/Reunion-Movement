using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace ReunionMovement.UI.RippleAnimation
{
    /// <summary>
    /// UI波纹动画 - 速率
    /// </summary>

    [RequireComponent(typeof(UIRipple))]
    public class RippleTimer : MonoBehaviour
    {
        /// <summary> 
        /// 偏移量
        /// </summary>
        public Vector2 Offset;

        /// <summary> 
        /// 波纹出现的速率
        /// </summary>
        public float Rate;

        //时间
        float T;

        /// <summary> 
        /// 颜色列表
        /// <summary> 
        public List<Color> Colors = new List<Color>();

        //颜色索引
        int ColorIndex = 0;

        //缓存组件引用，避免每帧重复 GetComponent
        UIRipple ripple;

        void Awake()
        {
            ripple = GetComponent<UIRipple>();
            // Rate = 0（Inspector 默认）时每帧创建一个波纹，瞬间刷满屏幕；设下限 0.05s
            if (Rate <= 0f)
            {
                Rate = 0.05f;
            }
        }

        void Update()
        {
            //当前时间 - 最后一个波纹的时间 >= 波纹
            if (Time.time - T >= Rate)
            {
                // 必须先设置颜色再创建波纹：
                // CreateRipple 内部会读取 StartColor/EndColor 并 InitVisuals，
                // 若在创建之后才设置，每个周期创建的第一个波纹会显示上一周期的颜色。
                if (Colors.Count > 0)
                {
                    ripple.StartColor = Colors[ColorIndex];
                    ripple.EndColor = Colors[ColorIndex];
                    ColorIndex = (ColorIndex + 1) % Colors.Count;
                }

                //创建波纹
                ripple.CreateRipple(Offset);
                //设置新的时间
                T = Time.time;
            }
        }
    }
}