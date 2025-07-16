using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace ReunionMovement.Common.Util
{
    /// <summary>
    /// 屏幕安全区域适配
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class SafeArea : MonoBehaviour
    {
        private RectTransform rectTf;
        private Rect lastSafeArea = Rect.zero;
        private Vector2 lastScreenSize = Vector2.zero;

        private void Awake()
        {
            rectTf = GetComponent<RectTransform>();
            ApplyIfChanged();
        }

        private void Update()
        {
            ApplyIfChanged();
        }

        /// <summary>
        /// 检查并应用安全区域
        /// </summary>
        private void ApplyIfChanged()
        {
            var safeArea = Screen.safeArea;
            var screenSize = new Vector2(Screen.width, Screen.height);

            if (safeArea.Equals(lastSafeArea) && screenSize.Equals(lastScreenSize))
            {
                return;
            }

            ApplySafeArea(safeArea, screenSize);

            lastSafeArea = safeArea;
            lastScreenSize = screenSize;
        }

        /// <summary>
        /// 应用安全区域
        /// </summary>
        /// <param name="safeArea"></param>
        /// <param name="screenSize"></param>
        private void ApplySafeArea(Rect safeArea, Vector2 screenSize)
        {
            if (rectTf == null || screenSize.x <= 0 || screenSize.y <= 0)
            {
                return;
            }

            Vector2 anchorMin = safeArea.position;
            Vector2 anchorMax = safeArea.position + safeArea.size;
            anchorMin.x /= screenSize.x;
            anchorMin.y /= screenSize.y;
            anchorMax.x /= screenSize.x;
            anchorMax.y /= screenSize.y;

            rectTf.anchoredPosition = Vector2.zero;
            rectTf.sizeDelta = Vector2.zero;
            rectTf.anchorMin = anchorMin.IsFinite() ? anchorMin : Vector2.zero;
            rectTf.anchorMax = anchorMax.IsFinite() ? anchorMax : Vector2.one;
        }
    }
}