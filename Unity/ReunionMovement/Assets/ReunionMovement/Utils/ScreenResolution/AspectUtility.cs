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
        // 实例字段：原为 static，多场景多实例共享时实例 A 销毁会连带销毁实例 B 的底衬相机
        private Camera backgroundCam;
        private Camera cameraObj;

        // 上次的屏幕宽度和高度
        private int lastWidth = -1, lastHeight = -1;
        // 上次的宽高比开关/目标比例（检测运行中配置切换）
        private bool lastFixedAspectRatio = false;
        private float lastTargetAspect = -1f;

        private void Awake()
        {
            cameraObj = GetComponent<Camera>() ?? Camera.main;

            if (!cameraObj)
            {
                Log.Error("无摄像头可用!");
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
            // fixedAspectRatio 关闭或相机缺失：恢复完整视口并清理底衬相机。
            // 运行中关闭开关后 backgroundCam 不应残留（黑色底衬遮住画面）
            if (!ResolutionMgr.Instance.fixedAspectRatio || cameraObj == null)
            {
                if (cameraObj != null)
                {
                    cameraObj.rect = new Rect(0f, 0f, 1f, 1f);
                }
                DestroyBackgroundCam();
                return;
            }

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

        private void OnDestroy()
        {
            // 清理本实例的底衬相机引用
            if (backgroundCam != null)
            {
                Destroy(backgroundCam.gameObject);
                backgroundCam = null;
            }
        }

        private void Update()
        {
            bool needRefresh = Screen.width != lastWidth || Screen.height != lastHeight;
            // 运行中切换 ResolutionMgr.fixedAspectRatio / targetAspectRatio 也应即时生效：
            // 原实现只响应屏幕尺寸变化，开关切换后黑边/裁剪残留到下次分辨率变化才刷新
            var resMgr = ResolutionMgr.Instance;
            if (resMgr != null)
            {
                if (resMgr.fixedAspectRatio != lastFixedAspectRatio
                    || !Mathf.Approximately(resMgr.targetAspectRatio, lastTargetAspect))
                {
                    needRefresh = true;
                }
            }

            if (needRefresh)
            {
                lastWidth = Screen.width;
                lastHeight = Screen.height;
                if (resMgr != null)
                {
                    lastFixedAspectRatio = resMgr.fixedAspectRatio;
                    lastTargetAspect = resMgr.targetAspectRatio;
                }
                UpdateCamera();
            }
        }

        public int screenHeight => cameraObj == null ? Screen.height : (int)(Screen.height * cameraObj.rect.height);
        public int screenWidth => cameraObj == null ? Screen.width : (int)(Screen.width * cameraObj.rect.width);
        public int xOffset => cameraObj == null ? 0 : (int)(Screen.width * cameraObj.rect.x);
        public int yOffset => cameraObj == null ? 0 : (int)(Screen.height * cameraObj.rect.y);

        /// <summary>
        /// 获取摄像机视口矩形，考虑摄像机视口偏移
        /// </summary>
        public Rect screenRect => cameraObj == null
            ? new Rect(0, 0, Screen.width, Screen.height)
            : new Rect(
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
                // cameraObj 缺失（Awake 找不到相机）时不得 NRE：返回原始鼠标位置
                if (cameraObj == null) return Input.mousePosition;
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
                // Event.current 仅在 OnGUI 内有效；cameraObj 缺失时返回原始坐标
                Vector2 mousePos = Event.current != null ? Event.current.mousePosition : Input.mousePosition;
                if (cameraObj == null) return mousePos;
                mousePos.y = Mathf.Clamp(mousePos.y, yOffset, yOffset + screenHeight);
                mousePos.x = Mathf.Clamp(mousePos.x, xOffset, xOffset + screenWidth);
                return mousePos;
            }
        }
    }
}