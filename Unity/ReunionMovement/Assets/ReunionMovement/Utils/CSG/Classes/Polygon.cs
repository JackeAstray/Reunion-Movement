using UnityEngine;
using System.Collections.Generic;

namespace ReunionMovement.Common.Util
{
    /// <summary>
    /// 表示具有任意数量顶点的多边形面
    /// </summary>
    sealed class Polygon
    {
        public List<Vertex> vertices;
        public Plane plane;
        public Material material;

        public Polygon(List<Vertex> list, Material mat)
        {
            vertices = list;
            plane = new Plane(list[0].Position, list[1].Position, list[2].Position);
            material = mat;
        }

        public void Flip()
        {
            vertices.Reverse();

            for (int i = 0; i < vertices.Count; i++)
                vertices[i].Flip();

            plane.Flip();
        }

        /// <summary>
        /// 深拷贝多边形。Vertex 是值类型，new List 即深拷贝；Material 为引用共享。
        /// </summary>
        public Polygon Clone()
        {
            var clonedVertices = new List<Vertex>(vertices);
            return new Polygon(clonedVertices, material) { plane = new Plane(plane) };
        }

        public override string ToString()
        {
            return $"[{vertices.Count}] {plane.normal}";
        }
    }
}