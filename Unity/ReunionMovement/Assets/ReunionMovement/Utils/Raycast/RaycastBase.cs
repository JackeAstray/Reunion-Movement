using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace ReunionMovement.Common.Util
{
    /// <summary>
    /// 射线-基类
    /// </summary>
    public class RaycastBase
    {
        // 层
        private LayerMask layerMask;
        // 摄像机
        private Camera camera;

        /// <summary>
        /// 构造射线基类，指定单个射线检测层和摄像机。
        /// </summary>
        /// <param name="layerName">用于射线检测的层名称。</param>
        /// <param name="camera">用于发射射线的摄像机，默认为主摄像机（Camera.main）。</param>
        public RaycastBase(string layerName, Camera camera = null)
        {
            layerMask = 1 << LayerMask.NameToLayer(layerName);
            this.camera = camera ?? Camera.main;
        }


        /// <summary>
        /// 构造射线基类，指定多个射线检测层和摄像机。
        /// </summary>
        /// <param name="layerNames">用于射线检测的层名称数组。</param>
        /// <param name="camera">用于发射射线的摄像机，默认为主摄像机（Camera.main）。</param>
        public RaycastBase(string[] layerNames, Camera camera = null)
        {
            layerMask = 0;
            foreach (var name in layerNames)
            {
                layerMask |= 1 << LayerMask.NameToLayer(name);
            }
            this.camera = camera ?? Camera.main;
        }

        /// <summary>
        /// 构造射线基类，指定射线检测层和摄像机。
        /// </summary>
        /// <param name="layerMask"></param>
        /// <param name="camera"></param>
        public RaycastBase(LayerMask layerMask, Camera camera)
        {
            this.layerMask = layerMask;
            this.camera = camera;
        }

        /// <summary>
        /// 从屏幕点发射射线
        /// </summary>
        /// <param name="screenPoint"></param>
        /// <param name="hitInfo"></param>
        /// <param name="distance"></param>
        /// <returns></returns>
        public bool CastRayFromScreenPoint(Vector2 screenPoint, out RaycastHit hitInfo, float distance = Mathf.Infinity)
        {
            Ray ray = camera.ScreenPointToRay(screenPoint);
            return Physics.Raycast(ray, out hitInfo, distance, layerMask);
        }

        /// <summary>
        /// 设置摄像机
        /// </summary>
        /// <param name="camera"></param>
        public void SetCamera(Camera camera)
        {
            this.camera = camera ?? Camera.main;
        }

        /// <summary>
        /// 设置射线层
        /// </summary>
        /// <param name="layerNames"></param>
        public void SetLayerNames(params string[] layerNames)
        {
            layerMask = 0;
            foreach (var name in layerNames)
            {
                layerMask |= 1 << LayerMask.NameToLayer(name);
            }
        }
    }
}
