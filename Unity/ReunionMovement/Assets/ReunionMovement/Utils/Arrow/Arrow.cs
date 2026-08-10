using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ReunionMovement.Common.Util
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
        private float cameraRetryTimer;
        private const float CameraRetryInterval = 2f;

        private void Awake()
        {
            myRect = (RectTransform)transform;
            canvas = GetComponentInParent<Canvas>();
            mainCamera = Camera.main;
            SetActive(startsActive);
        }

        private void Update()
        {
            if (!isActive)
            {
                return;
            }
            // 相机引用丢失时每帧 Camera.main 很慢（内部 FindGameObjectsWithTag），限流重试
            if (mainCamera == null)
            {
                cameraRetryTimer += Time.deltaTime;
                if (cameraRetryTimer >= CameraRetryInterval)
                {
                    cameraRetryTimer = 0f;
                    mainCamera = Camera.main;
                }
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
            Vector2 originPosOnScreen = origin.position;
            var originRect = origin.GetComponent<RectTransform>();
            if (originRect != null)
                myRect.anchoredPosition = originRect.anchoredPosition;
            ApplyDirection(originPosOnScreen);
        }

        public void SetupWorld()
        {
            if (origin == null || mainCamera == null || canvas == null || baseRect == null)
                return;
            Vector2 originPosOnScreen = mainCamera.WorldToScreenPoint(origin.position);
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
            differenceToMouse.Scale(new Vector2(1f / myRect.localScale.x, 1f / myRect.localScale.y));

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