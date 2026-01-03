using ReunionMovement.Common;
using ReunionMovement.Core.Base;
using ReunionMovement.Core.UI;
using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ReunionMovement.Core.Terminal
{
    public class TerminalSystem : ICustommSystem
    {
        #region 单例与初始化
        private static readonly Lazy<TerminalSystem> instance = new(() => new TerminalSystem());
        public static TerminalSystem Instance => instance.Value;

        public bool isInited { get; private set; }

        private double initProgress = 0;
        public double InitProgress { get { return initProgress; } }
        #endregion

        Keyboard keyboard;
        public TerminalRequest terminalRequest { get; private set; }

        public Task Init()
        {
            initProgress = 0;

            keyboard = Keyboard.current;

            // 创建或查找TerminalRequest MonoBehaviour，而不是使用“new”
            var existing = GameObject.FindFirstObjectByType<TerminalRequest>();
            if (existing != null)
            {
                terminalRequest = existing;
            }
            else
            {
                var go = new GameObject("TerminalRequest");
                UnityEngine.Object.DontDestroyOnLoad(go);
                terminalRequest = go.AddComponent<TerminalRequest>();
            }

            terminalRequest.RegisterCommands();

            initProgress = 100;
            isInited = true;
            Log.Debug("TerminalSystem 初始化完成");

            return Task.CompletedTask;
        }

        public void Update(float logicTime, float realTime)
        {
            // 确保 keyboard 可用（运行时 Keyboard.current 可能会在开始时为 null）
            if (keyboard == null)
                keyboard = Keyboard.current;

            if (keyboard == null)
                return;

            // 检测 ~ / ` 键（Backquote）被按下（按下当帧触发）
            if (keyboard.backquoteKey.wasPressedThisFrame)
            {
                // UI 系统未初始化时不执行切换
                if (UISystem.Instance != null && UISystem.Instance.isInited)
                {
                    UISystem.Instance.ToggleWindow("TerminalUIPlane");
                }
            }
        }

        public void Clear()
        {
            Log.Debug("TerminalSystem 清除数据");
        }

        #region 打开指定UI
        //测试指令： OpenWindow PopupUIPlane 提示 测试 Ok No 
        [RegisterCommand(Help = "OpenWindow 1-6 String", MinArgCount = 1, MaxArgCount = 6)]
        static void OpenWindow(CommandArg[] args)
        {
            int count = args.Length;

            switch (count)
            {
                case 1:
                    {
                        string windowName = args[0].String;
                        if (!string.IsNullOrEmpty(windowName))
                        {
                            if (!UISystem.Instance.IsOpen(windowName))
                            {
                                UISystem.Instance.OpenWindow(windowName);
                            }
                            else
                            {
                                Log.Error("窗口已打开!");
                            }
                        }
                        else
                        {
                            Log.Error("窗口名称不能为空.");
                        }
                    }
                    break;
                case 2:
                    {
                        string windowName = args[0].String;
                        string str1 = args[1].String;
                        if (!string.IsNullOrEmpty(windowName))
                        {
                            if (!UISystem.Instance.IsOpen(windowName))
                            {
                                UISystem.Instance.OpenWindow(windowName, str1);
                            }
                            else
                            {
                                Log.Error("窗口已打开!");
                            }
                        }
                        else
                        {
                            Log.Error("窗口名称不能为空.");
                        }
                    }
                    break;
                case 3:
                    {
                        string windowName = args[0].String;
                        string str1 = args[1].String;
                        string str2 = args[2].String;
                        if (!string.IsNullOrEmpty(windowName))
                        {
                            if (!UISystem.Instance.IsOpen(windowName))
                            {
                                UISystem.Instance.OpenWindow(windowName, str1, str2);
                            }
                            else
                            {
                                Log.Error("窗口已打开!");
                            }
                        }
                        else
                        {
                            Log.Error("窗口名称不能为空.");
                        }
                    }
                    break;
                case 4:
                    {
                        string windowName = args[0].String;
                        string str1 = args[1].String;
                        string str2 = args[2].String;
                        string str3 = args[3].String;
                        if (!string.IsNullOrEmpty(windowName))
                        {
                            if (!UISystem.Instance.IsOpen(windowName))
                            {
                                UISystem.Instance.OpenWindow(windowName, str1, str2, str3);
                            }
                            else
                            {
                                Log.Error("窗口已打开!");
                            }
                        }
                        else
                        {
                            Log.Error("窗口名称不能为空.");
                        }
                    }
                    break;
                case 5:
                    {
                        string windowName = args[0].String;
                        string str1 = args[1].String;
                        string str2 = args[2].String;
                        string str3 = args[3].String;
                        string str4 = args[4].String;
                        if (!string.IsNullOrEmpty(windowName))
                        {
                            if (!UISystem.Instance.IsOpen(windowName))
                            {
                                UISystem.Instance.OpenWindow(windowName, str1, str2, str3, str4);
                            }
                            else
                            {
                                Log.Error("窗口已打开!");
                            }
                        }
                        else
                        {
                            Log.Error("窗口名称不能为空.");
                        }
                    }
                    break;
                default:
                    break;
            }

        }
        #endregion

        #region 例子
        [RegisterCommand(Help = "TestTerminal 2 String", MinArgCount = 2, MaxArgCount = 2)]
        //测试指令： TestTerminal 测试1 测试2
        static void TestTerminal(CommandArg[] args)
        {
            if (args.Length >= 2)
            {
                string str = "使用测试命令： " + "值1:" + args[0].String + " | " + "值2:" + args[0].String;

                Log.Debug(str);

                if (UISystem.Instance.IsOpen("TerminalUIPlane"))
                {
                    UISystem.Instance.SetWindow("TerminalUIPlane", "CreateItem", str);
                }
            }
        }
        #endregion
    }
}