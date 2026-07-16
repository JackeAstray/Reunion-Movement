using ReunionMovement.Common;
using ReunionMovement.Core.Base;
using ReunionMovement.Core.UI;
using Cysharp.Threading.Tasks;
using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ReunionMovement.Core.Terminal
{
    public class TerminalSystem : ICustomSystem
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

        public UniTask Init()
        {
            initProgress = 0;

            keyboard = Keyboard.current;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // 创建或查找TerminalRequest MonoBehaviour，而不是使用"new"
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
#endif

            initProgress = 100;
            isInited = true;
            Log.Debug("TerminalSystem 初始化完成");

            return UniTask.CompletedTask;
        }

        public void Update(float logicTime, float realTime)
        {
            // 终端系统仅在编辑器或开发构建中可用，生产构建完全禁用
#if UNITY_EDITOR || DEVELOPMENT_BUILD
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
#endif
        }

        public void Clear()
        {
            Log.Debug("TerminalSystem 清除数据");
            isInited = false;
            // 销毁 TerminalRequest GameObject，避免重新 Init 时创建重复对象
            if (terminalRequest != null)
            {
                UnityEngine.Object.Destroy(terminalRequest.gameObject);
                terminalRequest = null;
            }
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        #region 打开指定UI
        //测试指令： OpenWindow PopupUIPlane 提示 测试 Ok No 
        [RegisterCommand(Help = "OpenWindow 1-16 String", MinArgCount = 1, MaxArgCount = 16)]
        static void OpenWindow(CommandArg[] args)
        {
            if (args.Length == 0)
            {
                Log.Error("OpenWindow: 缺少窗口名称参数");
                return;
            }

            string windowName = args[0].String;
            if (string.IsNullOrEmpty(windowName))
            {
                Log.Error("窗口名称不能为空.");
                return;
            }

            if (UISystem.Instance.IsOpen(windowName))
            {
                Log.Error("窗口已打开!");
                return;
            }

            // 提取剩余参数（动态数量，不再需要 switch-case）
            var extraArgs = new object[args.Length - 1];
            for (int i = 1; i < args.Length; i++)
            {
                extraArgs[i - 1] = args[i].String;
            }

            UISystem.Instance.OpenWindow(windowName, extraArgs);
        }
        #endregion

        #region 例子
        [RegisterCommand(Help = "TestTerminal 2 String", MinArgCount = 2, MaxArgCount = 2)]
        //测试指令： TestTerminal 测试1 测试2
        static void TestTerminal(CommandArg[] args)
        {
            if (args.Length >= 2)
            {
                string str = "使用测试命令： " + "值1:" + args[0].String + " | " + "值2:" + args[1].String;

                Log.Debug(str);

                if (UISystem.Instance.IsOpen("TerminalUIPlane"))
                {
                    UISystem.Instance.SetWindow("TerminalUIPlane", "CreateItem", str);
                }
            }
        }
        #endregion
#endif
    }
}