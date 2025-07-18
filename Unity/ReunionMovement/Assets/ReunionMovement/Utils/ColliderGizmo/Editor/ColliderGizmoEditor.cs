using UnityEditor;
using UnityEngine;

namespace ReunionMovement.Common.Util.EditorTools
{
    public class ColliderGizmoEditor
    {
        // 导航网格
        private const string EnabledNavMesh = "UNITY_NAVMESH_ENABLED";
        // 2D物理
        private const string EnabledPhysics2D = "UNITY_PHYSICS2D_ENABLED";
        // 3D物理
        private const string EnabledPhysics = "UNITY_PHYSICS_ENABLED";

        /// <summary>
        /// 获取所有脚本宏定义的字符串数组
        /// </summary>
        private static readonly string[] AllDefineSymbols = new string[]
        {
            EnabledNavMesh,
            EnabledPhysics2D,
            EnabledPhysics,
        };

        /// <summary>
        /// 禁用所有日志脚本宏定义。
        /// </summary>
        [MenuItem("工具箱/碰撞器线框宏/禁用所有宏", false, 17)]
        public static void DisableAllColliderGizmo()
        {
            foreach (string specifyLogScriptingDefineSymbol in AllDefineSymbols)
            {
                ScriptingDefineSymbols.RemoveScriptingDefineSymbol(specifyLogScriptingDefineSymbol);
            }
        }

        /// <summary>
        /// 启用所有日志脚本宏定义。
        /// </summary>
        [MenuItem("工具箱/碰撞器线框宏/启用所有宏", false, 18)]
        public static void EnableAllColliderGizmo()
        {
            DisableAllColliderGizmo();
            foreach (string specifyLogScriptingDefineSymbol in AllDefineSymbols)
            {
                ScriptingDefineSymbols.AddScriptingDefineSymbol(specifyLogScriptingDefineSymbol);
            }
        }

        /// <summary>
        /// 启用AI宏。
        /// </summary>
        [MenuItem("工具箱/碰撞器线框宏/启用导航网格宏", false, 19)]
        public static void EnableNavMesh()
        {
            SetAboveLogScriptingDefineSymbol(EnabledNavMesh);
        }

        /// <summary>
        /// 启用PHYSICS2D宏。
        /// </summary>
        [MenuItem("工具箱/碰撞器线框宏/启用Physics2D宏", false, 20)]
        public static void EnableEnabledPhysics2D()
        {
            SetAboveLogScriptingDefineSymbol(EnabledPhysics2D);
        }

        /// <summary>
        /// 启用PHYSICS宏。
        /// </summary>
        [MenuItem("工具箱/碰撞器线框宏/启用Physics宏", false, 21)]
        public static void EnableEnabledPhysics()
        {
            SetAboveLogScriptingDefineSymbol(EnabledPhysics);
        }

        /// <summary>
        /// 设置日志脚本宏定义。
        /// </summary>
        /// <param name="aboveLogScriptingDefineSymbol">要设置的日志脚本宏定义。</param>
        public static void SetAboveLogScriptingDefineSymbol(string aboveLogScriptingDefineSymbol)
        {
            if (string.IsNullOrEmpty(aboveLogScriptingDefineSymbol))
            {
                return;
            }

            foreach (string i in AllDefineSymbols)
            {
                if (i == aboveLogScriptingDefineSymbol)
                {
                    DisableAllColliderGizmo();
                    ScriptingDefineSymbols.AddScriptingDefineSymbol(aboveLogScriptingDefineSymbol);
                    return;
                }
            }
        }
    }
}