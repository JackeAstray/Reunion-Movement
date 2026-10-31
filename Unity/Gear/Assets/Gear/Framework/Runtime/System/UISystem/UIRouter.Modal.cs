using Gear.Common;
using System.Collections.Generic;
using UnityEngine;

namespace Gear.Framework.Runtime.UI
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

        /// <summary>被改过的 CanvasGroup 原值：窗口名 → (交互, 透明度, 具体实例)。</summary>
        private static readonly Dictionary<string, (bool interactable, float alpha, CanvasGroup group)> blockedWindows =
            new Dictionary<string, (bool, float, CanvasGroup)>();

        /// <summary>当前是否有模态窗口。</summary>
        public static bool HasModal => modalStack.Count > 0;

        /// <summary>当前模态层数。</summary>
        public static int ModalDepth => modalStack.Count;

        /// <summary>
        /// 是否存在仍打开的模态窗口。
        /// 会跳过"已关闭但还留在栈里"的残留项（不修改栈，避免属性读取产生副作用）。
        /// </summary>
        public static bool HasOpenModal
        {
            get
            {
                if (modalStack.Count == 0) return false;
                if (!Modules.TryGet(out UISystem ui) || ui == null || !ui.isInited)
                {
                    return modalStack.Count > 0;
                }
                for (int i = modalStack.Count - 1; i >= 0; i--)
                {
                    if (ui.IsOpen(modalStack[i])) return true;
                }
                return false;
            }
        }

        /// <summary>
        /// 取出最上层仍打开的模态窗口，并从模态栈移除（只出栈，不负责关闭）。
        /// 供 <see cref="UIRouter.Back"/> 使用：模态不一定进导航栈，必须单独处理。
        /// </summary>
        internal static bool TryPopTopModal(out string windowId)
        {
            windowId = null;
            if (modalStack.Count == 0) return false;
            if (!Modules.TryGet(out UISystem ui) || ui == null || !ui.isInited) return false;

            while (modalStack.Count > 0)
            {
                int last = modalStack.Count - 1;
                string candidate = modalStack[last];
                modalStack.RemoveAt(last);
                if (ui.IsOpen(candidate))
                {
                    windowId = candidate;
                    return true;
                }
            }
            return false;
        }

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
            // 先还原这个窗口自己的 CanvasGroup 并删记录：
            // 若它曾是被阻断的下层窗口，关闭后记录会一直留在 blockedWindows 里，
            // 同名窗口重建时被当成"已阻断"而跳过，或者永远不还原。
            RestoreWindowBlock(windowId);
            Recompute();
        }

        /// <summary>按窗口名还原并删除单条阻断记录（不依赖窗口实例，已销毁的窗口也能清掉）。</summary>
        private static void RestoreWindowBlock(string windowId)
        {
            if (!blockedWindows.TryGetValue(windowId, out var original)) return;
            blockedWindows.Remove(windowId);
            var group = original.group;
            if (group == null) return;
            group.interactable = original.interactable;
            group.alpha = original.alpha;
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

            // 剔除当前已不可见的模态（被 SetActive(false)/HideAllWindows 隐藏，或已销毁）：
            // 否则 topModal 指向一个看不见的窗口，下面会把其余所有可见窗口全部阻断 ——
            // 而唯一"该可交互"的顶层模态又不在可见集合里，表现为整个 UI 卡死。
            while (modalStack.Count > 0 && !ui.IsOpen(modalStack[modalStack.Count - 1]))
            {
                modalStack.RemoveAt(modalStack.Count - 1);
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
            if (id == null || window == null) return;

            var group = window.GetComponent<CanvasGroup>();
            if (group == null) group = window.gameObject.AddComponent<CanvasGroup>();

            // 同名窗口可能是"销毁后重建"的新实例：旧记录里的 group 已失效，
            // 不能因为 ContainsKey 就直接跳过（否则新实例永远不被阻断，模态失效）。
            if (blockedWindows.TryGetValue(id, out var existing))
            {
                if (existing.group == group) return;
                if (existing.group != null)
                {
                    existing.group.interactable = existing.interactable;
                    existing.group.alpha = existing.alpha;
                }
                blockedWindows.Remove(id);
            }

            blockedWindows[id] = (group.interactable, group.alpha, group);

            group.interactable = false;
            if (DimBackground) group.alpha = Mathf.Clamp01(DimAlpha);
        }

        private static void Unblock(string id, UIController window)
        {
            if (id == null || !blockedWindows.TryGetValue(id, out var original)) return;

            blockedWindows.Remove(id);
            if (window == null) return;

            var group = window.GetComponent<CanvasGroup>();
            // 实例不匹配说明窗口已销毁重建：不能把旧值还原到新实例上
            if (group == null || group != original.group) return;

            group.interactable = original.interactable;
            group.alpha = original.alpha;
        }

        /// <summary>
        /// 还原所有被改过的 CanvasGroup。
        /// 直接按记录里的实例还原，而不是遍历"当前打开"的窗口：
        /// 被隐藏/已关闭的窗口也要还原，且不会因为查不到窗口而把记录留成脏数据。
        /// </summary>
        private static void RestoreAll()
        {
            if (blockedWindows.Count == 0) return;

            foreach (var kv in blockedWindows)
            {
                var group = kv.Value.group;
                if (group == null) continue;
                group.interactable = kv.Value.interactable;
                group.alpha = kv.Value.alpha;
            }
            blockedWindows.Clear();
        }
    }
}
