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
        public bool IsInited { get; private set; }
        private double initProgress = 0;
        public double InitProgress { get { return initProgress; } }
        #endregion

        Keyboard keyboard;
        public TerminalRequest terminalRequest { get; private set; }

        public async Task Init()
        {
            initProgress = 0;

            keyboard = Keyboard.current;
            terminalRequest = new TerminalRequest();

            terminalRequest.RegisterCommands();

            initProgress = 100;
            IsInited = true;
            Log.Debug("TerminalSystem 初始化完成");
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
                if (UISystem.Instance != null && UISystem.Instance.IsInited)
                {
                    UISystem.Instance.ToggleWindow("TerminalUIPlane");
                }
            }
        }

        public void Clear()
        {
            Log.Debug("TerminalSystem 清除数据");
        }

        #region 例子
        [RegisterCommand(Help = "TestTerminal 2 String", MinArgCount = 2, MaxArgCount = 2)]
        static void TestTerminal(CommandArg[] args)
        {
            if (args.Length >= 2)
            {
                string str = "TestTerminal " + args[0].String + " | " + args[0].String;

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