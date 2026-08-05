using UnityEngine;
using UnityEngine.UI;
using System.Collections;

namespace ReunionMovement.UI.RippleAnimation
{
    /// <summary>
    /// UI波纹动画
    /// </summary>

    [RequireComponent(typeof(Image))]
    public class Ripple : MonoBehaviour
    {
        //波纹参数
        public float Speed;
        public float MaxSize;
        public Color StartColor;
        public Color EndColor;

        private Image cachedImage;

        /// <summary>波纹创建时间（用于寿命判断，不能用全局 Time.time 否则运行满 10 秒后所有新波纹立即销毁）</summary>
        private float bornTime;

        /// <summary>波纹结束回调（由 UIRipple 注入用于对象池回收；为空时默认销毁）</summary>
        public System.Action<GameObject> OnFinished;

        void Start()
        {
            cachedImage = GetComponent<Image>();
            InitVisuals();
        }

        /// <summary>
        /// 初始化视觉状态（对象池复用后重新调用；Start 只在首次运行时执行一次）
        /// </summary>
        public void InitVisuals()
        {
            if (cachedImage == null) cachedImage = GetComponent<Image>();
            bornTime = Time.time;
            //设置尺寸和颜色
            transform.localScale = Vector3.zero;
            if (cachedImage != null)
            {
                cachedImage.color = new Color(StartColor.r, StartColor.g, StartColor.b, 1f);
            }
        }

        void Update()
        {
            if (cachedImage == null) return;

            //调整比例和颜色
            transform.localScale = Vector3.Lerp(transform.localScale, new Vector3(MaxSize, MaxSize, MaxSize), Time.deltaTime * Speed);
            cachedImage.color = Color.Lerp(cachedImage.color, new Color(EndColor.r, EndColor.g, EndColor.b, 0f), Time.deltaTime * Speed);

            // 使用时间累计替代浮点阈值，避免 Speed 很小时跳过销毁点
            if (transform.localScale.x >= MaxSize * 0.99f || Time.time - bornTime > 10f)
            {
                if (OnFinished != null)
                {
                    var go = gameObject;
                    OnFinished(go);
                }
                else
                {
                    Destroy(gameObject);
                }
            }
        }
    }
}