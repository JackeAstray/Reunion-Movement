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
        #region Component
        /// <summary>
        /// 将一个组件附加到给定组件的游戏对象
        /// </summary>
        /// <param name="component">Component.</param>
        /// <returns>Newly attached component.</returns>
        public static T AddComponent<T>(this Component component) where T : Component
        {
            return component.gameObject.AddComponent<T>();
        }

        /// <summary>
        /// 获取附加到给定组件的游戏对象的组件
        /// 如果没有找到，则会附加一个新的并返回
        /// </summary>
        /// <param name="component">Component.</param>
        /// <returns>Previously or newly attached component.</returns>
        public static T GetOrAddComponent<T>(this Component component) where T : Component
        {
            var existingComponent = component.GetComponent<T>();
            return existingComponent != null ? existingComponent : component.AddComponent<T>();
        }

        /// <summary>
        /// 检查组件的游戏对象是否附加了类型为T的组件
        /// </summary>
        /// <param name="component">Component.</param>
        /// <returns>True when component is attached.</returns>
        public static bool HasComponent<T>(this Component component) where T : Component
        {
            return component.GetComponent<T>() != null;
        }

        /// <summary>
        /// 搜索子物体组件
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="go"></param>
        /// <param name="subnode"></param>
        /// <returns></returns>
        public static T Get<T>(this Component go, string subnode) where T : Component
        {
            var transform = go.transform.Find(subnode);
            return transform != null ? transform.GetComponent<T>() : null;
        }
        #endregion


        #region Vector
        /// <summary>
        /// 计算两个向量之间的夹角
        /// </summary>
        /// <param name="value1"></param>
        /// <param name="value2"></param>
        /// <returns></returns>
        public static float Angle(Vector2 value1, Vector2 value2)
        {
            return Vector2.Angle(value1, value2);
        }

        /// <summary>
        /// 计算两个向量之间的夹角
        /// </summary>
        /// <param name="value1"></param>
        /// <param name="value2"></param>
        /// <returns></returns>
        public static float Angle(Vector3 value1, Vector3 value2)
        {
            return Vector3.Angle(value1, value2);
        }

        /// <summary>
        /// 将角度转换为二维向量
        /// </summary>
        /// <param name="angle"></param>
        /// <returns></returns>
        public static Vector2 AngleToVector2D(float angle)
        {
            float radian = Mathf.Deg2Rad * angle; // 角度转弧度
            return new Vector2(Mathf.Cos(radian), Mathf.Sin(radian)).normalized; // 得到单位向量
        }

        /// <summary>
        /// 获取两个点之间的中间点（百分比0-1）
        /// </summary>
        /// <param name="start">起始点</param>
        /// <param name="end">结束点</param>
        /// <param name="percent">百分比</param>
        /// <returns></returns>
        public static Vector3 GetBetweenPointPercent(Vector3 start, Vector3 end, float percent)
        {
            return Vector3.Lerp(start, end, percent);
        }

        /// <summary>
        /// 获取两个点之间的中间点（距离）
        /// </summary>
        /// <param name="start"></param>
        /// <param name="end"></param>
        /// <param name="distance"></param>
        /// <returns></returns>
        public static Vector3 GetBetweenPointDistance(Vector3 start, Vector3 end, float distance)
        {
            return start + (end - start).normalized * distance;
        }

        /// <summary>
        /// 获取椭圆上某个角度的相对位置
        /// 作用是-已椭圆中心为原点，长轴与X轴重合，短轴与Y轴重合，计算出该角度对应的椭圆上点的坐标。
        /// </summary>
        /// <param name="longHalfAxis"></param>
        /// <param name="shortHalfAxis"></param>
        /// <param name="angle"></param>
        /// <returns></returns>
        public static Vector2 GetRelativePositionOfEllipse(float longHalfAxis, float shortHalfAxis, float angle)
        {
            var rad = angle * Mathf.Deg2Rad; // 弧度
            var newPos = Vector2.right * longHalfAxis * Mathf.Cos(rad) + Vector2.up * shortHalfAxis * Mathf.Sin(rad);
            return newPos;
        }

        /// <summary>
        /// 获得固定位数小数的向量
        /// </summary>
        public static Vector3 Round(this Vector3 value, int decimals)
        {
            value.x = (float)Math.Round(value.x, decimals);
            value.y = (float)Math.Round(value.y, decimals);
            value.z = (float)Math.Round(value.z, decimals);
            return value;
        }

        /// <summary>
        /// 获得固定位数小数的向量
        /// </summary>
        public static Vector2 Round(this Vector2 value, int decimals)
        {
            value.x = (float)Math.Round(value.x, decimals);
            value.y = (float)Math.Round(value.y, decimals);
            return value;
        }

        /// <summary>
        /// 限制一个三维向量在最大值与最小值之间
        /// </summary>
        public static Vector3 Clamp(this Vector3 value, float minX, float minY, float minZ, float maxX, float maxY, float maxZ)
        {
            value.x = Mathf.Clamp(value.x, minX, maxX);
            value.y = Mathf.Clamp(value.y, minY, maxY);
            value.z = Mathf.Clamp(value.z, minZ, maxZ);
            return value;
        }

        /// <summary>
        /// 限制一个二维向量在最大值与最小值之间
        /// </summary>
        /// <param name="value"></param>
        /// <param name="minX"></param>
        /// <param name="minY"></param>
        /// <param name="maxX"></param>
        /// <param name="maxY"></param>
        /// <returns></returns>
        public static Vector2 Clamp(this Vector2 value, float minX, float minY, float maxX, float maxY)
        {
            value.x = Mathf.Clamp(value.x, minX, maxX);
            value.y = Mathf.Clamp(value.y, minY, maxY);
            return value;
        }

        /// <summary>
        /// 计算中心点
        /// </summary>
        /// <param name="Points"></param>
        /// <returns></returns>
        public static Vector3 CalculateCenterPoint(List<Transform> Points)
        {
            if (Points == null || Points.Count == 0)
            {
                return Vector3.zero;
            }

            return Points.Aggregate(Vector3.zero, (acc, p) => acc + p.position) / Points.Count;
        }

        /// <summary>
        /// 获取BoxCollider内的随机位置
        /// </summary>
        /// <param name="collider"></param>
        /// <param name="method"></param>
        /// <returns></returns>
        public static Vector3 GetRandomPositionInBoxCollider(BoxCollider collider, int method = 1)
        {
            return new Vector3(UnityEngine.Random.Range(collider.bounds.min.x, collider.bounds.max.x),
                               UnityEngine.Random.Range(collider.bounds.min.y, collider.bounds.max.y),
                               UnityEngine.Random.Range(collider.bounds.min.z, collider.bounds.max.z));
        }

        /// <summary>
        /// 获取一个球内随机点（整体）
        /// </summary>
        /// <param name="center">中心点</param>
        /// <param name="radius">半径</param>
        /// <returns>球内随机点</returns>
        public static Vector3 GetRandomPointInSphere(Vector3 center, float radius)
        {
            if (radius < 0)
            {
                radius = 0;
            }

            var rndPtr = UnityEngine.Random.insideUnitSphere * radius;
            var rndPos = rndPtr + center;
            return rndPos;
        }

        /// <summary>
        /// 获取一个球内随机点（环带）
        /// </summary>
        /// <param name="center">中心点</param>
        /// <param name="miniRadius">最小半径</param>
        /// <param name="maxRadius">最大半径</param>
        /// <returns>球内随机点</returns>
        public static Vector3 GetRandomPointInSphere(Vector3 center, float miniRadius, float maxRadius)
        {
            if (miniRadius < 0)
            {
                miniRadius = 0;
            }

            if (maxRadius < miniRadius)
            {
                maxRadius = miniRadius;
            }

            var randomRadius = UnityEngine.Random.Range(miniRadius, maxRadius);
            var rndPtr = UnityEngine.Random.insideUnitSphere * randomRadius;
            var rndPos = rndPtr + center;
            return rndPos;
        }

        /// <summary>
        /// 生成一组以startPos为中心、在startDirection垂直方向上等间距排列的点。
        /// 常用于队列、阵型、道路布点等需要横向分布的场景。
        /// </summary>
        /// <param name="startPos"></param>
        /// <param name="startDirection"></param>
        /// <param name="nNum"></param>
        /// <param name="meterInterval"></param>
        /// <returns></returns>
        public static Vector3[] GetParallelPoints(Vector3 startPos, Vector3 startDirection, int nNum, float meterInterval)
        {
            Vector3[] targetPos = new Vector3[nNum];
            Vector3 perpendicularDirection = Quaternion.AngleAxis(90, Vector3.forward) * startDirection.normalized; // 计算垂直方向
            int halfNum = nNum / 2;
            bool isEven = nNum % 2 == 0;

            for (int i = 0; i < nNum; i++)
            {
                int indexOffset = i - halfNum + (isEven ? 1 : 0);
                float distance = indexOffset * meterInterval + (isEven ? meterInterval / 2 : 0);
                targetPos[i] = startPos + perpendicularDirection * distance;
            }

            return targetPos;
        }

        /// <summary>
        /// 计算两条线段的交点
        /// </summary>
        /// <param name="ps1"></param>
        /// <param name="pe1"></param>
        /// <param name="ps2"></param>
        /// <param name="pe2"></param>
        /// <returns></returns>
        public static (bool, Vector3) LineIntersectionPoint(
            Vector3 ps1,
            Vector3 pe1,
            Vector3 ps2,
            Vector3 pe2
        )
        {
            // 快速排斥实验，先排除无交点的情况
            if (Mathf.Min(ps1.x, pe1.x) > Mathf.Max(ps2.x, pe2.x) || Mathf.Max(ps1.x, pe1.x) < Mathf.Min(ps2.x, pe2.x) ||
                Mathf.Min(ps1.y, pe1.y) > Mathf.Max(ps2.y, pe2.y) || Mathf.Max(ps1.y, pe1.y) < Mathf.Min(ps2.y, pe2.y) ||
                Mathf.Min(ps1.z, pe1.z) > Mathf.Max(ps2.z, pe2.z) || Mathf.Max(ps1.z, pe1.z) < Mathf.Min(ps2.z, pe2.z))
            {
                return (false, Vector3.zero);
            }

            Vector3 ab = pe1 - ps1;
            Vector3 cd = pe2 - ps2;
            Vector3 ca = ps1 - ps2;

            // 判断共面
            Vector3 v1 = Vector3.Cross(ca, cd);
            float coplanar = Mathf.Abs(Vector3.Dot(v1, ab));
            if (coplanar > Mathf.Epsilon)
            {
                return (false, Vector3.zero);
            }

            // 判断平行
            Vector3 ab_cd = Vector3.Cross(ab, cd);
            if (ab_cd.sqrMagnitude <= Mathf.Epsilon)
            {
                return (false, Vector3.zero);
            }

            // 跨立试验
            Vector3 ad = pe2 - ps1;
            Vector3 cb = pe1 - ps2;
            float s1 = Vector3.Dot(Vector3.Cross(-ca, ab), Vector3.Cross(ab, ad));
            float s2 = Vector3.Dot(Vector3.Cross(ca, cd), Vector3.Cross(cd, cb));
            if (s1 > 0 && s2 > 0)
            {
                Vector3 v2 = Vector3.Cross(cd, ab);
                float ratio = Vector3.Dot(v1, v2) / v2.sqrMagnitude;
                Vector3 intersectPos = ps1 + ab * ratio;
                return (true, intersectPos);
            }

            return (false, Vector3.zero);
        }

        /// <summary>
        /// 计算两条线段的交点
        /// </summary>
        /// <param name="ps1"></param>
        /// <param name="pe1"></param>
        /// <param name="ps2"></param>
        /// <param name="pe2"></param>
        /// <returns></returns>
        public static (bool, Vector2) LineIntersectionPoint(Vector2 ps1, Vector2 pe1, Vector2 ps2, Vector2 pe2)
        {
            float A1 = pe1.y - ps1.y;
            float B1 = ps1.x - pe1.x;
            float C1 = A1 * ps1.x + B1 * ps1.y;

            float A2 = pe2.y - ps2.y;
            float B2 = ps2.x - pe2.x;
            float C2 = A2 * ps2.x + B2 * ps2.y;

            float delta = A1 * B2 - A2 * B1;
            if (Mathf.Abs(delta) < Mathf.Epsilon)
            {
                // 平行或重合
                return (false, Vector2.zero);
            }

            Vector2 intersectPoint = new Vector2(
                (B2 * C1 - B1 * C2) / delta,
                (A1 * C2 - A2 * C1) / delta
            );

            // 判断交点是否在线段ps1-pe1上
            bool onSeg1 =
                intersectPoint.x >= Mathf.Min(ps1.x, pe1.x) - Mathf.Epsilon &&
                intersectPoint.x <= Mathf.Max(ps1.x, pe1.x) + Mathf.Epsilon &&
                intersectPoint.y >= Mathf.Min(ps1.y, pe1.y) - Mathf.Epsilon &&
                intersectPoint.y <= Mathf.Max(ps1.y, pe1.y) + Mathf.Epsilon;

            // 判断交点是否在线段ps2-pe2上
            bool onSeg2 =
                intersectPoint.x >= Mathf.Min(ps2.x, pe2.x) - Mathf.Epsilon &&
                intersectPoint.x <= Mathf.Max(ps2.x, pe2.x) + Mathf.Epsilon &&
                intersectPoint.y >= Mathf.Min(ps2.y, pe2.y) - Mathf.Epsilon &&
                intersectPoint.y <= Mathf.Max(ps2.y, pe2.y) + Mathf.Epsilon;

            if (onSeg1 && onSeg2)
            {
                return (true, intersectPoint);
            }

            return (false, Vector2.zero);
        }

        /// <summary>
        /// 在某个中心点周围，生成一组等角度分布、距离相同的点，常用于NPC环绕、队形等需求
        /// </summary>
        /// <param name="startDirection">起始方向</param>
        /// <param name="nNum">需要的数量</param>
        /// <param name="pAnchorPos">锚点</param>
        /// <param name="fAngle">角度</param>
        /// <param name="nRadius">半径</param>
        /// <returns></returns>
        public static Vector3[] GetSmartNpcPoints(Vector3 startDirection, int nNum, Vector3 pAnchorPos, float fAngle, float nRadius)
        {
            Vector3[] points = new Vector3[nNum];
            // 每个点之间的角度增量
            float angleIncrement = fAngle / nNum;
            // 用于旋转的四元数
            Quaternion rotation = Quaternion.Euler(0, angleIncrement, 0);
            // 初始方向向量，确保其被规范化并乘以半径
            Vector3 direction = startDirection.normalized * nRadius;

            for (int i = 0; i < nNum; i++)
            {
                // 计算每个点的位置
                points[i] = pAnchorPos + direction;
                // 更新方向向量以指向下一个点
                direction = rotation * direction;
            }

            return points;
        }

        /// <summary>
        /// 将屏幕坐标转换为目标分辨率下的坐标
        /// </summary>
        /// <param name="originalX">原始X</param>
        /// <param name="originalY">原始Y</param>
        /// <param name="originalWidth">原始W</param>
        /// <param name="originalHeight">原始H</param>
        /// <param name="targetWidth">目标W</param>
        /// <param name="targetHeight">目标H</param>
        /// <returns></returns>
        public static Vector2 ConvertScreenPoint(float originalX, float originalY, float originalWidth, float originalHeight, float targetWidth, float targetHeight)
        {
            // 计算宽度和高度的缩放比例
            float scaleX = targetWidth / originalWidth;
            float scaleY = targetHeight / originalHeight;

            // 应用缩放比例到原始点位
            float newX = originalX * scaleX;
            float newY = originalY * scaleY;

            return new Vector2(newX, newY);
        }

        /// <summary>
        /// 获取两个Transform之间的旋转方向
        /// </summary>
        /// <param name="forward1">前方 半挂车 车头</param>
        /// <param name="forward2">后方 半挂车 半挂</param>
        /// <returns></returns>
        public static RotationDirection GetRotationDirection(Vector2 forward1, Vector2 forward2)
        {
            Vector2 v1 = forward1;
            Vector2 v2 = forward2;

            float rightFloat = v1.x * v2.y - v2.x * v1.y;

            if (rightFloat < 0)
            {
                return RotationDirection.Right;
            }
            else if (rightFloat > 0)
            {
                return RotationDirection.Left;
            }
            else
            {
                return RotationDirection.None;
            }
        }

        /// <summary>
        /// 在指定的容器中找到距离最近的位置
        /// </summary>
        /// <param name="position">自己的位置</param>
        /// <param name="otherPositions">其他对象的位置</param>
        /// <returns>最近的位置</returns>
        public static Vector3 GetClosest(this Vector3 position, IEnumerable<Vector3> otherPositions)
        {
            Vector3 closest = Vector3.zero;
            float shortestDistance = Mathf.Infinity;
            Vector3 difference;

            foreach (var otherPosition in otherPositions)
            {
                difference = position - otherPosition;
                float distance = difference.sqrMagnitude;

                if (distance < shortestDistance)
                {
                    closest = otherPosition;
                    shortestDistance = distance;
                }
            }

            return closest;
        }

        /// <summary>
        /// 将向量旋转指定角度
        /// </summary>
        /// <param name="vector">要旋转的向量</param>
        /// <param name="angleInDeg">角度（度）</param>
        /// <returns>旋转向量</returns>
        public static Vector2 Rotate(this Vector2 vector, float angleInDeg)
        {
            float angleInRad = Mathf.Deg2Rad * angleInDeg;
            float cosAngle = Mathf.Cos(angleInRad);
            float sinAngle = Mathf.Sin(angleInRad);

            float x = vector.x * cosAngle - vector.y * sinAngle;
            float y = vector.x * sinAngle + vector.y * cosAngle;

            return new Vector2(x, y);
        }

        /// <summary>
        /// 将向量围绕目标点旋转指定角度
        /// </summary>
        /// <param name="vector"></param>
        /// <param name="angleInDeg">角度</param>
        /// <param name="axisPosition">目标点</param>
        /// <returns></returns>
        public static Vector2 RotateAround(this Vector2 vector, float angleInDeg, Vector2 axisPosition)
        {
            return (vector - axisPosition).Rotate(angleInDeg) + axisPosition;
        }


        /// <summary>
        /// 将向量旋转90度
        /// </summary>
        /// <param name="vector"></param>
        /// <returns></returns>
        public static Vector2 Rotate90(this Vector2 vector)
        {
            return new Vector2(-vector.y, vector.x);
        }

        /// <summary>
        /// 将向量旋转180度
        /// </summary>
        /// <param name="vector"></param>
        /// <returns></returns>
        public static Vector2 Rotate180(this Vector2 vector)
        {
            return new Vector2(-vector.x, -vector.y);
        }

        /// <summary>
        /// 将向量旋转270度
        /// </summary>
        /// <param name="vector"></param>
        /// <returns></returns>
        public static Vector2 Rotate270(this Vector2 vector)
        {
            return new Vector2(vector.y, -vector.x);
        }

        /// <summary>
        /// 计算一个点在指定轴上的最近点
        /// </summary>
        /// <param name="axisDirection">轴的方向</param>
        /// <param name="point">要计算的点</param>
        /// <returns>点在轴上的最近点</returns>
        public static Vector3 NearestPointOnAxis(this Vector3 axisDirection, Vector3 point)
        {
            // 确保轴的方向是单位向量
            axisDirection.Normalize();

            // 计算点和轴方向的点积，得到点在轴上的投影长度
            var d = Vector3.Dot(point, axisDirection);

            // 将点积乘以轴的方向，得到点在轴上的最近点
            return axisDirection * d;
        }

        /// <summary>
        /// 计算一个点在给定直线上的最近点
        /// </summary>
        /// <param name="lineDirection">直线的方向向量</param>
        /// <param name="point">要计算的空间点</param>
        /// <param name="pointOnLine">用于唯一确定直线的位置，是直线上的一个已知点</param>
        /// <returns>点在直线上的最近点</returns>
        public static Vector3 NearestPointOnLine(this Vector3 lineDirection, Vector3 point, Vector3 pointOnLine)
        {
            // 确保直线的方向是单位向量
            lineDirection.Normalize();

            // 计算点和直线上的点的差，然后和直线方向的点积，得到点在直线上的投影长度
            var d = Vector3.Dot(point - pointOnLine, lineDirection);

            // 将点积乘以直线的方向，然后加上直线上的点，得到点在直线上的最近点
            return pointOnLine + lineDirection * d;
        }
        #endregion


    }
}
