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
    /// CameraUtil partial part: Runtime (same class, no behavior change)
    /// </summary>
    public partial class CameraUtil
    {

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

            // 掩码为 0 时 Physics.Raycast 永不命中，HandleMouseClick 形同虚设且无任何提示
            if (layerMask.value == 0)
            {
                Log.Warning("CameraUtil layerMask 为 0：射线检测不会命中任何对象，请在 Inspector 配置 layerMask");
            }

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
                Log.Error("CameraUtil: 未找到可用 Camera 组件。", this);
                enabled = false;
                return;
            }

            if (targetPos == null)
            {
                Log.Error("CameraUtil: 未找到 Target 目标对象。", this);
                enabled = false;
                return;
            }

            // 引用计数必须在全部校验通过后才增加：失败路径 enabled=false 且组件可能不被销毁，
            // 提前计数会导致 OnDestroy 永不执行、refCount 永久 +1、EnhancedTouchSupport 永不关闭
            if (enhancedTouchRefCount == 0)
            {
                EnhancedTouchSupport.Enable();
            }
            enhancedTouchRefCount++;

            raycastBase = new RaycastBase(layerMask, csmoCamera);
            distance = Mathf.Clamp(distance, GetEffectiveMinDistance(), maxDistance);
            currentDistance = distance;
            UpdatePosition();
        }

        private void OnDestroy()
        {
            // 取消在途动画：否则销毁后异步循环继续每帧访问已销毁对象，持续刷 MissingReferenceException
            targetPosCts?.Cancel();
            cameraViewCts?.Cancel();
            cameraZoomCts?.Cancel();
            targetPosCts?.Dispose();
            cameraViewCts?.Dispose();
            cameraZoomCts?.Dispose();
            targetPosCts = null;
            cameraViewCts = null;
            cameraZoomCts = null;

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

            // 鼠标设备热插拔/启动时无鼠标后接入：缓存失效时每帧补取（InputSystem 引用极轻量）
            if (mouse == null)
            {
                mouse = Mouse.current;
            }

            if (checkPointerOverUI && IsPointerOverUI())
            {
                return;
            }

            UpdateTwoFingerGestureState();
            HandleCameraMovement();
            HandleCameraRotation();
            HandleCameraZoom();
            HandleAutoRotation();
            HandleMouseClick();
            // 仅当本帧未因旋转/缩放调用过 UpdatePosition 时兜底执行：
            // OrbitCamera/SetZoom 内部已更新位置，重复执行等于每帧两次 Physics.Raycast 遮挡检测
            if (!positionUpdatedThisFrame)
            {
                UpdatePosition();
            }
            positionUpdatedThisFrame = false;
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
        /// 更新双指手势状态（平移/缩放互斥，平移优先）
        /// </summary>
        private void UpdateTwoFingerGestureState()
        {
            hasTwoFingerTouchGesture = false;
            isTwoFingerPanGesture = false;
            twoFingerPanDelta = Vector2.zero;
            twoFingerPinchDelta = 0f;

            if (Touch.activeTouches.Count != 2)
            {
                return;
            }

            var t0 = Touch.activeTouches[0];
            var t1 = Touch.activeTouches[1];
            if (t0.phase != UnityEngine.InputSystem.TouchPhase.Moved && t1.phase != UnityEngine.InputSystem.TouchPhase.Moved)
            {
                return;
            }

            hasTwoFingerTouchGesture = true;

            Vector2 delta0 = t0.delta;
            Vector2 delta1 = t1.delta;
            twoFingerPanDelta = (delta0 + delta1) * 0.5f;

            float prevDist = (t0.screenPosition - delta0 - (t1.screenPosition - delta1)).magnitude;
            float currDist = (t0.screenPosition - t1.screenPosition).magnitude;
            twoFingerPinchDelta = currDist - prevDist;

            float panAmount = twoFingerPanDelta.magnitude;
            float zoomAmount = Mathf.Abs(twoFingerPinchDelta);

            if (panAmount <= twoFingerGestureEpsilon && zoomAmount <= twoFingerGestureEpsilon)
            {
                return;
            }

            if (panAmount > twoFingerGestureEpsilon && zoomAmount <= twoFingerGestureEpsilon)
            {
                isTwoFingerPanGesture = true;
                return;
            }

            if (panAmount <= twoFingerGestureEpsilon && zoomAmount > twoFingerGestureEpsilon)
            {
                return;
            }

            float directionSimilarity = Vector2.Dot(delta0.normalized, delta1.normalized);
            isTwoFingerPanGesture = directionSimilarity >= 0f || panAmount >= zoomAmount;
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

            // 触摸双指拖动（优先级高于双指缩放）
            if (hasTwoFingerTouchGesture && isTwoFingerPanGesture)
            {
                MoveCamera(twoFingerPanDelta.x, twoFingerPanDelta.y);
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

            // 触摸双指缩放（当判定为平移时不触发）
            if (hasTwoFingerTouchGesture && !isTwoFingerPanGesture)
            {
                SetZoom(-twoFingerPinchDelta * 0.05f); // 缩放灵敏度可调
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
                    Log.Debug("CameraUtil: Hit {0} at {1}", hitInfo.collider.gameObject.name, hitInfo.point);
                }
            }

            // 触摸点击
            foreach (var t in Touch.activeTouches)
            {
                if (t.phase == UnityEngine.InputSystem.TouchPhase.Began)
                {
                    if (raycastBase.CastRayFromScreenPoint(t.screenPosition, out RaycastHit hitInfo))
                    {
                        Log.Debug("CameraUtil: Hit {0} at {1}", hitInfo.collider.gameObject.name, hitInfo.point);
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
            positionUpdatedThisFrame = true;
        }

        /// <summary>
        /// 更新摄像机位置
        /// </summary>
        private void UpdatePosition()
        {
            float minZoomDistance = GetEffectiveMinDistance();
            distance = Mathf.Clamp(distance, minZoomDistance, maxDistance);
            offsetDistance = Mathf.MoveTowards(offsetDistance, distance, Time.deltaTime * zoomSpeed);

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
                        currentDistance = Mathf.Clamp(hit.distance - obstructionOffset, minZoomDistance, desiredDistance);
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
                    }
                }
                else if (delta > 0f)
                {
                    // 缩小：直接增加距离，退出前进模式，目标点位置不变
                    distance = Mathf.Clamp(distance + delta, minZoomDistance, maxDistance);
                }
            }
            else
            {
                distance = Mathf.Clamp(distance + delta, minZoomDistance, maxDistance);
            }

            UpdatePosition();
            positionUpdatedThisFrame = true;
        }

    }
}
