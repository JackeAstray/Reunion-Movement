using System;
using System.Text.RegularExpressions;
using NUnit.Framework;
using ReunionMovement.Core.EventMessage;
using UnityEngine;
using UnityEngine.TestTools;

namespace ReunionMovement.Tests
{
    /// <summary>
    /// R3 事件总线 EditMode 测试。
    /// </summary>
    public class EventMessageSystemTests
    {
        private EventMessageSystem bus;

        [SetUp]
        public void SetUp()
        {
            bus = EventMessageSystem.Instance;
            bus.Init();
        }

        [TearDown]
        public void TearDown()
        {
            bus.Clear();
        }

        [Test]
        public void Dispatch_ToSubscribedListener_ReceivesData()
        {
            int received = -1;
            Action<EventData> listener = e => received = (int)e.data;
            bus.AddEventListener(EventMessageType.ButtonClick, listener);
            bus.DispatchEvent(EventMessageType.ButtonClick, 123);
            Assert.AreEqual(123, received);
        }

        [Test]
        public void RemoveEventListener_StopsDelivery()
        {
            int count = 0;
            Action<EventData> listener = e => count++;
            bus.AddEventListener(EventMessageType.Tip, listener);
            bus.DispatchEvent(EventMessageType.Tip, "x");
            bus.RemoveEventListener(EventMessageType.Tip, listener);
            bus.DispatchEvent(EventMessageType.Tip, "y");
            Assert.AreEqual(1, count);
        }

        [Test]
        public void DuplicateAdd_OnlyDeliversOnce()
        {
            int count = 0;
            Action<EventData> listener = e => count++;
            bus.AddEventListener(EventMessageType.Quit, listener);
            bus.AddEventListener(EventMessageType.Quit, listener);
            bus.DispatchEvent(EventMessageType.Quit, null);
            Assert.AreEqual(1, count);
        }

        [Test]
        public void TypedDispatch_ReceivesValue()
        {
            int received = -1;
            Action<EventData<int>> listener = e => received = e.data;
            bus.AddEventListenerTyped(EventMessageType.GoToNextScene, listener);
            bus.DispatchEventTyped(EventMessageType.GoToNextScene, 42);
            Assert.AreEqual(42, received);
        }

        [Test]
        public void ListenerException_DoesNotThrowToDispatcher()
        {
            Action<EventData> bad = e => throw new InvalidOperationException("boom");
            bus.AddEventListener(EventMessageType.SendMessage, bad);
            // 坏监听器异常按设计被隔离并记录错误日志（不向派发方抛出）：先声明期望
            LogAssert.Expect(LogType.Error,
                new Regex("EventMessageSystem 监听器异常（已隔离，不影响其他订阅者）: SendMessage, boom"));
            Assert.DoesNotThrow(() => bus.DispatchEvent(EventMessageType.SendMessage, "x"));
        }
    }
}
