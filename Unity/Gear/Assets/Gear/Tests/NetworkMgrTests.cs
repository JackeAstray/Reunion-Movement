using NUnit.Framework;
using Gear.Framework.Modules;

namespace Gear.Tests
{
    /// <summary>
    /// NetworkMgr 纯 C# 单例的 EditMode 测试：只验证生命周期，不建立真实连接。
    /// </summary>
    public class NetworkMgrTests
    {
        [TearDown]
        public void TearDown()
        {
            NetworkMgr.ResetStatics();
        }

        [Test]
        public void ResetStatics_Creates_New_Instance()
        {
            var first = NetworkMgr.Instance;
            NetworkMgr.ResetStatics();
            var second = NetworkMgr.Instance;

            Assert.AreNotSame(first, second, "ResetStatics 必须换掉纯 C# 单例，避免跨 Play 会话残留通道");
        }

        [Test]
        public void Tick_Without_Channels_Is_Safe()
        {
            NetworkMgr.Instance.Tick();
        }

        [Test]
        public void Clear_Without_Channels_Is_Safe()
        {
            NetworkMgr.Instance.Clear();
        }
    }
}
