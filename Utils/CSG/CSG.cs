using System;
using UnityEngine;
using System.Collections.Generic;

namespace ReunionMovement.Common.Util
{
    /// <summary>
    /// CSG操作的基类。包含用于减法、交集和并集操作的游戏对象级方法。传递给这些函数的游戏对象将不会被修改。
    /// </summary>
    public static class CSG
    {
        public enum BooleanOp
        {
            Intersection,
            Union,
            Subtraction
        }

        /// <summary>
        /// 默认公差 0.01mm，适配 Unity 米制单位下的 float 精度。
        /// 过小（如 1e-7）会导致 Plane 归一化后浮点误差被误判为非共面，引发无限递归。
        /// </summary>
        const double defaultEpsilon = 1e-5;
        static double epsilon = defaultEpsilon;

        /// <summary>
        /// 使用的公差确定平面是否重合
        /// <see cref="Plane.SplitPolygon"/> 
        /// </summary>
        public static double Epsilon
        {
            get => epsilon;
            set => epsilon = value;
        }

        /// <summary>
        /// 对两个游戏对象执行布尔运算
        /// </summary>
        /// <returns>A new mesh.</returns>
        public static Model Perform(BooleanOp op, GameObject lhs, GameObject rhs)
        {
            switch (op)
            {
                case BooleanOp.Intersection:
                    return Intersect(lhs, rhs);
                case BooleanOp.Union:
                    return Union(lhs, rhs);
                case BooleanOp.Subtraction:
                    return Subtract(lhs, rhs);
                default:
                    return null;
            }
        }

        /// <summary>
        /// 通过合并@lhs和@rhs返回一个新网格
        /// </summary>
        /// <param name="lhs">布尔运算的基本网格</param>
        /// <param name="rhs">布尔运算的基本网格</param>
        /// <returns>如果操作成功，则生成一个新网格，如果发生错误，则返回null</returns>
        public static Model Union(GameObject lhs, GameObject rhs)
        {
            Model csg_model_a = CreateModelOrNull(lhs);
            if (csg_model_a == null) return null;
            Model csg_model_b = CreateModelOrNull(rhs);
            if (csg_model_b == null) return null;

            Node a = new Node(csg_model_a.ToPolygons());
            Node b = new Node(csg_model_b.ToPolygons());

            List<Polygon> polygons = Node.Union(a, b).AllPolygons();

            return new Model(polygons);
        }

        /// <summary>
        /// 通过用@rhs减去@lhs来返回一个新网格
        /// </summary>
        /// <param name="lhs">布尔运算的基本网格</param>
        /// <param name="rhs">布尔运算的基本网格</param>
        /// <returns>如果操作成功，则生成一个新网格，如果发生错误，则为null</returns>
        public static Model Subtract(GameObject lhs, GameObject rhs)
        {
            Model csg_model_a = CreateModelOrNull(lhs);
            if (csg_model_a == null) return null;
            Model csg_model_b = CreateModelOrNull(rhs);
            if (csg_model_b == null) return null;

            Node a = new Node(csg_model_a.ToPolygons());
            Node b = new Node(csg_model_b.ToPolygons());

            List<Polygon> polygons = Node.Subtract(a, b).AllPolygons();

            return new Model(polygons);
        }

        /// <summary>
        /// 通过将@lhs与@rhs相交来返回新网格
        /// </summary>
        /// <param name="lhs">布尔运算的基本网格</param>
        /// <param name="rhs">布尔运算的基本网格</param>
        /// <returns>如果操作成功，则生成一个新网格，如果发生错误，则为null</returns>
        public static Model Intersect(GameObject lhs, GameObject rhs)
        {
            Model csg_model_a = CreateModelOrNull(lhs);
            if (csg_model_a == null) return null;
            Model csg_model_b = CreateModelOrNull(rhs);
            if (csg_model_b == null) return null;

            Node a = new Node(csg_model_a.ToPolygons());
            Node b = new Node(csg_model_b.ToPolygons());

            List<Polygon> polygons = Node.Intersect(a, b).AllPolygons();

            return new Model(polygons);
        }

        /// <summary>
        /// 构建输入模型：与文档一致，输入对象缺少 MeshFilter/sharedMesh 或 MeshRenderer 时返回 null，
        /// 而不是抛 ArgumentNullException（文档承诺"发生错误返回 null"）。
        /// </summary>
        private static Model CreateModelOrNull(GameObject go)
        {
            if (go == null)
            {
                Log.Error("CSG 输入对象为 null");
                return null;
            }
            var filter = go.GetComponent<MeshFilter>();
            var renderer = go.GetComponent<MeshRenderer>();
            if (filter == null || filter.sharedMesh == null || renderer == null)
            {
                Log.Error("CSG 输入对象缺少 MeshFilter/sharedMesh 或 MeshRenderer: {0}", go.name);
                return null;
            }
            try
            {
                return new Model(go);
            }
            catch (Exception ex)
            {
                Log.Error("CSG 构建输入模型失败: {0}", ex.Message);
                return null;
            }
        }
    }
}