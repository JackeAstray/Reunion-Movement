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
    /// CameraUtil partial part: Public methods + animation (same class, no behavior change)
    /// </summary>
    public partial class CameraUtil
    {
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
            targetPosCts?.Cancel();
            targetPosCts?.Dispose();

            if (duration <= 0f)
            {
                targetPos.position = pos;
            }
            else
            {
                targetPosCts = new CancellationTokenSource();
                AnimateTargetPosAsync(pos, duration, targetPosCts.Token).Forget();
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
            cameraViewCts?.Cancel();
            cameraViewCts?.Dispose();

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
                cameraViewCts = new CancellationTokenSource();
                AnimateCameraViewAsync(x, y, duration, cameraViewCts.Token).Forget();
            }
        }

        /// <summary>
        /// 设置摄像机远近
        /// </summary>
        /// <param name="value">目标距离</param>
        /// <param name="duration">过渡时间（秒），≤0 则瞬间设置</param>
        public void SetCameraZoom(float value, float duration = 0.5f)
        {
            cameraZoomCts?.Cancel();
            cameraZoomCts?.Dispose();

            float clampedValue = Mathf.Clamp(value, GetEffectiveMinDistance(), maxDistance);

            if (duration <= 0f)
            {
                distance = clampedValue;
                UpdatePosition();
            }
            else
            {
                cameraZoomCts = new CancellationTokenSource();
                AnimateCameraZoomAsync(clampedValue, duration, cameraZoomCts.Token).Forget();
            }
        }
        #endregion


        #region 动画（UniTask 零 GC）
        /// <summary>
        /// 平滑移动目标位置
        /// </summary>
        private async UniTaskVoid AnimateTargetPosAsync(Vector3 targetWorldPos, float duration, CancellationToken ct)
        {
            Vector3 startPos = targetPos.position;
            float elapsed = 0f;
            while (elapsed < duration && !ct.IsCancellationRequested)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                targetPos.position = Vector3.Lerp(startPos, targetWorldPos, t);
                await UniTask.Yield(PlayerLoopTiming.Update);
            }
            if (!ct.IsCancellationRequested)
                targetPos.position = targetWorldPos;
        }

        /// <summary>
        /// 平滑旋转摄像机视角
        /// </summary>
        private async UniTaskVoid AnimateCameraViewAsync(float targetX, float targetY, float duration, CancellationToken ct)
        {
            float startX = rotX;
            float startY = rotY;
            targetX = ClampAngle(targetX, minRotX, maxRotX);
            targetY = Mathf.Clamp(targetY, minRotY, maxRotY);
            float elapsed = 0f;
            while (elapsed < duration && !ct.IsCancellationRequested)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                rotX = Mathf.LerpAngle(startX, targetX, t);
                rotY = Mathf.Lerp(startY, targetY, t);
                Quaternion addRot = Quaternion.Euler(0f, rotX, 0f);
                destRot = addRot * Quaternion.Euler(rotY, 0f, 0f);
                csmoCamera.transform.rotation = destRot;
                UpdatePosition();
                await UniTask.Yield(PlayerLoopTiming.Update);
            }
            if (ct.IsCancellationRequested) return;
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
        private async UniTaskVoid AnimateCameraZoomAsync(float targetDistance, float duration, CancellationToken ct)
        {
            float clampedTargetDistance = Mathf.Clamp(targetDistance, GetEffectiveMinDistance(), maxDistance);
            float startDistance = distance;
            float elapsed = 0f;
            while (elapsed < duration && !ct.IsCancellationRequested)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                distance = Mathf.Lerp(startDistance, clampedTargetDistance, t);
                await UniTask.Yield(PlayerLoopTiming.Update);
            }
            if (ct.IsCancellationRequested) return;
            distance = clampedTargetDistance;
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
