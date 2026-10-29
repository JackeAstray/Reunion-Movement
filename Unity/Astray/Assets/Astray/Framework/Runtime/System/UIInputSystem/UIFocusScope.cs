using System;
using System.Collections.Generic;
using Astray.Common;
using UnityEngine;

namespace Astray.Framework.Runtime.UIInput
{
    /// <summary>
    /// 一个 UI 窗口的焦点域。窗口打开时压栈，关闭时出栈；
    /// UIInputSystem 只把输入派发给栈顶焦点域，替代 siblingIndex/priority 的窗口层级猜测。
    /// 同一焦点域内按 PlayerSlot 路由：每个玩家一个焦点目标栈，后注册的（子面板）优先，
    /// 关闭/禁用后自动回退到前一个（父面板）。选择组算一个目标。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UIFocusScope : MonoBehaviour
    {
        [Tooltip("窗口打开时是否请求 UI 控制模式；HUD 等常驻界面可关闭")]
        [SerializeField] private bool requestUIControl = true;

        private readonly Dictionary<int, List<IUINavigationTarget>> slotTargets =
            new Dictionary<int, List<IUINavigationTarget>>(4);
        private readonly List<IUINavigationTarget> anyTargets = new List<IUINavigationTarget>(2);
        private readonly List<IUINavigationTarget> targetBuffer = new List<IUINavigationTarget>(2);

        private bool scopeActive;

        public bool RequestUIControl
        {
            get => requestUIControl;
            set
            {
                if (requestUIControl == value) return;
                requestUIControl = value;
                if (!scopeActive) return;

                if (value) UIInputSystem.Instance.RequestUIControl(this);
                else UIInputSystem.Instance.ReleaseUIControl(this);
            }
        }

        public bool IsActive => isActiveAndEnabled && gameObject.activeInHierarchy;

        /// <summary>目标 Transform 是否属于本焦点域（用于阻止 EventSystem 回退到下层窗口）。</summary>
        public bool Contains(Transform target)
        {
            return target != null && (target == transform || target.IsChildOf(transform));
        }

        private void OnEnable()
        {
            scopeActive = true;
            UIInputSystem.Instance.PushFocusScope(this);
            Log.Debug("[UIFocusScope] 打开焦点域：{0}", gameObject.name);
        }

        private void OnDisable()
        {
            scopeActive = false;
            UIInputSystem.Instance.PopFocusScope(this);
            Log.Debug("[UIFocusScope] 关闭焦点域：{0}", gameObject.name);
        }

        public void BringToFront()
        {
            if (IsActive) UIInputSystem.Instance.BringFocusScopeToFront(this);
        }

        public void Register(IUINavigationTarget target)
        {
            if (IsDestroyed(target)) return;

            int slot = target.PlayerSlot;
            if (slot >= 0)
            {
                if (!slotTargets.TryGetValue(slot, out var targets))
                {
                    targets = new List<IUINavigationTarget>(2);
                    slotTargets[slot] = targets;
                }

                // 同一目标重复注册只提升到栈顶，不产生重复项
                int existing = targets.IndexOf(target);
                if (existing >= 0) targets.RemoveAt(existing);
                targets.Add(target);

                if (targets.Count > 1)
                {
                    Log.Debug("[UIFocusScope] Player {0} 焦点目标压栈：{1} 个，当前为 {2}",
                        slot, targets.Count, target.GetType().Name);
                }
            }
            else if (!anyTargets.Contains(target))
            {
                anyTargets.Add(target);
            }
        }

        public void Unregister(IUINavigationTarget target)
        {
            if (IsDestroyed(target)) return;

            int slot = target.PlayerSlot;
            if (slot >= 0 && slotTargets.TryGetValue(slot, out var targets))
            {
                targets.Remove(target);
                if (targets.Count == 0) slotTargets.Remove(slot);
            }
            anyTargets.Remove(target);
        }

        public bool HasTargetForPlayer(int playerIndex)
        {
            return GetTarget(playerIndex) != null;
        }

        private IUINavigationTarget GetTarget(int playerIndex)
        {
            if (slotTargets.TryGetValue(playerIndex, out var targets))
            {
                // 子面板后注册在栈顶；关闭/禁用后自动回退到父面板
                for (int i = targets.Count - 1; i >= 0; i--)
                {
                    var candidate = targets[i];
                    if (IsDestroyed(candidate))
                    {
                        targets.RemoveAt(i);
                        continue;
                    }
                    if (!candidate.AcceptsPlayer(playerIndex)) continue;
                    return candidate;
                }

                if (targets.Count == 0) slotTargets.Remove(playerIndex);
            }

            if (anyTargets.Count == 0) return null;

            targetBuffer.Clear();
            bool removedDestroyed = false;
            for (int i = 0; i < anyTargets.Count; i++)
            {
                var candidate = anyTargets[i];
                if (IsDestroyed(candidate))
                {
                    removedDestroyed = true;
                    continue;
                }
                if (!candidate.AcceptsPlayer(playerIndex)) continue;
                targetBuffer.Add(candidate);
            }

            if (removedDestroyed)
            {
                for (int i = anyTargets.Count - 1; i >= 0; i--)
                {
                    if (IsDestroyed(anyTargets[i])) anyTargets.RemoveAt(i);
                }
            }

            if (targetBuffer.Count == 0) return null;

            if (targetBuffer.Count > 1)
            {
                targetBuffer.Sort(TargetPriorityComparison);
            }

            var result = targetBuffer[0];
            targetBuffer.Clear();
            return result;
        }

        public bool HandleNavigate(in UINavigateEvent e)
        {
            var target = GetTarget(e.PlayerIndex);
            return target != null && target.HandleNavigate(e);
        }

        public bool HandleSubmit(in UIButtonEvent e)
        {
            var target = GetTarget(e.PlayerIndex);
            return target != null && target.HandleSubmit(e);
        }

        public bool HandleCancel(in UIButtonEvent e)
        {
            var target = GetTarget(e.PlayerIndex);
            return target != null && target.HandleCancel(e);
        }

        private static readonly Comparison<IUINavigationTarget> TargetPriorityComparison =
            (a, b) => (b != null ? b.InputPriority : int.MinValue)
                .CompareTo(a != null ? a.InputPriority : int.MinValue);

        private static bool IsDestroyed(IUINavigationTarget target)
        {
            return target == null || (target is UnityEngine.Object obj && obj == null);
        }
    }
}
