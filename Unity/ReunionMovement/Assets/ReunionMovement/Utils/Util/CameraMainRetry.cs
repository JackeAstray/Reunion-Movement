using UnityEngine;

namespace ReunionMovement.Common.Util
{
    /// <summary>
    /// Camera.main 限流重试 —— Camera.main 内部是 FindGameObjectsWithTag（极慢），
    /// 相机引用丢失时按固定间隔重试而非每帧查找。Arrow/Billboard 共用（消除重复实现）。
    /// </summary>
    public struct CameraMainRetry
    {
        private float lastRetryTime;
        private const float RetryInterval = 2f;

        /// <param name="initialLastRetryTime">初始上次重试时间（传 -999 使首次调用立即生效）</param>
        public CameraMainRetry(float initialLastRetryTime)
        {
            lastRetryTime = initialLastRetryTime;
        }

        /// <summary>尝试获取主相机；未到重试间隔时返回 null 且不触发查找</summary>
        public Camera TryGetCamera()
        {
            if (Time.time - lastRetryTime < RetryInterval) return null;
            lastRetryTime = Time.time;
            return Camera.main;
        }
    }
}
