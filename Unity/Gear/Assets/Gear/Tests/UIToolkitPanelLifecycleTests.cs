using Cysharp.Threading.Tasks;
using NUnit.Framework;
using Gear.Framework.Runtime.UIToolkit;
using System;
using UnityEngine.TestTools;

namespace Gear.Tests
{
    /// <summary>
    /// UIToolkitPanel 的打开钩子契约：<c>OnOpen</c> 负责同步状态，异步逻辑走 <c>OnOpenAsync</c>，
    /// 异常由基类统一记录（取代此前的 <c>async void</c> 写法）。
    /// </summary>
    public class UIToolkitPanelLifecycleTests
    {
        private sealed class ProbePanel : UIToolkitPanel
        {
            public int OpenCount;
            public object LastData;
            public bool ThrowOnOpen;

            protected override async UniTask OnOpenAsync(object data)
            {
                OpenCount++;
                LastData = data;
                if (ThrowOnOpen) throw new InvalidOperationException("probe-panel-boom");
                await UniTask.CompletedTask;
            }
        }

        [Test]
        public void OnOpen_InvokesAsyncHookOnce()
        {
            var panel = new ProbePanel();

            panel.OnOpen(null);

            Assert.AreEqual(1, panel.OpenCount, "OnOpen 应触发一次 OnOpenAsync");
            Assert.IsTrue(panel.IsOpen, "OnOpen 应立即置 IsOpen（不等待异步逻辑）");
        }

        [Test]
        public void OnOpen_PassesDataToAsyncHook()
        {
            var panel = new ProbePanel();
            var payload = new object();

            panel.OnOpen(payload);

            Assert.AreSame(payload, panel.LastData, "OnOpen(data) 的参数应原样传给 OnOpenAsync");
        }

        [Test]
        public void OnOpen_AsyncHookThrows_IsIsolatedFromCaller()
        {
            var panel = new ProbePanel { ThrowOnOpen = true };

            // 基类用 Forget(handler) 接住异常；测试环境里错误日志会让用例失败，先忽略
            var previous = LogAssert.ignoreFailingMessages;
            LogAssert.ignoreFailingMessages = true;
            try
            {
                Assert.DoesNotThrow(() => panel.OnOpen(null),
                    "OnOpenAsync 的异常不得抛给打开流程的调用方");
            }
            finally
            {
                LogAssert.ignoreFailingMessages = previous;
            }

            Assert.AreEqual(1, panel.OpenCount);
            Assert.IsTrue(panel.IsOpen, "异常不应回滚已置位的 IsOpen");
        }
    }
}
