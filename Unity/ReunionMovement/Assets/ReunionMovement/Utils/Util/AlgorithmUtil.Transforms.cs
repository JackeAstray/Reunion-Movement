using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ReunionMovement.Core;
using UnityEngine;
using UnityEngine.UI;

namespace ReunionMovement.Common.Util
{
    /// <summary>
    /// 算法工具类
    /// </summary>
    /// <summary>
    /// AlgorithmUtil 拆分部分（partial class，与其余 *.cs 同属一个静态类，调用方式不变）
    /// </summary>
    public static partial class AlgorithmUtil
    {
        #region Transform
        /// <summary>
        /// 使指定的多个GameObject成为子节点
        /// </summary>
        /// <param name="transform"></param>
        /// <param name="children"></param>
        public static void AddChildren(this Transform transform, GameObject[] children)
        {
            foreach (var child in children)
            {
                child.transform.parent = transform;
            }
        }

        /// <summary>
        /// 使指定的多个Component成为子节点
        /// </summary>
        /// <param name="transform"></param>
        /// <param name="children"></param>
        public static void AddChildren(this Transform transform, Component[] children)
        {
            foreach (var child in children)
            {
                child.transform.parent = transform;
            }
        }

        /// <summary>
        /// 重置子节点位置
        /// </summary>
        /// <param name="transform">父对象</param>
        /// <param name="recursive">父对象一起重置</param>
        public static void ResetChildPositions(this Transform transform, bool recursive = false)
        {
            foreach (Transform child in transform)
            {
                child.localPosition = Vector3.zero;

                if (recursive)
                {
                    child.ResetChildPositions(true);
                }
            }
        }

        /// <summary>
        /// 设置子层级的layer
        /// </summary>
        /// <param name="transform"></param>
        /// <param name="layerName"></param>
        /// <param name="recursive"></param>
        public static void SetChildLayers(this Transform transform, string layerName, bool recursive = false)
        {
            var layer = LayerMask.NameToLayer(layerName);

            foreach (Transform child in transform)
            {
                child.gameObject.layer = layer;

                if (recursive)
                {
                    child.SetChildLayers(layerName, true);
                }
            }
        }

        /// <summary>
        /// 设置XYZ位置
        /// </summary>
        /// <param name="transform"></param>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="z"></param>
        public static void SetXYZ(this Transform transform, float x, float y, float z)
        {
            transform.position = new Vector3(x, y, z);
        }

        /// <summary>
        /// 设置XYZ位置
        /// </summary>
        /// <param name="transform"></param>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="z"></param>
        public static void SetLocalXYZ(this Transform transform, float x, float y, float z)
        {
            transform.localPosition = new Vector3(x, y, z);
        }

        /// <summary>
        /// 设置X、Y和Y缩放
        /// </summary>
        public static void SetScaleXYZ(this Transform transform, float x, float y, float z)
        {
            transform.localScale = new Vector3(x, y, z);
        }

        /// <summary>
        /// 在X、Y和Z方向上缩放
        /// </summary>
        public static void ScaleByXYZ(this Transform transform, float x, float y, float z)
        {
            transform.localScale = new Vector3(x, y, z);
        }

        /// <summary>
        /// 在X、Y和Z方向上缩放
        /// </summary>
        public static void ScaleByXYZ(this Transform transform, float r)
        {
            transform.ScaleByXYZ(r, r, r);
        }

        /// <summary>
        /// 设置X轴旋转
        /// </summary>
        public static void SetRotationX(this Transform transform, float angle)
        {
            transform.eulerAngles = new Vector3(angle, 0, 0);
        }

        /// <summary>
        /// 设置Y轴旋转
        /// </summary>
        public static void SetRotationY(this Transform transform, float angle)
        {
            transform.eulerAngles = new Vector3(0, angle, 0);
        }

        /// <summary>
        /// 设置Z轴旋转
        /// </summary>
        public static void SetRotationZ(this Transform transform, float angle)
        {
            transform.eulerAngles = new Vector3(0, 0, angle);
        }

        /// <summary>
        /// 设置本地X轴旋转
        /// </summary>
        public static void SetLocalRotationX(this Transform transform, float angle)
        {
            transform.localRotation = Quaternion.Euler(new Vector3(angle, 0, 0));
        }

        /// <summary>
        /// 设置本地Y轴旋转
        /// </summary>
        public static void SetLocalRotationY(this Transform transform, float angle)
        {
            transform.localRotation = Quaternion.Euler(new Vector3(0, angle, 0));
        }

        /// <summary>
        /// 设置本地Z轴旋转
        /// </summary>
        public static void SetLocalRotationZ(this Transform transform, float angle)
        {
            transform.localRotation = Quaternion.Euler(new Vector3(0, 0, angle));
        }

        /// <summary>
        /// 重置位置
        /// </summary>
        public static void ResetPosition(this Transform transform)
        {
            transform.position = Vector3.zero;
        }

        /// <summary>
        /// 重置位置
        /// </summary>
        public static void ResetLocalPosition(this Transform transform)
        {
            transform.localPosition = Vector3.zero;
        }

        /// <summary>
        /// 重置旋转
        /// </summary>
        /// <param name="transform"></param>
        public static void ResetRotation(this Transform transform)
        {
            transform.rotation = Quaternion.identity;
        }

        /// <summary>
        /// 重置旋转
        /// </summary>

        public static void ResetLocalRotation(this Transform transform)
        {
            transform.localRotation = Quaternion.identity;
        }

        /// <summary>
        /// 重置本地位置/旋转/缩放
        /// </summary>
        /// <param name="transform"></param>
        public static void ResetLocal(this Transform transform)
        {
            transform.ResetLocalRotation();
            transform.ResetLocalPosition();
            transform.ResetScale();

        }

        /// <summary>
        /// 重置位置/旋转/缩放
        /// </summary>
        /// <param name="transform"></param>
        public static void Reset(this Transform transform)
        {
            transform.ResetRotation();
            transform.ResetPosition();
            transform.ResetScale();
        }

        /// <summary>
        /// 重置缩放
        /// </summary>
        /// <param name="transform"></param>
        public static void ResetScale(this Transform transform)
        {
            transform.localScale = Vector3.one;
        }

        /// <summary>
        /// 计算该物体的位置。无论它位于顶部还是底部。分别为-1和1。
        /// </summary>
        /// <returns></returns>
        public static int CloserEdge(this Transform transform, Camera camera, int width, int height)
        {
            // 世界坐标转换为屏幕坐标
            var worldPointTop = camera.ScreenToWorldPoint(new Vector3(width / 2, height));
            var worldPointBot = camera.ScreenToWorldPoint(new Vector3(width / 2, 0));
            // 计算距离
            var deltaTop = Vector2.Distance(worldPointTop, transform.position);
            var deltaBottom = Vector2.Distance(worldPointBot, transform.position);

            return deltaBottom <= deltaTop ? 1 : -1;
        }

        /// <summary>
        /// 搜索子物体组件
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="tf"></param>
        /// <param name="objName"></param>
        /// <returns></returns>
        public static T Get<T>(this Transform tf, string objName) where T : Component
        {
            var sub = tf?.Find(objName);
            return sub?.GetComponent<T>();
        }

        /// <summary>
        /// 清除所有子节点
        /// </summary>
        /// <param name="tf"></param>
        public static void ClearChild(this Transform tf)
        {
            if (tf == null) return;
            for (int i = tf.childCount - 1; i >= 0; i--)
            {
                GameObject.Destroy(tf.GetChild(i).gameObject);
            }
        }

        /// <summary>
        /// 将位置旋转缩放清零
        /// </summary>
        /// <param name="tf"></param>
        public static void ResetLocalTransform(this Transform tf)
        {
            tf.localPosition = Vector3.zero;
            tf.localRotation = Quaternion.identity;
            tf.localScale = Vector3.one;
        }

        /// <summary>
        /// 查找子项
        /// </summary>
        /// <param name="findTrans"></param>
        /// <param name="objName"></param>
        /// <param name="check_visible">检查可见性</param>
        /// <param name="raise_error">抛出错误</param>
        /// <returns></returns>
        public static Transform Child(this Transform findTrans, string objName, bool check_visible = false, bool raise_error = true)
        {
            if (!findTrans)
            {
                if (raise_error)
                {
                    Log.Error("查找失败. findTrans是空的!");
                }
                return null;
            }

            if (string.IsNullOrEmpty(objName))
            {
                return null;
            }
            // 如果需要检查可见性，但是当前物体不可见
            if (check_visible && !findTrans.gameObject.activeInHierarchy)
            {
                return null;
            }
            if (objName == ".")
            {
                return findTrans;
            }

            var ids = objName.Split('/');

            foreach (var id1 in ids)
            {
                findTrans = ChildDirect(findTrans, id1, check_visible);

                if (findTrans == null)
                {
                    // 如果需要抛出错误
                    if (raise_error)
                    {
                        Log.Error("查找子项失败, id:{0} ,parent={1}", objName, id1);
                    }
                    break;
                }
            }

            return findTrans;
        }

        /// <summary>
        /// 查找子项
        /// </summary>
        /// <param name="trans"></param>
        /// <param name="objName"></param>
        /// <param name="check_visible"></param>
        /// <returns></returns>
        private static Transform ChildDirect(this Transform trans, string objName, bool check_visible)
        {
            if (trans == null || string.IsNullOrEmpty(objName))
            {
                return null;
            }

            var child = trans.Find(objName);
            if (child != null && (!check_visible || child.gameObject.activeInHierarchy))
            {
                return child;
            }

            if (!check_visible)
            {
                // 如果不检查可见性且未找到匹配项，直接返回null
                return null;
            }

            foreach (Transform t in trans)
            {
                if (t.gameObject.activeInHierarchy)
                {
                    var found = ChildDirect(t, objName, true);
                    if (found != null)
                    {
                        return found;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// 获取从父节点到自己的完整路径
        /// </summary>
        /// <param name="transform"></param>
        /// <returns></returns>
        public static string GetRootPathName(this Transform transform)
        {
            if (transform == null)
            {
                return string.Empty;
            }

            StringBuilder path = new StringBuilder();
            BuildPath(transform, ref path);
            return path.ToString();
        }

        /// <summary>
        /// 递归构建Transform的完整路径名称
        /// </summary>
        /// <param name="current"></param>
        /// <param name="path"></param>
        private static void BuildPath(Transform current, ref StringBuilder path)
        {
            if (current.parent != null)
            {
                BuildPath(current.parent, ref path);
                path.Append("/");
            }
            path.Append(current.name);
        }

        /// <summary>
        /// 旋转物体，处理万向锁
        /// </summary>
        /// <param name="transform"></param>
        /// <param name="v3"></param>
        /// <param name="order"></param>
        /// <param name="algorithmType"></param>
        public static void RotateXYZ(this Transform transform, Vector3 v3, XYZOrder order, XYZAlgorithmType algorithmType = XYZAlgorithmType.Quaternion)
        {
            if (algorithmType == XYZAlgorithmType.Quaternion)
            {
                transform.rotation = RotateXYZ_Quaternion(v3, order);
            }
            else
            {
                transform.rotation = RotateXYZ_Matrix4x4(v3, order);
            }
        }

        /// <summary>
        /// 旋转物体，处理万向锁 采用四元数计算
        /// </summary>
        /// <param name="v3"></param>
        /// <param name="order"></param>
        /// <returns></returns>
        public static Quaternion RotateXYZ_Quaternion(Vector3 v3, XYZOrder order)
        {
            Quaternion xRot = Quaternion.AngleAxis(v3.x, Vector3.right);
            Quaternion yRot = Quaternion.AngleAxis(v3.y, Vector3.up);
            Quaternion zRot = Quaternion.AngleAxis(v3.z, Vector3.forward);

            Quaternion combinedRotation;

            switch (order)
            {
                case XYZOrder.XYZ:
                    combinedRotation = xRot * yRot * zRot;
                    break;
                case XYZOrder.XZY:
                    combinedRotation = xRot * zRot * yRot;
                    break;
                case XYZOrder.YXZ:
                    combinedRotation = yRot * xRot * zRot;
                    break;
                case XYZOrder.YZX:
                    combinedRotation = yRot * zRot * xRot;
                    break;
                case XYZOrder.ZXY:
                    combinedRotation = zRot * xRot * yRot;
                    break;
                case XYZOrder.ZYX:
                    combinedRotation = zRot * yRot * xRot;
                    break;
                // 与unity inspector中的顺序一致
                default:
                    combinedRotation = yRot * xRot * zRot;
                    break;
            }

            return combinedRotation;
        }

        /// <summary>
        /// 旋转物体，处理万向锁 采用矩阵计算
        /// </summary>
        /// <param name="transform"></param>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="z"></param>
        public static Quaternion RotateXYZ_Matrix4x4(Vector3 v3, XYZOrder order)
        {
            Matrix4x4 xRot = RotXMat(v3.x * Mathf.Deg2Rad);
            Matrix4x4 yRot = RotYMat(v3.y * Mathf.Deg2Rad);
            Matrix4x4 zRot = RotZMat(v3.z * Mathf.Deg2Rad);

            Matrix4x4 combinedRotation;

            switch (order)
            {
                case XYZOrder.XYZ:
                    combinedRotation = xRot * yRot * zRot;
                    break;
                case XYZOrder.XZY:
                    combinedRotation = xRot * zRot * yRot;
                    break;
                case XYZOrder.YXZ:
                    combinedRotation = yRot * xRot * zRot;
                    break;
                case XYZOrder.YZX:
                    combinedRotation = yRot * zRot * xRot;
                    break;
                case XYZOrder.ZXY:
                    combinedRotation = zRot * xRot * yRot;
                    break;
                case XYZOrder.ZYX:
                    combinedRotation = zRot * yRot * xRot;
                    break;
                // 与unity inspector中的顺序一致
                default:
                    combinedRotation = yRot * xRot * zRot;
                    break;
            }

            return combinedRotation.rotation;
        }

        static Matrix4x4 RotXMat(float angle)
        {
            Matrix4x4 rxmat = new Matrix4x4();
            rxmat.SetRow(0, new Vector4(1f, 0f, 0f, 0f));
            rxmat.SetRow(1, new Vector4(0f, Mathf.Cos(angle), -Mathf.Sin(angle), 0f));
            rxmat.SetRow(2, new Vector4(0f, Mathf.Sin(angle), Mathf.Cos(angle), 0f));
            rxmat.SetRow(3, new Vector4(0f, 0f, 0f, 1f));

            return rxmat;
        }

        static Matrix4x4 RotYMat(float angle)
        {
            Matrix4x4 rymat = new Matrix4x4();
            rymat.SetRow(0, new Vector4(Mathf.Cos(angle), 0f, Mathf.Sin(angle), 0f));
            rymat.SetRow(1, new Vector4(0f, 1f, 0f, 0f));
            rymat.SetRow(2, new Vector4(-Mathf.Sin(angle), 0f, Mathf.Cos(angle), 0f));
            rymat.SetRow(3, new Vector4(0f, 0f, 0f, 1f));

            return rymat;
        }

        static Matrix4x4 RotZMat(float angle)
        {
            Matrix4x4 rzmat = new Matrix4x4();
            rzmat.SetRow(0, new Vector4(Mathf.Cos(angle), -Mathf.Sin(angle), 0f, 0f));
            rzmat.SetRow(1, new Vector4(Mathf.Sin(angle), Mathf.Cos(angle), 0f, 0f));
            rzmat.SetRow(2, new Vector4(0f, 0f, 1f, 0f));
            rzmat.SetRow(3, new Vector4(0f, 0f, 0f, 1f));

            return rzmat;
        }
        #endregion


        #region RectTransform
        /// <summary>
        /// 设置RectTransform的锚点位置X
        /// </summary>
        /// <param name="rectTransform"></param>
        /// <param name="x"></param>
        public static void SetAnchoredPositionX(this RectTransform rectTransform, float x)
        {
            var pos = rectTransform.anchoredPosition;
            pos.x = x;
            rectTransform.anchoredPosition = pos;
        }
        /// <summary>
        /// 设置RectTransform的锚点位置Y
        /// </summary>
        /// <param name="rectTransform"></param>
        /// <param name="y"></param>
        public static void SetAnchoredPositionY(this RectTransform rectTransform, float y)
        {
            var pos = rectTransform.anchoredPosition;
            pos.y = y;
            rectTransform.anchoredPosition = pos;
        }
        /// <summary>
        /// 设置RectTransform的锚点位置Z
        /// </summary>
        /// <param name="rectTransform"></param>
        /// <param name="z"></param>
        public static void SetAnchoredPositionZ(this RectTransform rectTransform, float z)
        {
            var pos = rectTransform.anchoredPosition3D;
            pos.z = z;
            rectTransform.anchoredPosition3D = pos;
        }
        /// <summary>
        /// 设置RectTransform的大小X
        /// </summary>
        /// <param name="rectTransform"></param>
        /// <param name="x"></param>
        public static void SetSizeDeltaX(this RectTransform rectTransform, float x)
        {
            var size = rectTransform.sizeDelta;
            size.x = x;
            rectTransform.sizeDelta = size;
        }
        /// <summary>
        /// 设置RectTransform的大小Y
        /// </summary>
        /// <param name="rectTransform"></param>
        /// <param name="y"></param>
        public static void SetSizeDeltaY(this RectTransform rectTransform, float y)
        {
            var size = rectTransform.sizeDelta;
            size.y = y;
            rectTransform.sizeDelta = size;
        }
        /// <summary>
        /// 设置RectTransform的锚点最小位置X
        /// </summary>
        /// <param name="rectTransform"></param>
        /// <param name="x"></param>
        public static void SetAnchorMinX(this RectTransform rectTransform, float x)
        {
            var anchor = rectTransform.anchorMin;
            anchor.x = x;
            rectTransform.anchorMin = anchor;
        }
        /// <summary>
        /// 设置RectTransform的锚点最小位置Y
        /// </summary>
        /// <param name="rectTransform"></param>
        /// <param name="y"></param>
        public static void SetAnchorMinY(this RectTransform rectTransform, float y)
        {
            var anchor = rectTransform.anchorMin;
            anchor.y = y;
            rectTransform.anchorMin = anchor;
        }
        /// <summary>
        /// 设置RectTransform的锚点最大位置X
        /// </summary>
        /// <param name="rectTransform"></param>
        /// <param name="x"></param>
        public static void SetAnchorMaxX(this RectTransform rectTransform, float x)
        {
            var anchor = rectTransform.anchorMax;
            anchor.x = x;
            rectTransform.anchorMax = anchor;
        }
        /// <summary>
        /// 设置RectTransform的锚点最大位置Y
        /// </summary>
        /// <param name="rectTransform"></param>
        /// <param name="y"></param>
        public static void SetAnchorMaxY(this RectTransform rectTransform, float y)
        {
            var anchor = rectTransform.anchorMax;
            anchor.y = y;
            rectTransform.anchorMax = anchor;
        }
        /// <summary>
        /// 设置RectTransform的Pivot X
        /// </summary>
        /// <param name="rectTransform"></param>
        /// <param name="x"></param>
        public static void SetPivotX(this RectTransform rectTransform, float x)
        {
            var pivot = rectTransform.pivot;
            pivot.x = x;
            rectTransform.pivot = pivot;
        }

        /// <summary>
        /// 设置RectTransform的Pivot Y
        /// </summary>
        /// <param name="rectTransform"></param>
        /// <param name="y"></param>
        public static void SetPivotY(this RectTransform rectTransform, float y)
        {
            var pivot = rectTransform.pivot;
            pivot.y = y;
            rectTransform.pivot = pivot;
        }

        /// <summary>
        /// 设置锚点
        /// </summary>
        /// <param name="rect"></param>
        /// <param name="type"></param>
        public static void SetAnchor(this RectTransform rect, AnchorType type)
        {
            if (rect == null)
                return;
            var size = rect.sizeDelta;
            //left,right对应x,top,bottom对应Y
            switch (type)
            {
                case AnchorType.TopRight:
                    rect.pivot = new Vector2(1, 1);
                    rect.anchorMin = new Vector2(1, 1);
                    rect.anchorMax = new Vector2(1, 1);
                    rect.anchoredPosition = Vector2.zero;
                    break;
                case AnchorType.TopLeft:
                    rect.pivot = new Vector2(0.5f, 1);
                    rect.anchorMin = new Vector2(0, 1);
                    rect.anchorMax = new Vector2(0, 1);
                    rect.anchoredPosition = Vector2.zero;
                    break;
                case AnchorType.Stretch:
                    rect.pivot = new Vector2(0.5f, 0.5f);
                    rect.anchorMin = Vector2.zero;
                    rect.anchorMax = Vector2.one;
                    rect.anchoredPosition = Vector2.zero;
                    rect.sizeDelta = Vector2.zero;
                    break;
                case AnchorType.StretchTop:
                    rect.pivot = new Vector2(0.5f, 1);
                    rect.anchorMin = new Vector2(0, 1);
                    rect.anchorMax = new Vector2(1, 1);
                    rect.anchoredPosition = Vector2.zero;
                    rect.sizeDelta = new Vector2(0, rect.sizeDelta.y);
                    break;
                case AnchorType.StretchBottom:
                    rect.pivot = new Vector2(0.5f, 0);
                    rect.anchorMin = new Vector2(0, 0);
                    rect.anchorMax = new Vector2(1, 0);
                    rect.anchoredPosition = Vector2.zero;
                    rect.sizeDelta = new Vector2(0, rect.sizeDelta.y);
                    break;
                case AnchorType.StretchLeft:
                    rect.pivot = new Vector2(0, 0.5f);
                    rect.anchorMin = new Vector2(0, 0);
                    rect.anchorMax = new Vector2(0, 1);
                    rect.anchoredPosition = Vector2.zero;
                    rect.sizeDelta = new Vector2(rect.sizeDelta.x, 0);
                    break;
                case AnchorType.StretchRight:
                    rect.pivot = new Vector2(1, 0.5f);
                    rect.anchorMin = new Vector2(1, 0);
                    rect.anchorMax = new Vector2(1, 1);
                    rect.anchoredPosition = Vector2.zero;
                    rect.sizeDelta = new Vector2(rect.sizeDelta.x, 0);
                    break;
                default:
                    Log.Debug("未知的锚点类型");
                    break;
            }
        }
        #endregion


    }
}
