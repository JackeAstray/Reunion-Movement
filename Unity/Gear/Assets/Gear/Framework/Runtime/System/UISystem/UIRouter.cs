using Gear.Common;
using Gear.Framework.Runtime.UIInput;
using Cysharp.Threading.Tasks;
using R3;
using System;
using System.Collections.Generic;
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

        // 记录"当前订阅挂在哪个 Subject 实例上"：UISystem.Clear()/Init() 会重建 Subject，
        // 只靠 hooked 布尔量会导致重建后永远不再订阅（旧订阅指向已 Dispose 的 Subject）。
        private static Subject<UIController> hookedCloseSubject;
        private static Subject<R3.Unit> hookedCancelSubject;

        /// <summary>栈深度。</summary>
        public static int Depth => stack.Count;

        /// <summary>是否还有可返回的窗口（导航栈或模态栈）。</summary>
        public static bool CanGoBack => !stack.IsEmpty || HasOpenModal;

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
                // 模态登记已由 UISystem.OpenWindow(Async) → NotifyOpened 完成，这里不再重复调用
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
                // 模态登记已由 UISystem.OpenWindow(Async) → NotifyOpened 完成，这里不再重复调用
                Log.Debug("[UIRouter] Push {0}（栈: {1}）", windowId, stack);
            }
            return state;
        }

        /// <summary>
        /// 返回：优先关闭最上层模态窗口，其次关闭导航栈顶。
        /// <para>
        /// 模态窗口打开时不一定会经过 <see cref="PushAsync{TWindow, TArgs}"/>，因此不在导航栈里；
        /// 旧实现直接弹导航栈，会关掉模态下面那个被遮住的窗口。这里先处理模态栈。
        /// </para>
        /// </summary>
        /// <returns>成功返回 true；栈为空或系统未初始化返回 false（调用方可据此决定是否上报输入）</returns>
        public static bool Back()
        {
            if (TryPopTopModal(out string modalId))
            {
                if (TryCloseWindow(modalId, "Back(modal)")) return true;
                // 模态栈已弹出但关闭失败（UISystem 未初始化）：落到下面的栈逻辑，避免吞掉输入
            }

            if (!stack.TryPop(out string windowId)) return false;

            if (!Modules.TryGet(out UISystem ui) || ui == null || !ui.isInited)
            {
                Log.Debug("[UIRouter] Back：UISystem 未初始化，已清空栈");
                stack.Clear();
                return false;
            }

            return TryCloseWindow(windowId, "Back");
        }

        /// <summary>关闭一个窗口并记录日志；异常隔离，返回是否成功。</summary>
        private static bool TryCloseWindow(string windowId, string reason)
        {
            if (string.IsNullOrEmpty(windowId)) return false;
            if (!Modules.TryGet(out UISystem ui) || ui == null || !ui.isInited) return false;

            try
            {
                ui.CloseWindow(windowId);
                Log.Debug("[UIRouter] {0} 关闭 {1}（栈: {2}）", reason, windowId, stack);
                return true;
            }
            catch (Exception ex)
            {
                Log.Error("[UIRouter] {0} 关闭 {1} 异常: {2}", reason, windowId, ex.Message);
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
        /// <para>
        /// 自愈：UISystem.Clear()/Init() 会 Dispose 并重建 Subject。这里按"订阅时的 Subject 实例"
        /// 判断是否需要重挂，避免重建后返回键、栈同步、模态解除全部静默失效。
        /// </para>
        /// </summary>
        private static void EnsureHooked()
        {
            Modules.TryGet(out UISystem ui);
            Modules.TryGet(out UIInputSystem input);

            var currentCloseSubject = ui != null ? ui.OnCloseSubject : null;
            var currentCancelSubject = input != null ? input.CancelSubject : null;

            if (hooked)
            {
                // 仍然挂在同一对 Subject 上：无事可做（包括"当时两个 Subject 都为 null"的场景——
                // 若后来系统初始化完成，实例变化会走到下面的重挂分支）
                if (ReferenceEquals(hookedCloseSubject, currentCloseSubject)
                    && ReferenceEquals(hookedCancelSubject, currentCancelSubject))
                {
                    return;
                }

                // 系统重建过：丢弃指向旧 Subject 的订阅，下面重挂
                cancelSubscription?.Dispose();
                cancelSubscription = null;
                closeSubscription?.Dispose();
                closeSubscription = null;
            }

            hooked = true;
            hookedCloseSubject = currentCloseSubject;
            hookedCancelSubject = currentCancelSubject;

            // 1) 返回键：焦点域没有消费取消键时，UIInputSystem 才会广播 CancelSubject
            try
            {
                if (currentCancelSubject != null)
                {
                    cancelSubscription = currentCancelSubject.Subscribe(_ =>
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
                if (currentCloseSubject != null)
                {
                    closeSubscription = currentCloseSubject.Subscribe(window =>
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

        /// <summary>
        /// UISystem 即将 Clear 时调用：取消所有等待中的结果（返回 None，不抛异常）、
        /// 清空导航栈与模态、断开订阅。否则调用方会永久挂在 await，且旧栈/模态残留到下一次会话。
        /// </summary>
        internal static void NotifySystemClearing()
        {
            // 先完成等待者：slot.Cancel 内部 TrySetResult(None)，await 会正常返回而不是抛异常
            if (pendingResults.Count > 0)
            {
                var slots = new List<IWindowResultSlot>(pendingResults.Values);
                pendingResults.Clear();
                for (int i = 0; i < slots.Count; i++)
                {
                    try
                    {
                        slots[i].Cancel("UISystem 已清理");
                    }
                    catch (Exception ex)
                    {
                        // 某个等待者恢复时抛异常不能阻断其余等待者完成与后续清理
                        Log.Error("[UIRouter] 取消窗口结果时等待者异常（已隔离）: {0}", ex.Message);
                    }
                }
            }

            stack.Clear();
            ResetModal();

            cancelSubscription?.Dispose();
            cancelSubscription = null;
            closeSubscription?.Dispose();
            closeSubscription = null;
            hooked = false;
            hookedCloseSubject = null;
            hookedCancelSubject = null;
        }

        /// <summary>域重载关闭时静态栈不会自动复位，这里跟随 Play 会话复位。</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            Reset();
        }
    }
}
