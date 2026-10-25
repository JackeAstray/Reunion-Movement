using ReunionMovement.Common;
using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace ReunionMovement.Core.UI
{
    /// <summary>
    /// <see cref="UISystem"/> 的类型化窗口 API（增量，不动老接口）。
    ///
    /// <para>
    /// 老写法靠位置参数：<c>OpenWindowAsync("PopupUIPlane", "错误", 3)</c> + 窗口里 <c>GetFromArgs&lt;int&gt;(args, 1)</c>。
    /// 新写法把"参数"变成一个类型：
    /// </para>
    ///
    /// <code>
    /// public readonly record struct ShopArgs(string Category, int Page) : IWindowArgs;
    ///
    /// [UIWindow("ShopUI", Pooled = true)]
    /// public sealed class ShopUI : UIController, IWindowArgsReceiver&lt;ShopArgs&gt;
    /// {
    ///     public void OnWindowArgs(ShopArgs args) { /* 强类型，改字段编译期就报错 */ }
    /// }
    ///
    /// var state = await Modules.Get&lt;UISystem&gt;().OpenWindowAsync&lt;ShopUI, ShopArgs&gt;(new ShopArgs("weapon", 1));
    /// </code>
    /// </summary>
    public partial class UISystem
    {
        /// <summary>参数类型 → 泛型接收方法（避免每次打开都做反射查找）。</summary>
        private static readonly Dictionary<Type, MethodInfo> argsReceiverMethodCache = new Dictionary<Type, MethodInfo>();

        /// <summary>取窗口 Id：优先 <see cref="UIWindowAttribute.Id"/>，回退类名（与老规则一致）。</summary>
        public static string GetWindowId<TWindow>() where TWindow : UIController
        {
            return UIWindowCatalog.GetId(typeof(TWindow));
        }

        /// <summary>
        /// [推荐] 用强类型参数打开窗口 —— 窗口实现 <see cref="IWindowArgsReceiver{T}"/> 时自动收到参数。
        /// </summary>
        /// <typeparam name="TWindow">窗口控制器类型</typeparam>
        /// <typeparam name="TArgs">参数类型</typeparam>
        /// <param name="args">参数</param>
        /// <returns>窗口加载状态；加载失败返回 null（与老接口一致）</returns>
        public async UniTask<UILoadState> OpenWindowAsync<TWindow, TArgs>(TArgs args)
            where TWindow : UIController
            where TArgs : IWindowArgs
        {
            string windowId = UIWindowCatalog.GetId(typeof(TWindow));
            UILoadState state = await OpenWindowAsync(windowId, args);

            DispatchTypedArgs(state?.uiWindow, args);
            return state;
        }

        /// <summary>
        /// 用强类型参数打开窗口（不显式写参数类型）—— 参数类型由运行时取。
        /// 需要编译期检查时请用 <see cref="OpenWindowAsync{TWindow, TArgs}"/>。
        /// </summary>
        public async UniTask<UILoadState> OpenWindowAsync<TWindow>(IWindowArgs args) where TWindow : UIController
        {
            string windowId = UIWindowCatalog.GetId(typeof(TWindow));
            UILoadState state = await OpenWindowAsync(windowId, args);

            DispatchTypedArgs(state?.uiWindow, args);
            return state;
        }

        /// <summary>
        /// 把强类型参数派发给窗口：窗口实现了 <see cref="IWindowArgsReceiver{T}"/> 才调用，否则静默跳过
        /// （仍可用老方式从 <see cref="UIController.OnOpen"/> 的 object[] 里取）。
        /// </summary>
        private static void DispatchTypedArgs(UIController window, IWindowArgs args)
        {

            if (window == null || args == null) return;

            Type argsType = args.GetType();
            if (!argsReceiverMethodCache.TryGetValue(argsType, out MethodInfo method))
            {
                Type receiverType = typeof(IWindowArgsReceiver<>).MakeGenericType(argsType);
                if (!receiverType.IsInstanceOfType(window))
                {
                    // 窗口没实现该参数类型的接收接口：记一次调试日志即可（不算错误，老窗口就该走 OnOpen）
                    Log.Debug("[UISystem] {0} 未实现 IWindowArgsReceiver<{1}>，参数只通过 OnOpen(object[]) 传递",
                        window.GetType().Name, argsType.Name);
                    return;
                }
                // 显式接口实现的方法不是 public，必须带上 NonPublic 才能取到
                method = receiverType.GetMethod("OnWindowArgs", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                argsReceiverMethodCache[argsType] = method;
            }

            if (method == null) return;

            try
            {
                method.Invoke(window, new object[] { args });
            }
            catch (TargetInvocationException ex)
            {
                Log.Error("[UISystem] {0}.OnWindowArgs({1}) 抛异常（已隔离）: {2}",
                    window.GetType().Name, argsType.Name, (ex.InnerException ?? ex).Message);
            }
            catch (Exception ex)
            {
                Log.Error("[UISystem] 派发窗口参数失败（已隔离）: {0}", ex.Message);
            }
        }

        /// <summary>
        /// 派发强类型参数；窗口未打开时先加载并打开（与旧 <c>SetWindow</c> 语义一致）。
        /// 需要"窗口一定已打开"时用同步版 <see cref="SendWindowArgs"/>。
        /// </summary>
        public async UniTask SendWindowArgsAsync(string windowId, IWindowArgs args)
        {
            if (string.IsNullOrEmpty(windowId) || args == null) return;

            UIController window = FindOpenWindow(windowId);
            if (window == null)
            {
                // 旧 SetWindow 在窗口未加载时会先 LoadWindow(openWhenFinish: true)；
                // 这里保持一致，但**不进导航栈**（终端这类命令通道不该被 ESC 关掉）。
                UILoadState state = await LoadWindowAsync(windowId, true);
                window = state?.uiWindow;
            }

            if (window == null)
            {
                Log.Warning("[UISystem] SendWindowArgsAsync({0})：窗口加载失败，参数丢弃", windowId);
                return;
            }

            DispatchTypedArgs(window, args);
        }

        /// <summary>按窗口 Id 找已打开的窗口；找不到返回 null。</summary>
        private UIController FindOpenWindow(string windowId)
        {
            var open = new List<UIController>(8);
            GetAllOpenWindows(open);
            for (int i = 0; i < open.Count; i++)
            {
                if (open[i] != null && open[i].gameObject.name == windowId) return open[i];
            }
            return null;
        }
        /// <summary>给已打开的窗口派发强类型参数（不重新打开）。用于"命令通道"类场景。</summary>
        /// <param name="windowId">窗口 Id</param>
        /// <param name="args">参数</param>
        public void SendWindowArgs(string windowId, IWindowArgs args)
        {
            if (string.IsNullOrEmpty(windowId) || args == null) return;

            UIController window = FindOpenWindow(windowId);

            if (window == null)
            {
                Log.Debug("[UISystem] SendWindowArgs({0})：窗口未打开，参数丢弃（{1}）", windowId, args.GetType().Name);
                return;
            }

            DispatchTypedArgs(window, args);
        }
    }

    /// <summary>终端窗口的命令参数 —— 替代原 OnSet(object[]) 里按位置取 args[0]/args[1]。</summary>
    public readonly struct TerminalWindowArgs : IWindowArgs
    {
        public readonly string Command;
        public readonly string Payload;

        public TerminalWindowArgs(string command, string payload)
        {
            Command = command;
            Payload = payload;
        }

        public override string ToString() => Command + "(" + Payload + ")";
    }
}