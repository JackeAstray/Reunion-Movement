using ReunionMovement.Common;
using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;

namespace ReunionMovement.Core.UI
{
    /// <summary>
    /// <see cref="UIRouter"/> 的"页面返回值"部分：打开窗口时挂一个等待槽，窗口关闭时把结果交回来。
    ///
    /// <para>
    /// 三种结局：
    /// <list type="number">
    /// <item>窗口调用 <see cref="CloseWithResult{TResult}"/> → <c>HasValue = true</c>；</item>
    /// <item>窗口被直接关闭（点关闭按钮 / 场景切换）→ <c>HasValue = false</c>（视为取消）；</item>
    /// <item>窗口加载失败 → 立刻返回 <c>HasValue = false</c>，不会有人永远等下去。</item>
    /// </list>
    /// </para>
    /// </summary>
    public static partial class UIRouter
    {
        /// <summary>等待结果的槽（非泛型接口，便于按窗口 Id 存放）。</summary>
        private interface IWindowResultSlot
        {
            void Complete(object value);
            void Cancel(string reason);
        }

        private sealed class WindowResultSlot<TResult> : IWindowResultSlot
        {
            private readonly UniTaskCompletionSource<WindowResult<TResult>> source =
                new UniTaskCompletionSource<WindowResult<TResult>>();

            public UniTask<WindowResult<TResult>> Task => source.Task;

            public void Complete(object value)
            {
                if (value is TResult typed)
                {
                    source.TrySetResult(new WindowResult<TResult>(typed));
                }
                else
                {
                    Log.Warning("[UIRouter] 窗口回传的结果类型不符：期望 {0}，实际 {1}，按取消处理",
                        typeof(TResult).Name, value?.GetType().Name ?? "null");
                    source.TrySetResult(WindowResult<TResult>.None);
                }
            }

            public void Cancel(string reason)
            {
                if (!string.IsNullOrEmpty(reason))
                {
                    Log.Debug("[UIRouter] 窗口结果取消：{0}", reason);
                }
                source.TrySetResult(WindowResult<TResult>.None);
            }
        }

        /// <summary>窗口 Id → 等待槽。同一窗口同时只允许一个等待者（UI 语义上也不该有两个）。</summary>
        private static readonly Dictionary<string, IWindowResultSlot> pendingResults =
            new Dictionary<string, IWindowResultSlot>(StringComparer.Ordinal);

        /// <summary>
        /// 打开窗口并等待它回传结果（无参数版本）。
        /// </summary>
        public static UniTask<WindowResult<TResult>> PushForResultAsync<TWindow, TResult>()
            where TWindow : UIController
        {
            return PushForResultAsync<TWindow, NoWindowArgs, TResult>(NoWindowArgs.Default);
        }

        /// <summary>
        /// 打开窗口并等待它回传结果。
        /// </summary>
        /// <typeparam name="TWindow">窗口控制器类型</typeparam>
        /// <typeparam name="TArgs">窗口参数类型</typeparam>
        /// <typeparam name="TResult">结果类型</typeparam>
        /// <param name="args">窗口参数</param>
        /// <returns>窗口回传的结果；被直接关闭/加载失败时为 <see cref="WindowResult{T}.None"/></returns>
        public static async UniTask<WindowResult<TResult>> PushForResultAsync<TWindow, TArgs, TResult>(TArgs args)
            where TWindow : UIController
            where TArgs : IWindowArgs
        {
            string windowId = UIWindowCatalog.GetId(typeof(TWindow));

            if (pendingResults.ContainsKey(windowId))
            {
                Log.Warning("[UIRouter] {0} 已有等待中的结果请求，忽略本次", windowId);
                return WindowResult<TResult>.None;
            }

            var slot = new WindowResultSlot<TResult>();
            pendingResults[windowId] = slot;

            try
            {
                UILoadState state = await PushAsync<TWindow, TArgs>(args);
                if (state?.uiWindow == null)
                {
                    // 加载失败：立刻返回取消，避免调用方永久等待
                    return WindowResult<TResult>.None;
                }

                return await slot.Task;
            }
            finally
            {
                pendingResults.Remove(windowId);
            }
        }

        /// <summary>
        /// 关闭栈顶窗口并回传结果（窗口内部的"确定/取消"按钮调这个）。
        /// </summary>
        /// <typeparam name="TResult">结果类型</typeparam>
        /// <param name="result">结果</param>
        /// <returns>是否成功关闭了一个窗口</returns>
        public static bool CloseWithResult<TResult>(TResult result)
        {
            string windowId = stack.Top;
            if (string.IsNullOrEmpty(windowId))
            {
                Log.Warning("[UIRouter] CloseWithResult：栈为空，没有可关闭的窗口");
                return false;
            }
            return CloseWindowWithResult(windowId, result);
        }

        /// <summary>
        /// 关闭指定窗口并回传结果（不要求它在栈顶）。
        /// </summary>
        public static bool CloseWindowWithResult<TWindow, TResult>(TResult result)
            where TWindow : UIController
        {
            return CloseWindowWithResult(UIWindowCatalog.GetId(typeof(TWindow)), result);
        }

        /// <summary>
        /// 关闭指定窗口并回传结果（按窗口 Id）。
        /// </summary>
        public static bool CloseWindowWithResult<TResult>(string windowId, TResult result)
        {
            if (string.IsNullOrEmpty(windowId)) return false;

            // 先回传再关闭：关闭会触发 OnCloseSubject，若不先置结果会被当成"取消"
            CompletePendingResult(windowId, result);

            if (!Modules.TryGet(out UISystem ui) || ui == null || !ui.isInited) return false;

            bool wasOpen = ui.IsOpen(windowId);
            stack.Remove(windowId);
            ui.CloseWindow(windowId);
            return wasOpen;
        }

        /// <summary>把结果交给等待槽（没有等待者时说明窗口不是用路由打开的，只记调试日志）。</summary>
        private static void CompletePendingResult(string windowId, object value)
        {
            if (windowId == null) return;
            if (pendingResults.TryGetValue(windowId, out var slot))
            {
                slot.Complete(value);
            }
            else
            {
                Log.Debug("[UIRouter] {0} 回传结果但没有等待者（可能不是通过 PushForResultAsync 打开的）", windowId);
            }
        }

        /// <summary>
        /// 窗口被关闭时同步：有等待中的结果请求就按"取消"处理，避免调用方永远挂在 await 上。
        /// 由主文件里的 OnCloseSubject 订阅调用。
        /// </summary>
        private static void CancelPendingResult(string windowId, string reason)
        {
            if (string.IsNullOrEmpty(windowId)) return;
            if (pendingResults.TryGetValue(windowId, out var slot))
            {
                slot.Cancel(reason);
                pendingResults.Remove(windowId);
            }
        }
    }
}
