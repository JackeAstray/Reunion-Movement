using ReunionMovement.Common;
using System.Collections.Generic;
using UnityEngine;

namespace ReunionMovement.Framework.Runtime.UI
{
    /// <summary>
    /// <see cref="UIRouter"/> 的"模态"部分：模态窗口打开时，隔离下层窗口的交互（可选变暗）。
    ///
    /// <para>
    /// 特点：**不需要改预制体**。缺 <c>CanvasGroup</c> 的窗口在运行时补一个，
    /// 关闭模态时按记录的原值还原（<c>interactable</c> 与 <c>alpha</c> 都会还原，不覆盖美术配置）。
    /// </para>
    ///
    /// <para>
    /// 语义：
    /// <list type="bullet">
    /// <item>只有声明了 <c>[UIWindow(Modal = true)]</c> 的窗口才会触发隔离；</item>
    /// <item>支持嵌套：同时只让**最上层的模态**可交互，下层模态和普通窗口一起被阻断；</item>
    /// <item>模态关闭后按"还剩几层模态"重算，而不是简单全放开（否则嵌套时会误放开下层模态）。</item>
    /// </list>
    /// </para>
    /// </summary>
    public static partial class UIRouter
    {
        /// <summary>模态打开时是否把下层窗口变暗。默认关闭（保持原视觉，只阻断交互）。</summary>
        public static bool DimBackground { get; set; }

        /// <summary>变暗时的目标 alpha（<see cref="DimBackground"/> 为 true 时生效）。</summary>
        public static float DimAlpha { get; set; } = 0.6f;

        /// <summary>当前模态栈（从下到上）。</summary>
        private static readonly List<string> modalStack = new List<string>(4);

        /// <summary>被改过的 CanvasGroup 原值：窗口名 → (interactable, alpha)。</summary>
        private static readonly Dictionary<string, (bool interactable, float alpha)> blockedWindows =
            new Dictionary<string, (bool, float)>();

        /// <summary>当前是否有模态窗口。</summary>
        public static bool HasModal => modalStack.Count > 0;

        /// <summary>当前模态层数。</summary>
        public static int ModalDepth => modalStack.Count;

        /// <summary>压入一个窗口后调用：登记模态并重算阻断。</summary>
        internal static void NotifyPushed(string windowId, bool modal)
        {
            if (string.IsNullOrEmpty(windowId)) return;

            if (modal)
            {
                modalStack.Remove(windowId);
                modalStack.Add(windowId);
            }

            // 任何窗口打开都可能改变"谁该被阻断"（例如模态开着时又开了一个普通窗口），统一重算
            Recompute();
        }

        /// <summary>窗口关闭后调用：出栈模态并重算阻断。</summary>
        internal static void NotifyClosed(string windowId)
        {
            if (string.IsNullOrEmpty(windowId)) return;

            modalStack.Remove(windowId);
            Recompute();
        }

        /// <summary>清空模态状态并还原所有被阻断的窗口（重置/退出登录等场景）。</summary>
        public static void ResetModal()
        {
            modalStack.Clear();
            RestoreAll();
        }

        /// <summary>
        /// 重算阻断关系：
        /// 有模态时，只有"最上层模态"可交互，其余全部阻断；
        /// 没有模态时，全部还原。
        /// </summary>
        private static void Recompute()
        {
            if (!Modules.TryGet(out UISystem ui) || ui == null || !ui.isInited)
            {
                RestoreAll();
                return;
            }

            string topModal = modalStack.Count == 0 ? null : modalStack[modalStack.Count - 1];

            if (topModal == null)
            {
                RestoreAll();
                return;
            }

            var open = new List<UIController>(8);
            ui.GetAllOpenWindows(open);

            for (int i = 0; i < open.Count; i++)
            {
                var window = open[i];
                if (window == null) continue;

                // 窗口名就是窗口 Id（SetupLoadedWindow 里已把 GameObject 名设为逻辑名）
                string id = window.gameObject.name;
                if (string.Equals(id, topModal, System.StringComparison.Ordinal))
                {
                    Unblock(id, window);
                }
                else
                {
                    Block(id, window);
                }
            }
        }

        private static void Block(string id, UIController window)
        {
            if (id == null || blockedWindows.ContainsKey(id)) return;

            var group = window.GetComponent<CanvasGroup>();
            if (group == null) group = window.gameObject.AddComponent<CanvasGroup>();

            blockedWindows[id] = (group.interactable, group.alpha);

            group.interactable = false;
            if (DimBackground) group.alpha = Mathf.Clamp01(DimAlpha);
        }

        private static void Unblock(string id, UIController window)
        {
            if (id == null || !blockedWindows.TryGetValue(id, out var original)) return;

            blockedWindows.Remove(id);
            if (window == null) return;

            var group = window.GetComponent<CanvasGroup>();
            if (group == null) return;

            group.interactable = original.interactable;
            group.alpha = original.alpha;
        }

        private static void RestoreAll()
        {
            if (blockedWindows.Count == 0) return;

            if (Modules.TryGet(out UISystem ui) && ui != null && ui.isInited)
            {
                var open = new List<UIController>(8);
                ui.GetAllOpenWindows(open);
                for (int i = 0; i < open.Count; i++)
                {
                    var window = open[i];
                    if (window == null) continue;
                    Unblock(window.gameObject.name, window);
                }
            }

            // 剩下的（窗口已销毁/已关闭）直接丢弃记录，避免残留
            blockedWindows.Clear();
        }
    }
}
