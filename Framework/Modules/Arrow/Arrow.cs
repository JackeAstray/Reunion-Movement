using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

using Gear.Common;
using Gear.Utilities;
namespace Gear.Framework.Modules
{
    public class Arrow : MonoBehaviour
    {
        public enum ArrowType
        {
            UI,
            World
        }

        public Transform Origin { get { return origin; } set { origin = value; } }

        [SerializeField] private float baseHeight;
        [SerializeField] private RectTransform baseRect;
        [SerializeField] private Transform origin;
        [SerializeField] private bool startsActive;
        [SerializeField] private ArrowType type;

        private RectTransform myRect;
        private Canvas canvas;
        private Camera mainCamera;
        private bool isActive;
        private CameraMainRetry cameraRetry = new CameraMainRetry(-999f);
        // origin 的 RectTransform 缓存（避免每帧 GetComponent）；owner 变化时失效
        private RectTransform originRectCache;
        private Transform originRectOwner;

        private void Awake()
        {
            myRect = (RectTransform)transform;
            canvas = GetComponentInParent<Canvas>();
            mainCamera = Camera.main;
            SetActive(startsActive);
        }

        // LateUpdate：相机通常在 LateUpdate 才最终定位，Update 里计算会慢一帧导致箭头抖动
        private void LateUpdate()
        {
            if (!isActive)
            {
                return;
            }
            // 相机引用丢失时每帧 Camera.main 很慢（内部 FindGameObjectsWithTag），限流重试
            if (mainCamera == null)
            {
                mainCamera = cameraRetry.TryGetCamera();
            }
            Setup();
        }

        private void Setup()
        {
            if (type == ArrowType.UI)
            {
                SetupUI();
            }
            else
            {
                SetupWorld();
            }
        }

        public void SetupUI()
        {
            if (origin == null || canvas == null || baseRect == null)
                return;

            var originRect = GetOriginRect();
            Vector2 screenPos;
            if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                // Overlay：UI 坐标与屏幕像素同一空间，保持原行为
                screenPos = origin.position;
                if (originRect != null)
                    myRect.anchoredPosition = originRect.anchoredPosition;
            }
            else
            {
                // Camera/World 空间 Canvas：origin.position 是世界坐标，不能直接当屏幕坐标用；
                // 先转屏幕空间，再换算到箭头父矩形的本地坐标
                Camera cam = canvas.worldCamera != null ? canvas.worldCamera : mainCamera;
                screenPos = RectTransformUtility.WorldToScreenPoint(cam, origin.position);
                if (myRect.parent is RectTransform parentRect
                    && RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, screenPos, cam, out var local))
                {
                    myRect.anchoredPosition = local;
                }
            }
            ApplyDirection(screenPos);
        }

        private RectTransform GetOriginRect()
        {
            if (originRectOwner != origin)
            {
                originRectOwner = origin;
                originRectCache = origin != null ? origin.GetComponent<RectTransform>() : null;
            }
            return originRectCache;
        }

        public void SetupWorld()
        {
            if (origin == null || mainCamera == null || canvas == null || baseRect == null)
                return;
            Vector3 originPosOnScreen = mainCamera.WorldToScreenPoint(origin.position);
            // 目标位于相机背后时 WorldToScreenPoint 返回的是"镜像"坐标（x/y 取反且 z<0），
            // 直接使用会把背后的目标指示到屏幕另一侧的完全错误位置 —— 而这恰恰是指示箭头
            // 最需要正确工作的场景。按屏幕中心做一次镜像，得到与相机前方一致的方位。
            if (originPosOnScreen.z < 0f)
            {
                originPosOnScreen.x = Screen.width - originPosOnScreen.x;
                originPosOnScreen.y = Screen.height - originPosOnScreen.y;
            }
            myRect.anchoredPosition = new Vector2(originPosOnScreen.x - Screen.width / 2, originPosOnScreen.y - Screen.height / 2) / canvas.scaleFactor;
            ApplyDirection(originPosOnScreen);
        }

        /// <summary>
        /// 计算鼠标相对原点的方向并设置箭头旋转与长度（UI/World 共用）。
        /// 鼠标恰好位于箭头上方时方向向量为零，跳过设置避免 NaN 与除零。
        /// </summary>
        private void ApplyDirection(Vector2 originPosOnScreen)
        {
            Vector2 differenceToMouse = Pointer.current != null ? Pointer.current.position.ReadValue() - originPosOnScreen : Vector2.zero;
            // 除零防护：localScale 任一轴为 0 时除法得 Inf/NaN，随后写入 transform.up 产生 NaN 旋转
            Vector3 scale = myRect.localScale;
            if (scale.x != 0f && scale.y != 0f)
            {
                differenceToMouse.Scale(new Vector2(1f / scale.x, 1f / scale.y));
            }

            // 鼠标恰好位于箭头上方时方向向量为零，跳过旋转/长度设置，避免 NaN 与除零
            if (differenceToMouse.sqrMagnitude < 0.0001f)
                return;

            transform.up = differenceToMouse;
            baseRect.anchorMax = new Vector2(baseRect.anchorMax.x, differenceToMouse.magnitude / canvas.scaleFactor / Mathf.Max(baseHeight, 0.01f));
        }

        private void SetActive(bool b)
        {
            isActive = b;
            if (b)
                Setup();
            if (baseRect != null)
                baseRect.gameObject.SetActive(b);
        }

        public void Activate() => SetActive(true);
        public void Deactivate() => SetActive(false);
        public void SetupAndActivate(Transform origin)
        {
            Origin = origin;
            Activate();
        }
    }
}