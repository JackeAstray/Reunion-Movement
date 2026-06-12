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
        // EnhancedTouch 全局引用计数，避免多实例互相关闭
        private static int enhancedTouchRefCount;

        // 启用到达 0 距离后继续沿摄像机 Z 轴前进
        [Tooltip("启用到达 0 距离后继续沿摄像机 Z 轴前进")]
        public bool enableForwardZoomAfterZero = false;
        // 前进缩放累计位移（目标点沿摄像机 Z 轴前进的总量，用于回退）
        private float forwardZoomDistance = 0f;
        #endregion

        [Space(10)]

        #region 遮挡物检测
        [Tooltip("启用遮挡物检测，摄像机会自动调整位置以避免被遮挡物挡住")]
        // 是否启用遮挡物检测
        public bool enableObstructionCheck = true;
        // 遮挡物层
        [Tooltip("遮挡物层")]
        public LayerMask obstructionMask;
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
        private Coroutine targetPosCoroutine;
        private Coroutine cameraViewCoroutine;
        private Coroutine cameraZoomCoroutine;
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
            if (enhancedTouchRefCount == 0)
            {
                EnhancedTouchSupport.Enable();
            }
            enhancedTouchRefCount++;

            if (csmoCamera == null)
            {
                Transform cameraTransform = transform.Find("Camera");
                if (cameraTransform != null)
                {
                    csmoCamera = cameraTransform.GetComponent<Camera>();
                }
                if (csmoCamera == null)
                {
                    csmoCamera = GetComponentInChildren<Camera>();
                }
            }

            if (csmoCamera == null)
            {
                Debug.LogError("CameraUtil: 未找到可用 Camera 组件。", this);
                enabled = false;
                return;
            }

            if (targetPos == null)
            {
                Debug.LogError("CameraUtil: 未找到 Target 目标对象。", this);
                enabled = false;
                return;
            }

            raycastBase = new RaycastBase(layerMask, csmoCamera);
            distance = Mathf.Clamp(distance, GetEffectiveMinDistance(), maxDistance);
            currentDistance = distance;
            UpdatePosition();
        }

        private void OnDestroy()
        {
            enhancedTouchRefCount = Mathf.Max(0, enhancedTouchRefCount - 1);
            if (enhancedTouchRefCount == 0)
            {
                EnhancedTouchSupport.Disable();
            }
        }

        void Update()
        {
            if (csmoCamera == null || targetPos == null)
            {
                return;
            }

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
            if (EventSystem.current == null)
            {
                return false;
            }

            // 鼠标（任意主要交互输入都检查）
            if (mouse != null)
            {
                bool mouseInteracting = mouse.leftButton.isPressed ||
                                        mouse.rightButton.isPressed ||
                                        Mathf.Abs(mouse.scroll.ReadValue().y) > 0.01f;
                if (mouseInteracting)
                {
                    return EventSystem.current.IsPointerOverGameObject();
                }
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
            bool hasMouseInput = mouse != null && (mouse.leftButton.isPressed || mouse.rightButton.isPressed);
            if (isRotating && !hasMouseInput)
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

        /// <summary>
        /// 根据水平和垂直输入沿摄像机的局部右向和上向平移目标位置，并按摄像机速度缩放；若设置了限制区域，则将位置约束在该盒形碰撞体内。
        /// </summary>
        /// <remarks>移动量以 csmoCameraSpeed * 0.001f 缩放，并在存在 restrictedZone 时将位置夹在盒形碰撞体内。</remarks>
        /// <param name="horz">水平方向输入；正值将目标向摄像机左侧移动。</param>
        /// <param name="vert">垂直方向输入；正值将目标向摄像机下方移动。</param>
        private void MoveCamera(float horz, float vert)
        {
            Vector3 moveDirection = (csmoCamera.transform.right * -horz) + (csmoCamera.transform.up * -vert);
            moveDirection *= (csmoCameraSpeed * 0.001f);
            targetPos.position += moveDirection;

            if (restrictedZone != null)
            {
                targetPos.position = ClampPointToBoxCollider(restrictedZone, targetPos.position);
            }
        }

        /// <summary>
        /// 将给定的世界坐标点限制在指定 BoxCollider 的边界内并返回限制后的世界坐标点。
        /// </summary>
        /// <remarks>先将点转换到 BoxCollider 的局部空间，基于 center 和 size 计算最小/最大边界，对局部坐标的各分量使用
        /// Mathf.Clamp，然后将结果转换回世界空间。</remarks>
        /// <param name="box">裁剪所依据的 BoxCollider。</param>
        /// <param name="worldPoint">待裁剪的世界坐标点。</param>
        /// <returns>裁剪到 BoxCollider 边界内的世界坐标点。</returns>
        private Vector3 ClampPointToBoxCollider(BoxCollider box, Vector3 worldPoint)
        {
            Transform boxTransform = box.transform;
            Vector3 localPoint = boxTransform.InverseTransformPoint(worldPoint);

            Vector3 min = box.center - (box.size * 0.5f);
            Vector3 max = box.center + (box.size * 0.5f);

            localPoint.x = Mathf.Clamp(localPoint.x, min.x, max.x);
            localPoint.y = Mathf.Clamp(localPoint.y, min.y, max.y);
            localPoint.z = Mathf.Clamp(localPoint.z, min.z, max.z);

            return boxTransform.TransformPoint(localPoint);
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
                    Log.Debug($"CameraUtil: Hit {hitInfo.collider.gameObject.name} at {hitInfo.point}");
                }
            }

            // 触摸点击
            foreach (var t in Touch.activeTouches)
            {
                if (t.phase == UnityEngine.InputSystem.TouchPhase.Began)
                {
                    if (raycastBase.CastRayFromScreenPoint(t.screenPosition, out RaycastHit hitInfo))
                    {
                        Log.Debug($"CameraUtil: Hit {hitInfo.collider.gameObject.name} at {hitInfo.point}");
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
            float minZoomDistance = GetEffectiveMinDistance();
            distance = Mathf.Clamp(distance, minZoomDistance, maxDistance);
            offsetDistance = Mathf.MoveTowards(offsetDistance, distance, Time.deltaTime * zoomSpeed);

            if (!enableForwardZoomAfterZero)
            {
                forwardZoomDistance = 0f;
            }
            forwardZoomDistance = Mathf.Max(0f, forwardZoomDistance);

            Vector3 target = targetPos != null ? targetPos.position : Vector3.zero;

            // 基础摄像机位置（不含任何额外偏移，直接由目标点和后退距离决定）
            Vector3 baseCameraPos = target + (Vector3.up * offsetHeight) +
                                   (csmoCamera.transform.rotation * (Vector3.back * offsetDistance)) +
                                   (csmoCamera.transform.right * lateralOffset);

            if (enableObstructionCheck)
            {
                Vector3 direction = baseCameraPos - target;
                float desiredDistance = direction.magnitude;
                if (desiredDistance > 0.0001f)
                {
                    Ray ray = new Ray(target, direction.normalized);
                    if (Physics.Raycast(ray, out RaycastHit hit, desiredDistance, obstructionMask))
                    {
                        currentDistance = Mathf.Clamp(hit.distance - 0.2f, minZoomDistance, desiredDistance);
                        offsetDistance = Mathf.Min(offsetDistance, currentDistance);
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
            }
            else
            {
                currentDistance = Mathf.MoveTowards(currentDistance, offsetDistance, Time.deltaTime * zoomSpeed);
            }

            // 使用 currentDistance 重建位置
            Vector3 finalCameraPos = target + (Vector3.up * offsetHeight) +
                                    (csmoCamera.transform.rotation * (Vector3.back * currentDistance)) +
                                    (csmoCamera.transform.right * lateralOffset);

            csmoCamera.transform.position = finalCameraPos;
        }

        /// <summary>
        /// 设置摄像机远近
        /// </summary>
        /// <param name="delta">变化量，负值=放大拉近，正值=缩小拉远</param>
        private void SetZoom(float delta)
        {
            float minZoomDistance = GetEffectiveMinDistance();

            if (enableForwardZoomAfterZero)
            {
                if (delta < 0f)
                {
                    // 放大：先缩近距离，到最小值后移动目标点沿摄像机Z轴前进
                    float desiredDistance = distance + delta;
                    distance = Mathf.Max(minZoomDistance, desiredDistance);
                    float leftover = desiredDistance - distance;
                    if (leftover < 0f)
                    {
                        float forwardAmount = -leftover;
                        Vector3 oldTargetPos = targetPos.position;
                        Vector3 newTargetPos = oldTargetPos + csmoCamera.transform.forward * forwardAmount;
                        if (restrictedZone != null)
                        {
                            newTargetPos = ClampPointToBoxCollider(restrictedZone, newTargetPos);
                        }
                        targetPos.position = newTargetPos;
                        // 记录实际前进量（可能被 restrictedZone 限制）
                        float actualForward = Vector3.Dot(newTargetPos - oldTargetPos, csmoCamera.transform.forward);
                        forwardZoomDistance += Mathf.Max(0f, actualForward);
                    }
                }
                else if (delta > 0f)
                {
                    // 缩小：先回退目标点前进量，再增加距离
                    float consumeForward = Mathf.Min(forwardZoomDistance, delta);
                    if (consumeForward > 0f)
                    {
                        forwardZoomDistance -= consumeForward;
                        targetPos.position -= csmoCamera.transform.forward * consumeForward;
                    }
                    distance += (delta - consumeForward);
                }

                distance = Mathf.Clamp(distance, minZoomDistance, maxDistance);
                forwardZoomDistance = Mathf.Max(0f, forwardZoomDistance);
            }
            else
            {
                forwardZoomDistance = 0f;
                distance = Mathf.Clamp(distance + delta, minZoomDistance, maxDistance);
            }

            UpdatePosition();
        }

        #region 公开方法
        /// <summary>
        /// 设置目标
        /// </summary>
        /// <param name="target">目标对象</param>
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
        /// <param name="pos">目标世界坐标</param>
        /// <param name="duration">过渡时间（秒），≤0 则瞬间设置</param>
        public void SetTargetPos(Vector3 pos, float duration = 0.5f)
        {
            if (targetPosCoroutine != null)
                StopCoroutine(targetPosCoroutine);

            if (duration <= 0f)
            {
                targetPos.position = pos;
            }
            else
            {
                targetPosCoroutine = StartCoroutine(AnimateTargetPos(pos, duration));
            }
        }

        /// <summary>
        /// 设置摄像机视角
        /// </summary>
        /// <param name="x">目标水平角度</param>
        /// <param name="y">目标垂直角度</param>
        /// <param name="duration">过渡时间（秒），≤0 则瞬间设置</param>
        public void SetCameraView(float x, float y, float duration = 0.5f)
        {
            if (cameraViewCoroutine != null)
                StopCoroutine(cameraViewCoroutine);

            if (duration <= 0f)
            {
                rotX = ClampAngle(x, minRotX, maxRotX);
                rotY = Mathf.Clamp(y, minRotY, maxRotY);
                Quaternion addRot = Quaternion.Euler(0f, rotX, 0f);
                destRot = addRot * Quaternion.Euler(rotY, 0f, 0f);
                csmoCamera.transform.localEulerAngles = destRot.eulerAngles;
                UpdatePosition();
            }
            else
            {
                cameraViewCoroutine = StartCoroutine(AnimateCameraView(x, y, duration));
            }
        }

        /// <summary>
        /// 设置摄像机远近
        /// </summary>
        /// <param name="value">目标距离</param>
        /// <param name="duration">过渡时间（秒），≤0 则瞬间设置</param>
        public void SetCameraZoom(float value, float duration = 0.5f)
        {
            if (cameraZoomCoroutine != null)
                StopCoroutine(cameraZoomCoroutine);

            float clampedValue = Mathf.Clamp(value, GetEffectiveMinDistance(), maxDistance);

            if (duration <= 0f)
            {
                distance = clampedValue;
                forwardZoomDistance = 0f;
                UpdatePosition();
            }
            else
            {
                cameraZoomCoroutine = StartCoroutine(AnimateCameraZoom(clampedValue, duration));
            }
        }
        #endregion


        #region 动画协程
        /// <summary>
        /// 平滑移动目标位置
        /// </summary>
        private System.Collections.IEnumerator AnimateTargetPos(Vector3 targetWorldPos, float duration)
        {
            Vector3 startPos = targetPos.position;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                targetPos.position = Vector3.Lerp(startPos, targetWorldPos, t);
                yield return null;
            }
            targetPos.position = targetWorldPos;
        }

        /// <summary>
        /// 平滑旋转摄像机视角
        /// </summary>
        private System.Collections.IEnumerator AnimateCameraView(float targetX, float targetY, float duration)
        {
            float startX = rotX;
            float startY = rotY;
            targetX = ClampAngle(targetX, minRotX, maxRotX);
            targetY = Mathf.Clamp(targetY, minRotY, maxRotY);
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                rotX = Mathf.LerpAngle(startX, targetX, t);
                rotY = Mathf.Lerp(startY, targetY, t);
                Quaternion addRot = Quaternion.Euler(0f, rotX, 0f);
                destRot = addRot * Quaternion.Euler(rotY, 0f, 0f);
                csmoCamera.transform.rotation = destRot;
                UpdatePosition();
                yield return null;
            }
            rotX = targetX;
            rotY = targetY;
            Quaternion finalRot = Quaternion.Euler(0f, rotX, 0f) * Quaternion.Euler(rotY, 0f, 0f);
            destRot = finalRot;
            csmoCamera.transform.rotation = destRot;
            UpdatePosition();
        }

        /// <summary>
        /// 平滑缩放摄像机距离
        /// </summary>
        private System.Collections.IEnumerator AnimateCameraZoom(float targetDistance, float duration)
        {
            float clampedTargetDistance = Mathf.Clamp(targetDistance, GetEffectiveMinDistance(), maxDistance);
            float startDistance = distance;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                distance = Mathf.Lerp(startDistance, clampedTargetDistance, t);
                forwardZoomDistance = 0f;
                yield return null;
            }
            distance = clampedTargetDistance;
            forwardZoomDistance = 0f;
            UpdatePosition();
        }
        #endregion

        private float GetEffectiveMinDistance()
        {
            return enableForwardZoomAfterZero ? 0f : minDistance;
        }

        // 辅助：把角度归一化到 -180~180 并在范围内夹取
        private float ClampAngle(float angle, float min, float max)
        {
            angle = Mathf.Repeat(angle + 180f, 360f) - 180f;
            return Mathf.Clamp(angle, min, max);
        }
    }
}