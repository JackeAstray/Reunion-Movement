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
    public class CameraUtil : MonoBehaviour
    {
        #region 目标
        // 目标对象
        public Transform targetPos;
        // 目标对象(原始)
        public Transform targetPosOriginal;
        #endregion

        #region 摄像机移动
        // 摄像机
        public Camera csmoCamera { get; private set; }

        [Space(10)]

        // 移动速度
        public float csmoCameraSpeed = 50;
        // 如果不为空，摄像机将限制在该盒子碰撞器内
        public BoxCollider restrictedZone;
        // 鼠标
        private Mouse mouse;
        // 射线管理器
        private RaycastBase raycastBase;
        // 是否检查鼠标是否在UI上
        public bool checkPointerOverUI = true;
        // 当前摄像机距离
        private float currentDistance;
        // 鼠标点击射线检测层
        public LayerMask layerMask;
        #endregion

        [Space(10)]

        #region 遮挡物检测
        // 是否启用遮挡物检测
        public bool enableObstructionCheck = true;
        // 遮挡物层
        public LayerMask obstructionMask;
        #endregion

        [Space(10)]

        #region 摄像机旋转/远近
        // 初始角度
        public float rotX = 0;
        public float rotY = 0;

        public float offsetHeight = 0f;
        public float lateralOffset = 0f;
        public float offsetDistance = 30f;
        public float maxDistance = 30f;                     //最大距离
        public float minDistance = 10f;                     //最小距离
        public float zoomSpeed = 50f;                       //缩放速度
        public float zoomValue = 50f;                       //缩放值
        public float rotateSpeed = 15f;                     //转速
        [Space(10)]
        public float maxRotY = 90f;                         //最大上下旋转角度
        public float minRotY = -90f;                        //最小上下旋转角度
        [Space(10)]
        public float minRotX = -180f;                       // 最小左右旋转角度
        public float maxRotX = 180f;                        // 最大左右旋转角度
        [Space(10)]
        public float distance = 30f;                        //默认距离
        Quaternion destRot = Quaternion.identity;
        #endregion

        [Space(10)]

        #region 旋转控制
        // 旋转控制变量
        public bool isRotating = false;
        public RotationDirection rotationDirection = RotationDirection.None;
        public float autoRotateSpeed = 15f;                 //自动转速
        public enum RotationDirection
        {
            None,
            Left,
            Right
        }
        #endregion

        void Start()
        {
            if (targetPos == null)
            {
                targetPos = transform.Find("Target");
            }

            if (targetPosOriginal == null)
            {
                targetPosOriginal = transform.Find("Target");
            }

            mouse = Mouse.current;
            EnhancedTouchSupport.Enable();

            if (csmoCamera == null)
            {
                csmoCamera = transform.Find("Camera").GetComponent<Camera>();
            }

            raycastBase = new RaycastBase(layerMask, csmoCamera);
            currentDistance = distance;
        }

        void Update()
        {
            if (checkPointerOverUI && IsPointerOverUI())
            {
                return;
            }

            HandleCameraMovement();
            HandleCameraRotation();
            HandleCameraZoom();
            HandleAutoRotation();
            HandleMouseClick();
            UpdatePosition();
        }

        /// <summary>
        /// 鼠标或触摸是否在UI上
        /// </summary>
        private bool IsPointerOverUI()
        {
            // 鼠标
            if (mouse != null && mouse.leftButton.isPressed)
            {
                return EventSystem.current.IsPointerOverGameObject();
            }
            // 触摸
            if (Touch.activeTouches.Count > 0)
            {
                foreach (var touch in Touch.activeTouches)
                {
                    if (EventSystem.current.IsPointerOverGameObject(touch.touchId))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// 处理摄像机移动
        /// </summary>
        private void HandleCameraMovement()
        {
            // 鼠标右键拖动
            if (mouse != null && mouse.rightButton.isPressed)
            {
                float horz = mouse.delta.x.ReadValue();
                float vert = mouse.delta.y.ReadValue();
                MoveCamera(horz, vert);
            }

            // 触摸双指拖动
            if (Touch.activeTouches.Count == 2)
            {
                var t0 = Touch.activeTouches[0];
                var t1 = Touch.activeTouches[1];
                if (t0.phase == UnityEngine.InputSystem.TouchPhase.Moved && t1.phase == UnityEngine.InputSystem.TouchPhase.Moved)
                {
                    Vector2 avgDelta = (t0.delta + t1.delta) / 2f;
                    MoveCamera(avgDelta.x, avgDelta.y);
                }
            }
        }

        /// <summary>
        /// 处理摄像机旋转
        /// </summary>
        private void HandleCameraRotation()
        {
            // 鼠标左键拖动
            if (mouse != null && mouse.leftButton.isPressed)
            {
                float horz = mouse.delta.x.ReadValue();
                float vert = mouse.delta.y.ReadValue();
                OrbitCamera(horz, -vert);
            }

            // 触摸单指拖动
            if (Touch.activeTouches.Count == 1)
            {
                var t = Touch.activeTouches[0];
                if (t.phase == UnityEngine.InputSystem.TouchPhase.Moved)
                {
                    OrbitCamera(t.delta.x, -t.delta.y);
                }
            }
        }

        /// <summary>
        /// 处理自动旋转
        /// </summary>
        private void HandleAutoRotation()
        {
            if (isRotating && !mouse.leftButton.isPressed && !mouse.rightButton.isPressed)
            {
                float rotationStep = autoRotateSpeed * Time.deltaTime;
                if (rotationDirection == RotationDirection.Left)
                {
                    OrbitCamera(rotationStep, 0);
                }
                else if (rotationDirection == RotationDirection.Right)
                {
                    OrbitCamera(-rotationStep, 0);
                }
            }
        }

        private void MoveCamera(float horz, float vert)
        {
            Vector3 moveDirection = (csmoCamera.transform.right * -horz) + (csmoCamera.transform.up * -vert);
            moveDirection *= (csmoCameraSpeed * 0.001f);
            targetPos.position += moveDirection;

            if (restrictedZone != null)
            {
                Vector3 newPosition = targetPos.position;
                if (restrictedZone.bounds.Contains(newPosition))
                {
                    targetPos.position = newPosition;
                }
                else
                {
                    targetPos.position = restrictedZone.bounds.ClosestPoint(newPosition);
                }
            }
        }

        /// <summary>
        /// 处理摄像机缩放
        /// </summary>
        private void HandleCameraZoom()
        {
            // 鼠标滚轮
            if (mouse != null)
            {
                float value = mouse.scroll.ReadValue().y;
                float delta = value > 0 ? 1 : (value < 0 ? -1 : 0);
                SetZoom(delta * -zoomValue);
            }

            // 触摸双指缩放
            if (Touch.activeTouches.Count == 2)
            {
                var t0 = Touch.activeTouches[0];
                var t1 = Touch.activeTouches[1];
                if (t0.phase == UnityEngine.InputSystem.TouchPhase.Moved || t1.phase == UnityEngine.InputSystem.TouchPhase.Moved)
                {
                    float prevDist = (t0.screenPosition - t0.delta - (t1.screenPosition - t1.delta)).magnitude;
                    float currDist = (t0.screenPosition - t1.screenPosition).magnitude;
                    float pinchDelta = currDist - prevDist;
                    SetZoom(-pinchDelta * 0.05f); // 缩放灵敏度可调
                }
            }
        }

        /// <summary>
        /// 处理鼠标点击
        /// </summary>
        private void HandleMouseClick()
        {
            // 鼠标点击
            if (mouse != null && mouse.leftButton.wasPressedThisFrame)
            {
                Vector2 mousePosition = mouse.position.ReadValue();
                if (raycastBase.CastRayFromScreenPoint(mousePosition, out RaycastHit hitInfo))
                {
                    Log.Debug("Hit: " + hitInfo.collider.name);
                }
            }

            // 触摸点击
            foreach (var t in Touch.activeTouches)
            {
                if (t.phase == UnityEngine.InputSystem.TouchPhase.Began)
                {
                    if (raycastBase.CastRayFromScreenPoint(t.screenPosition, out RaycastHit hitInfo))
                    {
                        Log.Debug("Hit: " + hitInfo.collider.name);
                    }
                }
            }
        }

        /// <summary>
        /// 旋转摄像机
        /// </summary>
        /// <param name="horz">水平调整</param>
        /// <param name="vert">垂直调整</param>
        private void OrbitCamera(float horz, float vert)
        {
            float step = Time.deltaTime * rotateSpeed;
            rotX += horz * step;
            rotY += vert * step;

            // 限制左右旋转
            rotX = ClampAngle(rotX, minRotX, maxRotX);

            rotY = Mathf.Clamp(rotY, minRotY, maxRotY);
            Quaternion addRot = Quaternion.Euler(0f, rotX, 0f);
            destRot = addRot * Quaternion.Euler(rotY, 0f, 0f);
            csmoCamera.transform.rotation = destRot;
            UpdatePosition();
        }

        /// <summary>
        /// 更新摄像机位置
        /// </summary>
        private void UpdatePosition()
        {
            offsetDistance = Mathf.MoveTowards(offsetDistance, distance, Time.deltaTime * zoomSpeed);

            Vector3 target = targetPos != null ? targetPos.position : Vector3.zero;
            Vector3 desiredCameraPos = (target + (Vector3.up * offsetHeight)) +
                                      (csmoCamera.transform.rotation * (Vector3.forward * -offsetDistance)) +
                                      (csmoCamera.transform.right * lateralOffset);

            if (enableObstructionCheck)
            {
                // 遮挡检测
                Vector3 direction = desiredCameraPos - target;
                float desiredDistance = direction.magnitude;
                Ray ray = new Ray(target, direction.normalized);
                RaycastHit hit;
                if (Physics.Raycast(ray, out hit, desiredDistance, obstructionMask))
                {
                    currentDistance = hit.distance - 0.2f; // 0.2f为缓冲距离，防止摄像机贴面
                    currentDistance = Mathf.Clamp(currentDistance, minDistance, desiredDistance);
                }
                else
                {
                    currentDistance = Mathf.MoveTowards(currentDistance, offsetDistance, Time.deltaTime * zoomSpeed);
                }
            }
            else
            {
                currentDistance = Mathf.MoveTowards(currentDistance, offsetDistance, Time.deltaTime * zoomSpeed);
            }

            // 重新计算摄像机位置
            csmoCamera.transform.position = target + (csmoCamera.transform.rotation * (Vector3.back * currentDistance)) + (csmoCamera.transform.right * lateralOffset) + (Vector3.up * offsetHeight);
        }

        #region 公开方法
        /// <summary>
        /// 设置目标
        /// </summary>
        /// <param name="target"></param>
        public void SetTarget(Transform target)
        {
            targetPos = target;
        }

        /// <summary>
        /// 设置目标位置为原始对象
        /// </summary>
        public void SetTargetToOriginal()
        {
            targetPos = targetPosOriginal;
        }

        /// <summary>
        /// 设置目标位置
        /// </summary>
        /// <param name="pos"></param>
        public void SetTargetPos(Vector3 pos, float duration = 0.5f)
        {
            targetPos.localPosition = pos;
        }

        /// <summary>
        /// 设置摄像机视角
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        public void SetCameraView(float x, float y)
        {
            rotX = ClampAngle(x, minRotX, maxRotX);
            rotY = Mathf.Clamp(y, minRotY, maxRotY);
            Quaternion addRot = Quaternion.Euler(0f, rotX, 0f);
            destRot = addRot * Quaternion.Euler(rotY, 0f, 0f);

            csmoCamera.transform.localEulerAngles = destRot.eulerAngles;

            UpdatePosition();
        }

        /// <summary>
        /// 设置摄像机远近
        /// </summary>
        /// <param name="value"></param>
        public void SetCameraZoom(float value, float duration = 0.5f)
        {
            distance = value;
        }

        /// <summary>
        /// 设置摄像头远近
        /// </summary>
        public void SetZoom(float delta)
        {
            distance += delta;
            distance = Mathf.Clamp(distance, minDistance, maxDistance);
            UpdatePosition();
        }
        #endregion

        // 辅助：把角度归一化到 -180~180 并在范围内夹取
        private float ClampAngle(float angle, float min, float max)
        {
            angle = Mathf.Repeat(angle + 180f, 360f) - 180f;
            return Mathf.Clamp(angle, min, max);
        }
    }
}