using NUnit.Framework;
using Gear.Framework.Runtime.UI;

namespace Gear.Tests
{
    /// <summary>
    /// 模态栈纯逻辑测试：覆盖 UIRouter.Back 的关闭顺序核心——
    /// 后打开的在栈顶、已关闭的残留项被跳过、重复打开只保留一份。
    /// </summary>
    public class UIModalStackTests
    {
        [Test]
        public void Push_SameIdTwice_KeepsSingleEntryAtTop()
        {
            var stack = new UIModalStack();
            stack.Push("A");
            stack.Push("B");
            stack.Push("A");

            Assert.AreEqual(2, stack.Count);
            Assert.AreEqual("A", stack.Top);
        }

        [Test]
        public void TryPopTop_ReturnsTopOpenAndSkipsClosed()
        {
            var stack = new UIModalStack();
            stack.Push("bottom");
            stack.Push("middle");
            stack.Push("top");

            // top 已关闭：应跳过它并返回 middle，同时把 top 从栈里剔除
            bool popped = stack.TryPopTop(id => id != "top", out string windowId);

            Assert.IsTrue(popped);
            Assert.AreEqual("middle", windowId);
            Assert.AreEqual(1, stack.Count);
            Assert.AreEqual("bottom", stack.Top);
        }

        [Test]
        public void TryPopTop_Empty_ReturnsFalse()
        {
            var stack = new UIModalStack();
            Assert.IsFalse(stack.TryPopTop(_ => true, out string windowId));
            Assert.IsNull(windowId);
        }

        [Test]
        public void TryPopTop_AllClosed_ReturnsFalseAndEmpties()
        {
            var stack = new UIModalStack();
            stack.Push("A");
            stack.Push("B");

            Assert.IsFalse(stack.TryPopTop(_ => false, out _));
            Assert.AreEqual(0, stack.Count);
        }

        [Test]
        public void PruneClosed_RemovesOnlyClosedTopEntries()
        {
            var stack = new UIModalStack();
            stack.Push("A");
            stack.Push("B");
            stack.Push("C");

            stack.PruneClosed(id => id != "C");

            Assert.AreEqual(2, stack.Count);
            Assert.AreEqual("B", stack.Top);
        }

        [Test]
        public void HasOpenAndTopOpen_IgnoreClosedEntries()
        {
            var stack = new UIModalStack();
            stack.Push("A");
            stack.Push("B");

            Assert.IsTrue(stack.HasOpen(id => id == "A"));
            Assert.AreEqual("A", stack.TopOpen(id => id == "A"));
            Assert.IsFalse(stack.HasOpen(_ => false));
            Assert.IsNull(stack.TopOpen(_ => false));
        }

        [Test]
        public void RemoveAndClear_Work()
        {
            var stack = new UIModalStack();
            stack.Push("A");
            stack.Push("B");

            Assert.IsTrue(stack.Remove("A"));
            Assert.IsFalse(stack.Remove("missing"));
            Assert.AreEqual(1, stack.Count);

            stack.Clear();
            Assert.IsTrue(stack.IsEmpty);
        }
    }
}
