using Gear.Common;
using Gear.Framework.Runtime.UIInput;
using Cysharp.Threading.Tasks;
using R3;
using System;
using UnityEngine;

namespace Gear.Framework.Runtime.UI
{
    /// <summary>
    /// UI 路由 —— 在 <see cref="UISystem"/> 之上补一层"界面之间怎么走"的能力。
    ///
    /// <para>
    /// 改造前只有命令式的 <c>OpenWindow/CloseWindow</c>：没有栈、没有"返回"、层级只由打开顺序决定。
    /// 这里加三件事，且**不改变现有窗口的任何行为**：
    /// <list type="bullet">
    /// <item><b>栈</b>：<see cref="PushAsync{TWindow, TArgs}"/> 打开并入栈，<see cref="Back"/> 关闭栈顶；</item>
    /// <item><b>返回键</b>：第一次使用路由时自动订阅 <c>UIInputSystem.CancelSubject</c>
    ///       （焦点域没消费取消键时它才会广播），ESC / 手柄 B 即"返回"；</item>
    /// <item><b>栈同步</b>：窗口若被直接 <c>CloseWindow</c> 关掉，会自动从栈里摘除，不留幽灵项。</item>
    /// </list>
    /// </para>
    ///
    /// <code>
    /// await UIRouter.PushAsync&lt;ShopUI, ShopArgs&gt;(new ShopArgs("weapon", 1));
    /// if (UIRouter.CanGoBack) UIRouter.Back();      // 等价于按 ESC
    /// </code>
    /// </summary>
    public static partial class UIRouter
    {
        private static readonly UINavigationStack stack = new UINavigationStack();

        private static IDisposable cancelSubscription;
        private static IDisposable closeSubscription;
        private static bool hooked;

        /// <summary>栈深度。</summary>
        public static int Depth => stack.Count;

        /// <summary>是否还有可返回的窗口。</summary>
        public static bool CanGoBack => !stack.IsEmpty;

        /// <summary>栈顶窗口 Id；空栈为 null。</summary>
        public static string Top => stack.Top;

        /// <summary>当前路由栈快照（调试用）。</summary>
        public static UINavigationStack Stack => stack;

        /// <summary>
        /// 打开窗口并入栈（无参数版本）。
        /// </summary>
        public static async UniTask<UILoadState> PushAsync<TWindow>() where TWindow : UIController
        {
            return await PushAsync<TWindow, NoWindowArgs>(NoWindowArgs.Default);
        }

        /// <summary>
        /// 打开窗口并入栈（强类型参数）。
        /// </summary>
        public static async UniTask<UILoadState> PushAsync<TWindow, TArgs>(TArgs args)
            where TWindow : UIController
            where TArgs : IWindowArgs
        {
            if (!Modules.TryGet(out UISystem ui) || ui == null || !ui.isInited)
            {
                Log.Warning("[UIRouter] UISystem 尚未初始化，忽略 PushAsync<{0}>", typeof(TWindow).Name);
                return null;
            }

            EnsureHooked();

            string windowId = UIWindowCatalog.GetId(typeof(TWindow));
            UILoadState state = await ui.OpenWindowAsync<TWindow, TArgs>(args);

            if (state?.uiWindow != null)
            {
                stack.Push(windowId);
                // 声明了 [UIWindow(Modal = true)] 的窗口会阻断下层窗口交互
                NotifyPushed(windowId, UIWindowCatalog.Get(windowId).Modal);
                Log.Debug("[UIRouter] Push {0}（栈: {1}）", windowId, stack);
            }
            else
            {
                Log.Warning("[UIRouter] Push {0} 失败（窗口未加载），栈不变", windowId);
            }
            return state;
        }

        /// <summary>
        /// 打开窗口并入栈（按窗口 Id，用于没有强类型参数的场景）。
        /// </summary>
        public static async UniTask<UILoadState> PushAsync(string windowId, params object[] args)
        {
            if (string.IsNullOrEmpty(windowId) || !Modules.TryGet(out UISystem ui) || ui == null || !ui.isInited)
            {
                Log.Warning("[UIRouter] PushAsync 参数无效（windowId={0}）", windowId);
                return null;
            }

            EnsureHooked();

            UILoadState state = await ui.OpenWindowAsync(windowId, args);
            if (state?.uiWindow != null)
            {
                stack.Push(windowId);
                // 声明了 [UIWindow(Modal = true)] 的窗口会阻断下层窗口交互
                NotifyPushed(windowId, UIWindowCatalog.Get(windowId).Modal);
                Log.Debug("[UIRouter] Push {0}（栈: {1}）", windowId, stack);
            }
            return state;
        }

        /// <summary>
        /// 返回：关闭栈顶窗口并从栈里移除。
        /// </summary>
        /// <returns>成功返回 true；栈为空或系统未初始化返回 false（调用方可据此决定是否上报输入）</returns>
        public static bool Back()
        {
            if (!stack.TryPop(out string windowId)) return false;

            if (!Modules.TryGet(out UISystem ui) || ui == null || !ui.isInited)
            {
                Log.Debug("[UIRouter] Back：UISystem 未初始化，已清空栈");
                stack.Clear();
                return false;
            }

            try
            {
                ui.CloseWindow(windowId);
                Log.Debug("[UIRouter] Back 关闭 {0}（栈: {1}）", windowId, stack);
                return true;
            }
            catch (Exception ex)
            {
                Log.Error("[UIRouter] Back 关闭 {0} 异常: {1}", windowId, ex.Message);
                return false;
            }
        }

        /// <summary>关闭指定窗口（若在栈里则一并摘除）。</summary>
        public static void Close(string windowId)
        {
            if (string.IsNullOrEmpty(windowId)) return;
            stack.Remove(windowId);
if (Modules.TryGet(out UISystem ui) && ui != null && ui.isInited)
            {
                ui.CloseWindow(windowId);
            }
        }

        /// <summary>清空栈（不关闭窗口，只清导航状态；域名重载/退出登录等场景用）。</summary>
        public static void ClearStack()
        {
            stack.Clear();
        }

        /// <summary>释放订阅并清空栈（测试 / 引擎重启用）。</summary>
        public static void Reset()
        {
            cancelSubscription?.Dispose();
            cancelSubscription = null;
            closeSubscription?.Dispose();
            closeSubscription = null;
            hooked = false;
            stack.Clear();
            ResetModal();
        }

        /// <summary>
        /// 惰性挂钩：第一次 Push 时才订阅（此时 UISystem/UIInputSystem 必然已初始化）。
        /// </summary>
        private static void EnsureHooked()
        {
            if (hooked) return;
            hooked = true;

            // 1) 返回键：焦点域没有消费取消键时，UIInputSystem 才会广播 CancelSubject
            try
            {
                if (Modules.TryGet<UIInputSystem>(out var input) && input.CancelSubject != null)
                {
                    cancelSubscription = input.CancelSubject.Subscribe(_ =>
                    {
                        if (CanGoBack) Back();
                    });
                }
            }
            catch (Exception ex)
            {
                Log.Warning("[UIRouter] 订阅取消键失败（不影响其他功能）: {0}", ex.Message);
            }

            // 2) 栈同步：窗口被直接关掉时从栈里摘除
            try
            {
                if (Modules.TryGet(out UISystem ui) && ui != null && ui.OnCloseSubject != null)
                {
                    closeSubscription = ui.OnCloseSubject.Subscribe(window =>
                    {
                        if (window == null) return;
                        string id = string.IsNullOrEmpty(window.uiName)
                            ? UIWindowCatalog.GetId(window.GetType())
                            : window.uiName;
                        stack.Remove(id);
                        // 有等待中的结果请求就按"取消"处理，避免调用方永远挂在 await 上
                        CancelPendingResult(id, "窗口被直接关闭");
                        // 模态关掉后重算下层阻断
                        NotifyClosed(id);
                    });
                }
            }
            catch (Exception ex)
            {
                Log.Warning("[UIRouter] 订阅窗口关闭事件失败（不影响其他功能）: {0}", ex.Message);
            }
        }

        /// <summary>域重载关闭时静态栈不会自动复位，这里跟随 Play 会话复位。</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            Reset();
        }
    }
}
