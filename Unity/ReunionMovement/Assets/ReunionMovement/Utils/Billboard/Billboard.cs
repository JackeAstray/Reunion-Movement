using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ReunionMovement.Common.Util
{
    /// <summary>
    /// 广告牌
    /// </summary>
    //[ExecuteAlways]
    public class Billboard : MonoBehaviour
    {
        public enum BillboardType
        {
            Mode1, //和摄像机保持一个方向和角度
            Mode2, //Z轴朝向摄像机，但角度一直为0
            Mode3, //Z轴朝向摄像机
        }

        public Transform targetTF;
        public BillboardType billboardType = BillboardType.Mode1;
        Quaternion originalRotation;
        private float lastErrorLogTime = -999f;
        private float lastCameraRetryTime = -999f;
        private const float CameraRetryInterval = 2f; // Camera.main 重试间隔

        void Start()
        {
            if (targetTF == null)
            {
                var cam = Camera.main;
                if (cam != null)
                    targetTF = cam.transform;
            }
            originalRotation = transform.rotation;
        }

        void Update()
        {
            // 如果目标引用丢失，限流重试获取主相机（Camera.main 内部是 FindGameObjectsWithTag，极慢）
            if (targetTF == null && Time.time - lastCameraRetryTime > CameraRetryInterval)
            {
                lastCameraRetryTime = Time.time;
                var cam = Camera.main;
                if (cam != null)
                    targetTF = cam.transform;
            }

            if (targetTF == null)
            {
                // 每 5 秒最多记录一次，避免每帧刷屏
                if (Time.time - lastErrorLogTime > 5f)
                {
                    lastErrorLogTime = Time.time;
                    Log.Error("Billboard 目标不存在，请查找原因！");
                }
                return;
            }

            switch (billboardType)
            {
                case BillboardType.Mode1:
                    transform.rotation = targetTF.rotation * originalRotation;
                    break;
                case BillboardType.Mode2:
                    Vector3 v = targetTF.position - transform.position;
                    v.x = v.z = 0.0f;
                    transform.LookAt(targetTF.position - v);
                    break;
                case BillboardType.Mode3:
                    transform.LookAt(targetTF.position);
                    break;
            }
        }
    }
}