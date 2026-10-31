using System;
using System.Collections.Generic;

namespace Gear.Framework.Runtime.UI
{
    /// <summary>
    /// 窗口导航栈 —— 纯 C# 逻辑，不依赖 UnityEngine，可直接单元测试。
    ///
    /// <para>
    /// 语义：
    /// <list type="bullet">
    /// <item><see cref="Push"/> 把窗口压到栈顶；已在栈里则先移除再压（不产生重复项）；</item>
    /// <item><see cref="TryPop"/> 弹出栈顶（"返回"就是弹栈 + 关窗口）；</item>
    /// <item><see cref="Remove"/> 用于"窗口被直接关掉"时同步栈，避免栈里留下已关闭的窗口。</item>
    /// </list>
    /// </para>
    /// </summary>
    public sealed class UINavigationStack
    {
        private readonly List<string> entries = new List<string>(8);

        /// <summary>栈深度。</summary>
        public int Count => entries.Count;

        /// <summary>栈是否为空（为空表示"没有可返回的窗口"）。</summary>
        public bool IsEmpty => entries.Count == 0;

        /// <summary>栈顶窗口 Id；空栈返回 null。</summary>
        public string Top => entries.Count == 0 ? null : entries[entries.Count - 1];

        /// <summary>从栈底到栈顶的只读快照（调试/日志用）。</summary>
        public IReadOnlyList<string> Entries => entries;

        /// <summary>是否已在栈里。</summary>
        public bool Contains(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            return entries.Contains(id);
        }

        /// <summary>压栈（重复项会先被移到栈顶）。</summary>
        public void Push(string id)
        {
            if (string.IsNullOrEmpty(id)) return;
            entries.Remove(id);
            entries.Add(id);
        }

        /// <summary>弹出栈顶。</summary>
        /// <param name="id">被弹出的窗口 Id</param>
        /// <returns>栈为空时返回 false</returns>
        public bool TryPop(out string id)
        {
            if (entries.Count == 0)
            {
                id = null;
                return false;
            }
            id = entries[entries.Count - 1];
            entries.RemoveAt(entries.Count - 1);
            return true;
        }

        /// <summary>移除指定窗口（窗口被直接关闭时同步栈）。</summary>
        public bool Remove(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            return entries.Remove(id);
        }

        /// <summary>清空。</summary>
        public void Clear()
        {
            entries.Clear();
        }

        /// <summary>以 "A → B → C" 形式输出，便于日志。</summary>
        public override string ToString()
        {
            return entries.Count == 0 ? "(空)" : string.Join(" → ", entries);
        }
    }
}
