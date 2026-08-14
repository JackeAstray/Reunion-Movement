using NUnit.Framework;
using ReunionMovement.Core.Terminal;

namespace ReunionMovement.Tests
{
    /// <summary>
    /// 终端命令参数解析 EditMode 测试。
    /// </summary>
    public class CommandArgTests
    {
        [Test]
        public void Int_Parses()
        {
            var arg = new CommandArg { String = "42" };
            Assert.AreEqual(42, arg.Int);
        }

        [Test]
        public void Int_Invalid_ReturnsZero()
        {
            var arg = new CommandArg { String = "abc" };
            Assert.AreEqual(0, arg.Int);
        }

        [Test]
        public void Float_Parses()
        {
            var arg = new CommandArg { String = "1.5" };
            Assert.AreEqual(1.5f, arg.Float, 0.0001f);
        }

        [Test]
        public void Bool_Parses_CaseInsensitive()
        {
            Assert.IsTrue(new CommandArg { String = "TRUE" }.Bool);
            Assert.IsTrue(new CommandArg { String = "true" }.Bool);
            Assert.IsFalse(new CommandArg { String = "False" }.Bool);
            Assert.IsFalse(new CommandArg { String = "x" }.Bool, "非法值应返回 false");
        }

        [Test]
        public void ToString_ReturnsRawString()
        {
            var arg = new CommandArg { String = "hello" };
            Assert.AreEqual("hello", arg.ToString());
        }
    }
}
