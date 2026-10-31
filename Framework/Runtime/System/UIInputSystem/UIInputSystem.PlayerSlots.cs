using System;
using System.Collections.Generic;
using Gear.Common;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Gear.Framework.Runtime.UIInput
{
    /// <summary>
    /// <see cref="UIInputSystem"/> 的玩家槽位分配与持久化部分。
    ///
    /// <para>
    /// 持久化只认"跨会话稳定的设备键"：<c>product#serial</c> 之类。
    /// 同型号且无 serial 的手柄在运行时靠会话序号 <c>#dupN</c> 区分，
    /// 这个序号重启后可能指到另一台设备，因此只在本次会话内生效、不落盘；
    /// 旧版本落盘的 <c>#dupN</c> 键会在读取时丢弃。
    /// </para>
    /// </summary>
    public partial class UIInputSystem
    {
        /// <summary>读取设备被显式分配的玩家槽位（-1 表示按连接顺序自动分配）。</summary>
        public int GetAssignedPlayerSlot(InputDevice device)
        {
            if (device == null) return -1;
            return TryGetSlotOverride(device, out int slot) ? slot : -1;
        }

        /// <summary>
        /// 读取槽位覆盖：优先持久化键（可跨会话），其次本次会话键（同型号无 serial 手柄）。
        /// </summary>
        private bool TryGetSlotOverride(InputDevice device, out int slot)
        {
            slot = -1;
            if (device == null) return false;
            EnsurePlayerSlotsLoaded();

            string persistentKey = UIInputDeviceRegistry.GetPersistentDeviceKey(device);
            if (!string.IsNullOrEmpty(persistentKey)
                && playerSlotOverrides.TryGetValue(persistentKey, out slot))
            {
                return true;
            }

            string runtimeKey = UIInputDeviceRegistry.GetDeviceKey(device);
            return sessionPlayerSlotOverrides.TryGetValue(runtimeKey, out slot);
        }

        /// <summary>
        /// 把设备固定到指定玩家槽位。
        /// 能稳定识别（有 serial）的设备会持久化；同型号无 serial 的手柄只在本次会话内生效。
        /// 多方向盘场景建议在设置界面中显式分配，避免设备枚举顺序变化导致选错选择框。
        /// slot &lt; 0 表示恢复自动分配。
        /// </summary>
        public void AssignPlayerSlot(InputDevice device, int slot)
        {
            if (device == null) return;
            EnsurePlayerSlotsLoaded();

            string runtimeKey = UIInputDeviceRegistry.GetDeviceKey(device);
            string persistentKey = UIInputDeviceRegistry.GetPersistentDeviceKey(device);

            if (slot < 0)
            {
                if (!string.IsNullOrEmpty(persistentKey)) playerSlotOverrides.Remove(persistentKey);
                sessionPlayerSlotOverrides.Remove(runtimeKey);
            }
            else if (!string.IsNullOrEmpty(persistentKey))
            {
                // 能稳定识别的设备：落盘，重启后继续生效
                playerSlotOverrides[persistentKey] = slot;
                sessionPlayerSlotOverrides.Remove(runtimeKey);
            }
            else
            {
                // 同型号且无 serial 的手柄：设备键含会话序号，落盘会在下次启动指到另一台设备。
                // 只在本次会话内固定，并明确告知调用方。
                sessionPlayerSlotOverrides[runtimeKey] = slot;
                Log.Warning("AssignPlayerSlot：设备 {0} 同型号且无 serial，槽位 {1} 只在本次会话内有效；" +
                            "需要跨会话固定请给设备设置不同 serial，或改用按玩家分组的显式配置",
                    device.displayName, slot);
            }
            SavePlayerSlots();
        }

        /// <summary>清除所有设备的玩家槽位固定分配。</summary>
        public void ClearPlayerSlotAssignments()
        {
            EnsurePlayerSlotsLoaded();
            if (playerSlotOverrides.Count == 0 && sessionPlayerSlotOverrides.Count == 0) return;
            playerSlotOverrides.Clear();
            sessionPlayerSlotOverrides.Clear();
            SavePlayerSlots();
        }

        private void EnsurePlayerSlotsLoaded()
        {
            if (playerSlotsLoaded) return;
            playerSlotsLoaded = true;
            playerSlotOverrides = new Dictionary<string, int>(8);

            string json = PlayerPrefs.GetString(PlayerSlotPrefsKey, null);
            if (string.IsNullOrEmpty(json)) return;

            try
            {
                var list = JsonUtility.FromJson<PlayerSlotList>(json);
                if (list?.items == null) return;
                for (int i = 0; i < list.items.Count; i++)
                {
                    var entry = list.items[i];
                    if (entry != null && !string.IsNullOrEmpty(entry.deviceKey))
                    {
                        // 旧版本把含会话序号的键（product#dup1）落盘了：这个键下次启动可能指到
                        // 另一台同型号手柄，必须丢弃，否则槽位会错位。
                        if (entry.deviceKey.LastIndexOf("#dup", StringComparison.Ordinal) >= 0)
                        {
                            Log.Warning("UIInputSystem: 丢弃旧版不稳定的玩家槽位键 {0}（同型号无 serial 手柄无法跨会话识别）",
                                entry.deviceKey);
                            continue;
                        }
                        playerSlotOverrides[entry.deviceKey] = entry.slot;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning("UIInputSystem: 玩家槽位配置读取失败: {0}", ex.Message);
            }
        }

        private void SavePlayerSlots()
        {
            EnsurePlayerSlotsLoaded();
            var list = new PlayerSlotList();
            foreach (var kv in playerSlotOverrides)
            {
                list.items.Add(new PlayerSlotEntry { deviceKey = kv.Key, slot = kv.Value });
            }
            PlayerPrefs.SetString(PlayerSlotPrefsKey, JsonUtility.ToJson(list));
            PlayerPrefs.Save();
        }
    }
}
