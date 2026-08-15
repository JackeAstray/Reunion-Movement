using Cysharp.Threading.Tasks;
using ReunionMovement.Common;
using System.Threading;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;

namespace ReunionMovement.Common.Util
{
    /// <summary>
    /// 摄像机工具类
    /// </summary>
    public partial class CameraUtil : MonoBehaviour
    {
        #region 目标
        // 目标对象
        [Tooltip("摄像机跟随的目标对象，若不设置则默认查找名为 'Target' 的子对象")]
        public Transform targetPos;
        // 目标对象(原始)
        [Tooltip("原始目标对象，提供一个默认位置以便重置，若不设置则默认查找名为 'Target' 的子对象")]
        public Transform targetPosOriginal;
        #endregion

        #region 摄像机移动
        // 摄像机
        public Camera csmoCamera { get; private set; }

        [Space(10)]

        // 移动速度
        [Tooltip("摄像机移动速度，数值越大移动越快")]
        public float csmoCameraSpeed = 50;
        // 如果不为空，摄像机将限制在该盒子碰撞器内
        [Tooltip("如果不为空，摄像机将限制在该盒子碰撞器内")]
        public BoxCollider restrictedZone;
        // 鼠标
        [Tooltip("鼠标输入，若不设置则自动获取")]
        private Mouse mouse;
        // 射线管理器
        [Tooltip("射线检测管理器，负责从屏幕点发出射线并检测碰撞，自动使用指定的 layerMask 和摄像机")]
        private RaycastBase raycastBase;
        // 是否检查鼠标是否在UI上
        [Tooltip("是否检查鼠标或触摸输入是否在UI上，启用后当输入在UI上时将不会进行摄像机控制")]
        public bool checkPointerOverUI = true;
        // 当前摄像机距离
        [Tooltip("当前摄像机距离，自动更新以反映实际距离，启用遮挡物检测时会根据遮挡物调整")]
        private float currentDistance;
        // 鼠标点击射线检测层
        [Tooltip("鼠标点击射线检测层")]
        public LayerMask layerMask;
        [Space(10)]
        // EnhancedTouch 全局引用计数，避免多实例互相关闭
        private static int enhancedTouchRefCount;
        // 启用到达 0 距离后继续沿摄像机 Z 轴前进
        [Tooltip("启用到达 0 距离后继续沿摄像机 Z 轴前进")]
        public bool enableForwardZoomAfterZero = false;
        // 双指手势状态（每帧更新，确保平移/缩放互斥）
        private bool hasTwoFingerTouchGesture;
        private bool isTwoFingerPanGesture;
        private Vector2 twoFingerPanDelta;
        private float twoFingerPinchDelta;
        private const float twoFingerGestureEpsilon = 0.01f;
        // 本帧是否已通过旋转/缩放更新过位置（避免 Update 末尾重复 UpdatePosition 双 Raycast）
        private bool positionUpdatedThisFrame;
        #endregion

        [Space(10)]

        #region 遮挡物检测
        [Tooltip("启用遮挡物检测，摄像机会自动调整位置以避免被遮挡物挡住")]
        // 是否启用遮挡物检测
        public bool enableObstructionCheck = true;
        // 遮挡物层
        [Tooltip("遮挡物层")]
        public LayerMask obstructionMask;
        [Space(5)]
        [Tooltip("遮挡检测时摄像机距离目标的安全偏移量（单位：米），用于避免相机紧贴碰撞表面")]
        public float obstructionOffset = 0.2f;
        #endregion

        [Space(10)]

        #region 摄像机旋转/远近
        // 初始角度
        [Tooltip("初始水平旋转角度（度）")]
        public float rotX = 0;
        [Tooltip("初始垂直旋转角度（度）")]
        public float rotY = 0;
        [Tooltip("摄像机高度偏移")]
        public float offsetHeight = 0f;
        [Tooltip("摄像机水平偏移")]
        public float lateralOffset = 0f;
        [Tooltip("摄像机距离目标的偏移距离")]
        public float offsetDistance = 30f;
        [Tooltip("摄像机最大距离")]
        public float maxDistance = 30f;                     //最大距离
        [Tooltip("摄像机最小距离")]
        public float minDistance = 10f;                     //最小距离
        [Tooltip("摄像机缩放速度")]
        public float zoomSpeed = 50f;                       //缩放速度
        [Tooltip("摄像机缩放值")]
        public float zoomValue = 50f;                       //缩放值
        [Tooltip("摄像机旋转速度")]
        public float rotateSpeed = 15f;                     //转速
        [Space(10)]
        [Tooltip("摄像机最大上下旋转角度")]
        public float maxRotY = 90f;                         //最大上下旋转角度
        [Tooltip("摄像机最小上下旋转角度")]
        public float minRotY = -90f;                        //最小上下旋转角度
        [Space(10)]
        [Tooltip("摄像机最小左右旋转角度")]
        public float minRotX = -180f;                       // 最小左右旋转角度
        [Tooltip("摄像机最大左右旋转角度")]
        public float maxRotX = 180f;                        // 最大左右旋转角度
        [Space(10)]
        [Tooltip("默认距离")]
        public float distance = 30f;                        //默认距离
        Quaternion destRot = Quaternion.identity;
        #endregion

        [Space(10)]

        #region 旋转控制
        // 旋转控制变量
        [Tooltip("启用自动旋转，摄像机会以固定速度自动绕目标旋转")]
        public bool isRotating = false;
        [Tooltip("自动旋转方向，左为逆时针，右为顺时针，None 为不旋转")]
        public RotationDirection rotationDirection = RotationDirection.None;
        [Tooltip("自动旋转速度，单位为度/秒")]
        public float autoRotateSpeed = 15f;                 //自动转速
        public enum RotationDirection
        {
            None,
            Left,
            Right
        }
        #endregion

        #region 动画过渡
        private CancellationTokenSource targetPosCts;
        private CancellationTokenSource cameraViewCts;
        private CancellationTokenSource cameraZoomCts;
        #endregion
    }
}
