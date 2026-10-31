using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine.InputSystem;
using Gear.Framework.Runtime.UIInput;

namespace Gear.Tests
{
    /// <summary>
    /// 设备键/玩家分组键回归：两个同型号且无 serial 的手柄（例如两个 Xbox 手柄）
    /// 必须得到不同键，否则玩家槽位覆盖互相踩、UI 会把它们当成同一个玩家。
    /// </summary>
    public class UIInputDeviceKeyTests
    {
        private readonly List<InputDevice> devices = new List<InputDevice>();

        [SetUp]
        public void SetUp()
        {
            // 序号是会话内状态：每个用例从干净状态开始，避免用例顺序影响键值
            UIInputDeviceRegistry.InvalidateAll();
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = devices.Count - 1; i >= 0; i--)
            {
                InputDevice device = devices[i];
                if (device != null && device.added) InputSystem.RemoveDevice(device);
            }
            devices.Clear();
            UIInputDeviceRegistry.InvalidateAll();
        }

        private Gamepad AddGamepad()
        {
            var pad = InputSystem.AddDevice<Gamepad>();
            devices.Add(pad);
            return pad;
        }

        [Test]
        public void TwoGamepads_WithoutSerial_GetDistinctDeviceAndGroupKeys()
        {
            var first = AddGamepad();
            var second = AddGamepad();

            string firstKey = UIInputDeviceRegistry.GetDeviceKey(first);
            string secondKey = UIInputDeviceRegistry.GetDeviceKey(second);

            Assert.AreNotEqual(firstKey, secondKey, "同型号无 serial 的两个手柄必须得到不同的设备键");
            Assert.AreEqual(firstKey + "#dup1", secondKey, "第二个手柄应获得会话内唯一序号后缀");

            Assert.AreNotEqual(
                UIInputDeviceRegistry.GetPlayerGroupKey(first),
                UIInputDeviceRegistry.GetPlayerGroupKey(second),
                "两个独立手柄必须分到不同玩家组，否则会被算成同一个玩家");
        }

        [Test]
        public void SingleGamepad_KeepsLegacyKey()
        {
            var pad = AddGamepad();
            string key = UIInputDeviceRegistry.GetDeviceKey(pad);

            Assert.IsFalse(key.EndsWith("#dup1"), "单手柄必须保持原有键，避免已保存的绑定/玩家槽位失效");
        }
    }
}
