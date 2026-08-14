using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;
using UnityEngine.InputSystem;

namespace ReunionMovement.UI.RippleAnimation
{
    /// <summary>
    /// UI波纹动画
    /// </summary>
    [RequireComponent(typeof(Mask))]
    [RequireComponent(typeof(Image))]
    public class UIRipple : MonoBehaviour
    {

        /// <summary> 
        /// 将渲染的精灵
        /// </summary>
        public Sprite ShapeSprite;

        /// <summary> 
        /// 纹波增长的速度
        /// </summary>
        [Range(0.25f, 5f)]
        public float Speed = 1f;

        /// <summary> 
        /// 如果为 true，MaxSize 将自动设置
        /// </summary>
        public bool AutomaticMaxSize = true;

        /// <summary> 
        /// 波纹的最大尺寸
        /// </summary>
        public float MaxSize = 4f;

        /// <summary> 
        /// 波纹起始颜色
        /// </summary>
        public Color StartColor = new Color(1f, 1f, 1f, 1f);

        /// <summary> 
        /// 波纹结束颜色
        /// </summary>
        public Color EndColor = new Color(1f, 1f, 1f, 1f);

        /// <summary> 
        /// 如果为 true，则仅当您单击 UI 元素时才会出现波纹
        /// </summary>
        public bool OnUIOnly = true;

        /// <summary> 
        /// 如果 true 波纹将出现在 UI 元素中所有其他子项的顶部 
        /// </summary>
        public bool RenderOnTop = false;

        /// <summary> 
        /// 如果为 true，波纹将从 UI 元素的中心开始
        /// </summary>
        public bool StartAtCenter = false;

        // 波纹对象池（复用避免每次点击 new GameObject + AddComponent 的 GC 压力）
        private readonly List<GameObject> ripplePool = new List<GameObject>();
        private const int MaxPoolSize = 16;

        void Awake()
        {
            //根据需要自动设置 MaxSize
            if (AutomaticMaxSize)
            {
                RectTransform RT = gameObject.transform as RectTransform;
                float w = Mathf.Abs(RT.rect.width);
                float h = Mathf.Abs(RT.rect.height);
                // 宽高为 0 时除法产生 NaN/Infinity；Infinity 无法被下方 NaN 分支兑底，
                // Clamp 后变成 1000 使波纹瞬间覆盖全屏。先做尺寸有效性守卫。
                if (w > 0.001f && h > 0.001f)
                {
                    MaxSize = (w > h) ? 4f * (w / h) : 4f * (h / w);
                }

                if (float.IsNaN(MaxSize) || float.IsInfinity(MaxSize) || MaxSize <= 0f)
                {
                    MaxSize = (transform.localScale.x > transform.localScale.y) ? 4f * transform.localScale.x : 4f * transform.localScale.y;
                }
            }

            MaxSize = Mathf.Clamp(MaxSize, 0.5f, 1000f);
        }

        void OnDestroy()
        {
            // 本组件销毁时子物体波纹一并销毁，清空池避免残留已销毁引用
            ripplePool.Clear();
        }

        void Update()
        {
            // 检测鼠标左键的点击或者手机屏幕的触摸
            if (Pointer.current != null && Pointer.current.press.wasPressedThisFrame)
            {
                var pos = Pointer.current.position.ReadValue();
                if (!OnUIOnly || IsOnUIElement(pos))
                {
                    CreateRipple(pos);
                }
            }
        }

        /// <summary>
        /// 创建波纹
        /// </summary>
        /// <param name="Position"></param>
        public void CreateRipple(Vector2 Position)
        {
            // 优先从对象池复用（顺带清理已被销毁的失效项，防止池退化）
            GameObject ThisRipple = null;
            for (int i = ripplePool.Count - 1; i >= 0; i--)
            {
                if (ripplePool[i] == null)  // Unity fake-null：组件已被销毁的条目
                {
                    ripplePool.RemoveAt(i);
                    continue;
                }
                if (!ripplePool[i].activeInHierarchy)
                {
                    ThisRipple = ripplePool[i];
                    break;
                }
            }

            if (ThisRipple == null)
            {
                //创建游戏对象并添加组件
                ThisRipple = new GameObject();
                ThisRipple.AddComponent<Ripple>();
                ThisRipple.AddComponent<Image>();
                ThisRipple.name = "Ripple";
                ThisRipple.GetComponent<Ripple>().OnFinished = RecycleRipple;
            }

            ThisRipple.SetActive(true);
            ThisRipple.GetComponent<Image>().sprite = ShapeSprite;

            //设置父对象
            ThisRipple.transform.SetParent(gameObject.transform);

            //如果需要，重新排列子对象
            if (!RenderOnTop)
            { ThisRipple.transform.SetAsFirstSibling(); }

            //将波纹设置在正确的位置（屏幕坐标 → 父 RectTransform 本地坐标，兼容 Camera/WorldSpace 画布）
            if (StartAtCenter)
            { ThisRipple.transform.localPosition = new Vector2(0f, 0f); }
            else
            {
                var parentRt = transform as RectTransform;
                var canvas = parentRt != null ? parentRt.GetComponentInParent<Canvas>() : null;
                if (parentRt != null &&
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(
                        parentRt, Position,
                        canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null,
                        out var localPos))
                {
                    ThisRipple.transform.localPosition = localPos;
                }
                else
                {
                    // 兜底：异常挂载（无 RectTransform）时按屏幕坐标直接赋值（Overlay 画布下等价）
                    ThisRipple.transform.position = Position;
                }
            }

            //在Ripple中设置参数并重置视觉状态（对象池复用后 Start 不会再次执行）
            var ripple = ThisRipple.GetComponent<Ripple>();
            ripple.Speed = Speed;
            ripple.MaxSize = MaxSize;
            ripple.StartColor = StartColor;
            ripple.EndColor = EndColor;
            ripple.InitVisuals();
        }

        /// <summary>
        /// 回收波纹到对象池（超出上限或已被销毁则直接销毁）
        /// </summary>
        private void RecycleRipple(GameObject go)
        {
            if (go == null) return;
            go.SetActive(false);
            if (ripplePool.Count < MaxPoolSize && !ripplePool.Contains(go))
            {
                ripplePool.Add(go);
            }
            else
            {
                Destroy(go);
            }
        }


        /// <summary>
        /// 是在UI元素上
        /// </summary>
        /// <param name="Position"></param>
        /// <returns></returns>
        public bool IsOnUIElement(Vector2 Position)
        {
            RectTransform rt = GetComponent<RectTransform>();
            if (rt == null) return false;

            // 屏幕坐标 → 本地坐标，兼容 CanvasScaler 缩放、pivot 与渲染相机；
            // 原实现用世界坐标与屏幕像素直接比较，在非 1:1 缩放画布下会误判
            Camera cam = null;
            var canvas = rt.GetComponentInParent<Canvas>();
            if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            {
                cam = canvas.worldCamera;
            }

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, Position, cam, out Vector2 localPoint))
            {
                return false;
            }

            // rect 为相对 pivot 的局部矩形，localPoint 同为本地坐标，直接包含判断（自动兼容 pivot）
            return rt.rect.Contains(localPoint);
        }
    }
}