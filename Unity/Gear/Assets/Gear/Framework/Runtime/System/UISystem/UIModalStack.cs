using System;
using System.Collections.Generic;

namespace Gear.Framework.Runtime.UI
{
    /// <summary>
    /// 模态窗口栈（纯 C#，不依赖 Unity / UISystem），便于单独测试返回键的关闭顺序。
    /// <para>
    /// 语义：后打开的在栈顶；<see cref="TryPopTop"/> 从栈顶向下找第一个仍打开的窗口，
    /// 顺手丢弃已关闭的残留项。UIRouter 只负责把"是否仍打开"的判定委托进来，
    /// 这样"先关模态、再关导航栈"的顺序就可以在 EditMode 里验证。
    /// </para>
    /// </summary>
    public sealed class UIModalStack
    {
        private readonly List<string> items = new List<string>(4);

        public int Count => items.Count;

        public bool IsEmpty => items.Count == 0;

        /// <summary>栈顶 Id（可能已关闭；需要"仍打开"请用 <see cref="TopOpen"/>）。</summary>
        public string Top => items.Count == 0 ? null : items[items.Count - 1];

        public void Push(string windowId)
        {
            if (string.IsNullOrEmpty(windowId)) return;
            // 重复打开同一窗口：移到栈顶，避免同一 Id 在栈里出现两次
            items.Remove(windowId);
            items.Add(windowId);
        }

        public bool Remove(string windowId)
        {
            return !string.IsNullOrEmpty(windowId) && items.Remove(windowId);
        }

        public void Clear()
        {
            items.Clear();
        }

        /// <summary>是否存在仍打开的模态（不修改栈）。</summary>
        public bool HasOpen(Func<string, bool> isOpen)
        {
            if (isOpen == null) return items.Count > 0;
            for (int i = items.Count - 1; i >= 0; i--)
            {
                if (isOpen(items[i])) return true;
            }
            return false;
        }

        /// <summary>最上层仍打开的模态 Id；没有则返回 null（不修改栈）。</summary>
        public string TopOpen(Func<string, bool> isOpen)
        {
            if (isOpen == null) return Top;
            for (int i = items.Count - 1; i >= 0; i--)
            {
                if (isOpen(items[i])) return items[i];
            }
            return null;
        }

        /// <summary>丢弃栈顶已关闭的残留项（调用方应先确认 UISystem 已初始化）。</summary>
        public void PruneClosed(Func<string, bool> isOpen)
        {
            if (isOpen == null) return;
            while (items.Count > 0 && !isOpen(items[items.Count - 1]))
            {
                items.RemoveAt(items.Count - 1);
            }
        }

        /// <summary>
        /// 取出最上层仍打开的模态并从栈中移除（只出栈，不负责关闭窗口）。
        /// 已关闭的残留项会被丢弃；找不到时返回 false。
        /// </summary>
        public bool TryPopTop(Func<string, bool> isOpen, out string windowId)
        {
            windowId = null;
            while (items.Count > 0)
            {
                int last = items.Count - 1;
                string candidate = items[last];
                items.RemoveAt(last);
                if (isOpen == null || isOpen(candidate))
                {
                    windowId = candidate;
                    return true;
                }
            }
            return false;
        }
    }
}
