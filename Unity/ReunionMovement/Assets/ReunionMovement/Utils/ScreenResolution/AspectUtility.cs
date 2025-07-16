using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ReunionMovement.Common.Util
{
    /// <summary>
    /// 宽高比工具
    /// </summary>
    [DisallowMultipleComponent]
    public class AspectUtility : MonoBehaviour
    {
        private static Camera backgroundCam;
        private Camera cameraObj;

        // 上次的屏幕宽度和高度
        private int lastWidth = -1, lastHeight = -1;

        private void Awake()
        {
            cameraObj = GetComponent<Camera>() ?? Camera.main;

            if (!cameraObj)
            {
                Debug.LogError("无摄像头可用!");
                enabled = false;
                return;
            }

            UpdateCamera();
        }

        /// <summary>
        /// 更新摄像头的视口以适应屏幕宽高比
        /// </summary>
        private void UpdateCamera()
        {
            if (!ResolutionMgr.Instance.fixedAspectRatio || cameraObj == null) return;

            float currentAspectRatio = (float)Screen.width / Screen.height;
            float targetAspect = ResolutionMgr.Instance.targetAspectRatio;

            if (Mathf.Approximately(currentAspectRatio, targetAspect))
            {
                cameraObj.rect = new Rect(0f, 0f, 1f, 1f);
                DestroyBackgroundCam();
                return;
            }

            if (currentAspectRatio > targetAspect)
            {
                float inset = 1f - targetAspect / currentAspectRatio;
                cameraObj.rect = new Rect(inset / 2f, 0f, 1f - inset, 1f);
            }
            else
            {
                float inset = 1f - currentAspectRatio / targetAspect;
                cameraObj.rect = new Rect(0f, inset / 2f, 1f, 1f - inset);
            }

            EnsureBackgroundCam();
        }

        private void EnsureBackgroundCam()
        {
            if (backgroundCam == null)
            {
                backgroundCam = new GameObject("BackgroundCam", typeof(Camera)).GetComponent<Camera>();
                backgroundCam.depth = int.MinValue;
                backgroundCam.clearFlags = CameraClearFlags.SolidColor;
                backgroundCam.backgroundColor = Color.black;
                backgroundCam.cullingMask = 0;
            }
        }

        private void DestroyBackgroundCam()
        {
            if (backgroundCam)
            {
                Destroy(backgroundCam.gameObject);
                backgroundCam = null;
            }
        }

        private void Update()
        {
            if (Screen.width != lastWidth || Screen.height != lastHeight)
            {
                lastWidth = Screen.width;
                lastHeight = Screen.height;
                UpdateCamera();
            }
        }

        public int screenHeight => (int)(Screen.height * cameraObj.rect.height);
        public int screenWidth => (int)(Screen.width * cameraObj.rect.width);
        public int xOffset => (int)(Screen.width * cameraObj.rect.x);
        public int yOffset => (int)(Screen.height * cameraObj.rect.y);

        /// <summary>
        /// 获取摄像机视口矩形，考虑摄像机视口偏移
        /// </summary>
        public Rect screenRect => new Rect(
            cameraObj.rect.x * Screen.width,
            cameraObj.rect.y * Screen.height,
            cameraObj.rect.width * Screen.width,
            cameraObj.rect.height * Screen.height
        );

        /// <summary>
        /// 获取鼠标位置，考虑摄像机视口偏移
        /// </summary>
        public Vector3 mousePosition
        {
            get
            {
                Vector3 mousePos = Input.mousePosition;
                mousePos.y -= yOffset;
                mousePos.x -= xOffset;
                return mousePos;
            }
        }

        /// <summary>
        /// 获取 GUI 鼠标位置，考虑摄像机视口偏移
        /// </summary>
        public Vector2 guiMousePosition
        {
            get
            {
                Vector2 mousePos = Event.current.mousePosition;
                mousePos.y = Mathf.Clamp(mousePos.y, yOffset, yOffset + screenHeight);
                mousePos.x = Mathf.Clamp(mousePos.x, xOffset, xOffset + screenWidth);
                return mousePos;
            }
        }
    }
}