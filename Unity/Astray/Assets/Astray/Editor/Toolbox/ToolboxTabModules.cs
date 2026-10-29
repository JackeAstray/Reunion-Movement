using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Astray.EditorTools.Toolbox
{
    /// <summary>
    /// 模块：把 [ModuleOrder] 声明的初始化顺序与依赖画成一张表。
    /// 这里展示的是"声明"，实际生效顺序由 ModuleCatalog 在启动时拓扑排序，日志里会打印一行「模块顺序: …」。
    /// </summary>
    public sealed partial class AstrayToolboxWindow
    {
        private void DrawModulesTab()
        {
            SectionHeader("模块顺序与依赖",
                "顺序来自各模块类上的 [ModuleOrder(order, 依赖…)]；实际启动顺序由 ModuleCatalog 校验后拓扑排序。");

            if (modules.Count == 0)
            {
                EditorGUILayout.HelpBox("未检测到带 [ModuleOrder] 的模块。请确认 Core / Utils 程序集已编译成功。", MessageType.Warning);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("定位 StartGame.cs", GUILayout.Width(150f))) SelectAsset(AssetRoot + "/Framework/Runtime/StartGame.cs");
                    if (GUILayout.Button("重新检查", GUILayout.Width(90f))) RefreshAll();
                }
                return;
            }

            List<string> problems = ValidateModules();

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("共 " + modules.Count + " 个模块", EditorStyles.boldLabel, GUILayout.Width(120f));
                if (GUILayout.Button("定位注册入口 StartGame.cs", GUILayout.Width(200f))) SelectAsset(AssetRoot + "/Framework/Runtime/StartGame.cs");
                if (GUILayout.Button("定位 Framework/Runtime/System", GUILayout.Width(200f))) PingFolder(AssetRoot + "/Framework/Runtime/System");
            }

            if (problems.Count == 0)
            {
                EditorGUILayout.HelpBox("未发现序号冲突或缺失依赖。", MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox(string.Join("\n", problems), MessageType.Warning);
            }

            EditorGUILayout.Space(4f);
            for (int i = 0; i < modules.Count; i++)
            {
                DrawModuleRow(modules[i]);
            }

            EditorGUILayout.Space(6f);
            EditorGUILayout.HelpBox(
                "运行时取模块用 Modules.Get<T>()（取不到会抛异常并列出已注册模块）；" +
                "XxxInstance 只留给编辑器工具与单元测试。新增模块请写 [ModuleOrder] 并注册进 StartGame.CreateModules()。",
                MessageType.Info);
        }

        private void DrawModuleRow(ModuleInfo module)
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField(module.Order.ToString(), EditorStyles.boldLabel, GUILayout.Width(34f));
                using (new EditorGUILayout.VerticalScope())
                {
                    EditorGUILayout.LabelField(module.Name, EditorStyles.boldLabel);
                    EditorGUILayout.LabelField(module.Namespace + "  ·  " + module.AssemblyName, EditorStyles.miniLabel);
                }

                EditorGUILayout.LabelField("依赖 " + module.DependencyText, EditorStyles.wordWrappedMiniLabel, GUILayout.Width(240f));

                if (GUILayout.Button("定位脚本", GUILayout.Width(84f), GUILayout.Height(28f)))
                {
                    LocateModuleScript(module);
                }
            }
        }

        /// <summary>序号冲突与"依赖不在列表里"两类问题——和 ModuleCatalog 的校验对齐，但只做静态可见的部分。</summary>
        private List<string> ValidateModules()
        {
            var problems = new List<string>();

            for (int i = 0; i < modules.Count; i++)
            {
                for (int j = i + 1; j < modules.Count; j++)
                {
                    if (modules[i].Order != modules[j].Order) continue;

                    problems.Add("序号冲突：" + modules[i].Name + " 与 " + modules[j].Name + " 都是 " + modules[i].Order +
                                 "（同序号按注册顺序排序，若两者无依赖关系则启动顺序取决于 CreateModules 的书写位置）");
                }
            }

            for (int i = 0; i < modules.Count; i++)
            {
                Type[] dependencies = modules[i].DependsOn;
                if (dependencies == null) continue;

                for (int d = 0; d < dependencies.Length; d++)
                {
                    Type dependency = dependencies[d];
                    if (dependency == null) continue;

                    if (!ContainsProvider(dependency))
                    {
                        problems.Add(modules[i].Name + " 依赖 " + dependency.Name + "，但该模块未声明 [ModuleOrder] 或未在本工程内");
                    }
                }
            }

            return problems;
        }

        private bool ContainsProvider(Type dependency)
        {
            for (int i = 0; i < modules.Count; i++)
            {
                if (dependency.IsInstanceOfType(modules[i].Type)) return true;
            }
            return false;
        }

        /// <summary>把模块脚本 ping 出来（不依赖 type.FullName → 路径的假设，按类名找 MonoScript 再比对）。</summary>
        private static void LocateModuleScript(ModuleInfo module)
        {
            string[] guids = AssetDatabase.FindAssets(module.Name + " t:MonoScript");
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
                if (script == null) continue;
                if (script.GetClass() != module.Type) continue;

                Selection.activeObject = script;
                EditorGUIUtility.PingObject(script);
                return;
            }

            EditorUtility.DisplayDialog("Astray",
                "没有找到 " + module.Name + " 的脚本资产。若它是 partial 类，主文件未改名时无法按类名定位。", "确定");
        }
    }
}
