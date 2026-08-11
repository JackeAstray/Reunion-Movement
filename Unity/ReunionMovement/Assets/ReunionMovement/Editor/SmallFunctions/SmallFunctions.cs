using ReunionMovement.UI.RippleAnimation;
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace ReunionMovement.Common.Util.EditorTools
{
    /// <summary>
    /// 一些小功能
    /// </summary>
    public class SmallFunctions : EditorWindow
    {
        // 实例字段：避免公共静态可变状态在编辑器域内跨窗口泄漏，且 OnEnable 时刷新保证不过期
        private List<string> scenesName = new List<string>();
        private List<string> scenePaths = new List<string>();

        /// <summary>
        /// 小功能窗口
        /// </summary>
        [MenuItem("ReunionMovement/小功能", false, 100)]
        public static void SmallFunctionsWindow()
        {
            //version = new System.Version(PlayerSettings.bundleVersion);

            SmallFunctions smallFunctions = GetWindow<SmallFunctions>(true, "小功能", true);
            smallFunctions.minSize = new Vector2(400, 600);
            smallFunctions.GetAllScene();
        }

        private void OnEnable()
        {
            // 窗口打开/Build Settings 变化后重新加载场景列表，避免列表过期
            GetAllScene();
        }

        /// <summary>
        /// 获取所有场景
        /// </summary>
        public void GetAllScene()
        {
            scenesName.Clear();
            scenePaths.Clear();

            foreach (UnityEditor.EditorBuildSettingsScene scene in UnityEditor.EditorBuildSettings.scenes)
            {
                // 过滤被禁用的场景与空路径，保证 scenesName 与 scenePaths 一一对应（防索引错位）
                if (!scene.enabled || string.IsNullOrEmpty(scene.path)) continue;

                scenePaths.Add(scene.path);
                scenesName.Add(System.IO.Path.GetFileNameWithoutExtension(scene.path));
            }
        }

        /// <summary>
        /// 加载场景
        /// </summary>
        /// <param name="scenePaths"></param>
        public void LoadScene(string scenePaths)
        {
            EditorSceneManager.OpenScene(scenePaths, OpenSceneMode.Single);
        }

        void OnGUI()
        {
            GUILayout.Label("场景切换");
            GUILayout.BeginVertical();
            for (int i = 0; i < scenesName.Count; i++)
            {
                if (GUILayout.Button(scenesName[i]))
                {
                    LoadScene(scenePaths[i]);
                }
            }
            GUILayout.EndVertical();

            CreateButtonGroup("屏幕日志", "生成屏幕日志控件", "移除屏幕日志控件", CreateLogComponent, CloseLogComponent);
            CreateButtonGroup("FPS", "生成FPS控件", "移除FPS控件", CreateFPSComponent, CloseFPSComponent);
            CreateButtonGroup("UI波纹", "添加波纹效果（Image）", "移除波纹效果（UIRipple）", AddRippleEffect<Image, UIRipple>, RemoveRippleEffect<UIRipple>);
        }

        /// <summary>
        /// 创建按钮组
        /// </summary>
        /// <param name="label"></param>
        /// <param name="button1Text"></param>
        /// <param name="button2Text"></param>
        /// <param name="button1Action"></param>
        /// <param name="button2Action"></param>
        void CreateButtonGroup(string label, string button1Text, string button2Text, Action button1Action, Action button2Action)
        {
            GUILayout.Label(label);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(button1Text, GUILayout.Width(195)))
            {
                button1Action();
            }
            if (GUILayout.Button(button2Text, GUILayout.Width(195)))
            {
                button2Action();
            }
            GUILayout.EndHorizontal();
        }

        /// <summary>
        /// 为选中的对象添加波纹效果
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <typeparam name="U"></typeparam>
        void AddRippleEffect<T, U>() where T : Component where U : Component
        {
            GameObject selectedObject = Selection.activeGameObject;
            if (selectedObject == null)
            {
                Log.Warning("请先在 Hierarchy 中选中一个对象！");
                return;
            }
            var assetPath = EditorUtility.IsPersistent(selectedObject);
            if (assetPath == false)
            {
                if (selectedObject.GetComponent<T>())
                {
                    // 通过 Undo API 添加，支持 Ctrl+Z 撤销
                    Undo.AddComponent<U>(selectedObject);
                }
                else
                {
                    Log.Warning("选中的对象缺少" + typeof(T).Name + "部件，不予添加！");
                }
            }
            else
            {
                Log.Warning("选中的对象必须在Hierachy视图！");
            }
        }

        /// <summary>
        /// 为选中的对象移除波纹效果
        /// </summary>
        /// <typeparam name="T"></typeparam>
        void RemoveRippleEffect<T>() where T : Component
        {
            GameObject selectedObject = Selection.activeGameObject;
            if (selectedObject == null)
            {
                Log.Warning("请先在 Hierarchy 中选中一个对象！");
                return;
            }
            var assetPath = EditorUtility.IsPersistent(selectedObject);
            if (assetPath == false)
            {
                var comp = selectedObject.GetComponent<T>();
                if (comp)
                {
                    // 通过 Undo API 移除，支持 Ctrl+Z 撤销
                    Undo.DestroyObjectImmediate(comp);
                    // 不再无条件删除 Mask：Mask 可能是用户自行添加的（用于裁剪），误删会破坏场景数据。
                    // 若 Mask 是添加波纹时由 RequireComponent 自动生成的，移除 UIRipple 后 Unity 会尝试自动清理。
                }
                else
                {
                    Log.Warning("选中的对象缺少" + typeof(T).Name + "部件，无法移除！");
                }
            }
            else
            {
                Log.Warning("选中的对象必须在Hierachy视图！");
            }
        }

        /// <summary>
        /// 创建日志组件
        /// </summary>
        public static void CreateLogComponent()
        {
            CreateComponent<ScreenLogger>("ScreenLogger");
        }

        /// <summary>
        /// 关闭日志组件
        /// </summary>
        public static void CloseLogComponent()
        {
            CloseComponent<ScreenLogger>();
        }

        /// <summary>
        /// 创建FPS组件
        /// </summary>
        public static void CreateFPSComponent()
        {
            CreateComponent<FPSCounter>("FPSCounter");
        }

        /// <summary>
        /// 关闭FPS组件
        /// </summary>
        public static void CloseFPSComponent()
        {
            CloseComponent<FPSCounter>();
        }

        /// <summary>
        /// 创建组件
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="name"></param>
        public static void CreateComponent<T>(string name) where T : Component
        {
            GameObject obj = GameObject.Find(name);

            if (obj)
            {
                if (!obj.GetComponent<T>())
                {
                    Undo.AddComponent<T>(obj);
                }
            }
            else
            {
                obj = new GameObject(name);
                Selection.activeGameObject = obj;
                Undo.RegisterCreatedObjectUndo(obj, "创建 " + name);
                Undo.AddComponent<T>(obj);
            }
        }

        /// <summary>
        /// 关闭组件
        /// </summary>
        /// <typeparam name="T"></typeparam>
        public static void CloseComponent<T>() where T : Component
        {
            GameObject[] objects = GameObject.FindObjectsByType<GameObject>(FindObjectsSortMode.InstanceID);

            foreach (GameObject obj in objects)
            {
                var comp = obj.GetComponent<T>();
                if (comp != null)
                {
                    // 仅移除目标组件（支持 Undo），避免销毁整个 GameObject 连带删掉整棵 UI 子树
                    Undo.DestroyObjectImmediate(comp);
                }
            }
        }
    }
}