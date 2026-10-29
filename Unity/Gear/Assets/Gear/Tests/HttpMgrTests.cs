using NUnit.Framework;
using Gear.Framework.Modules.Http;

namespace Gear.Tests
{
    /// <summary>
    /// HttpMgr 纯 C# 单例的 EditMode 测试：只验证生命周期，不发真实网络请求。
    /// </summary>
    public class HttpMgrTests
    {
        [TearDown]
        public void TearDown()
        {
            HttpMgr.ResetStatics();
        }

        [Test]
        public void ResetStatics_Creates_New_Instance()
        {
            var first = HttpMgr.Instance;
            HttpMgr.ResetStatics();
            var second = HttpMgr.Instance;

            Assert.AreNotSame(first, second, "ResetStatics 必须换掉纯 C# 单例，避免跨 Play 会话残留请求/服务");
        }

        [Test]
        public void Tick_Without_Requests_Is_Safe()
        {
            // 空请求列表下轮询；不抛异常即通过
            HttpMgr.Instance.Tick();
        }
    }
}
