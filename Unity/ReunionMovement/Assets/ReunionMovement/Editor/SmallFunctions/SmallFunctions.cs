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
        public static List<string> scenesName = new List<string>();
        public static List<string> scenePaths = new List<string>();

        /// <summary>
        /// 小功能窗口
        /// </summary>
        [MenuItem("工具箱/小功能", false, 100)]
        public static void SmallFunctionsWindow()
        {
            //version = new System.Version(PlayerSettings.bundleVersion);

            GetAllScene();
            SmallFunctions smallFunctions = GetWindow<SmallFunctions>(true, "小功能", true);
            smallFunctions.minSize = new Vector2(400, 600);
        }

        /// <summary>
        /// 获取所有场景
        /// </summary>
        public static void GetAllScene()
        {
            scenesName.Clear();
            scenePaths.Clear();

            foreach (UnityEditor.EditorBuildSettingsScene scene in UnityEditor.EditorBuildSettings.scenes)
            {
                string tempPath = scene.path;
                scenePaths.Add(tempPath);

                string[] Name = tempPath.Split('/');

                foreach (var item in Name)
                {
                    if (item.Contains(".unity"))
                    {
                        scenesName.Add(item.Substring(0, item.IndexOf('.')));
                    }
                }
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
            var assetPath = EditorUtility.IsPersistent(selectedObject);
            if (assetPath == false)
            {
                if (selectedObject.GetComponent<T>())
                {
                    selectedObject.AddComponent<U>();
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
            var assetPath = EditorUtility.IsPersistent(selectedObject);
            if (assetPath == false)
            {
                if (selectedObject.GetComponent<T>())
                {
                    DestroyImmediate(selectedObject.GetComponent<T>());
                    DestroyImmediate(selectedObject.GetComponent<Mask>());
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
                    obj.AddComponent<T>();
                }
            }
            else
            {
                obj = new GameObject(name);
                Selection.activeGameObject = obj;
                obj.AddComponent<T>();
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
                if (obj.GetComponent<T>())
                {
                    GameObject.DestroyImmediate(obj);
                }
            }
        }
    }
}