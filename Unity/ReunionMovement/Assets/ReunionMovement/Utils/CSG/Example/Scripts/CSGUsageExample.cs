using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ReunionMovement.Common.Util.Example
{
    public class CSGUsageExample : MonoBehaviour
    {
        public GameObject lhs;

        public GameObject rhs;

        public CSG.BooleanOp Operation;

        public Material material;

        /// <summary>
        /// 执行
        /// </summary>
        public void Perform()
        {
            if (lhs == null || rhs == null)
            {
                Debug.LogError("CSGUsageExample: lhs/rhs 未赋值");
                return;
            }
            Model result = CSG.Perform(Operation, lhs, rhs);
            if (result == null)
            {
                Debug.LogError("CSG 运算失败（输入对象缺少 MeshFilter/MeshRenderer）");
                return;
            }

            // 必须先设置材质再取 mesh：网格转换按 Materials 列表划分 submesh，
            // 构建后再 Add 的材质没有对应几何体（material 为 null 时渲染品红）
            if (material != null)
            {
                for (int i = 0; i < result.Materials.Count; i++)
                {
                    result.Materials[i] = material;
                }
            }

            var composite = new GameObject(Operation + " Object");
            composite.AddComponent<MeshFilter>().sharedMesh = result.mesh;
            composite.AddComponent<MeshRenderer>().sharedMaterials = result.Materials.ToArray();
        }
    }
}