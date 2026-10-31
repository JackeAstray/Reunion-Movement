using System;
using System.Collections.Generic;
using UnityEngine.InputSystem;

namespace Gear.Framework.Runtime.UIInput
{
    /// <summary>
    /// <see cref="UIInputDeviceRegistry"/> 的设备键/玩家分组键部分（按职责从主文件拆出）：
    /// 键的拼接、无 serial 手柄的去重序号、以及方向盘/HOTAS 的分组语义。
    /// </summary>
    public static partial class UIInputDeviceRegistry
    {
        /// <summary>
        /// 同一会话内为“同型号且无 serial 的手柄”分配的去重序号（deviceId → 序号）。
        /// 两个 Xbox 手柄的 product 都是 "Xbox Controller"、serial 都为空，若不加区分，
        /// 它们的设备键/玩家分组键完全相同 → 槽位覆盖互相踩、两个手柄被当成同一个玩家。
        /// </summary>
        private static readonly Dictionary<int, int> s_gamepadOrdinals = new Dictionary<int, int>(4);

        /// <summary>每个设备基准键已分配到的下一个序号（仅会话内有效，InvalidateAll 重置）。</summary>
        private static readonly Dictionary<string, int> s_nextGamepadOrdinal = new Dictionary<string, int>(4);

        /// <summary>
        /// 设备键：优先 product[#serial]，缺失时退回 displayName/name/layout。
        /// 结果缓存在 DeviceCache，避免每帧拼接字符串。
        /// </summary>
        public static string GetDeviceKey(InputDevice device)
        {
            if (device == null) return "keyboard_mouse";
            return GetCache(device).DeviceKey;
        }

        /// <summary>
        /// 跨会话稳定的设备键；无法稳定识别时返回 null。
        /// <para>
        /// <see cref="GetDeviceKey"/> 对"同型号且无 serial 的手柄"会追加会话内序号 <c>#dupN</c>，
        /// 这个序号只保证本次运行内唯一，重启后设备枚举顺序一变就会指到另一台手柄。
        /// 因此持久化玩家槽位时必须用它过滤：带 <c>#dup</c> 的键不落盘，只在本次会话内生效。
        /// </para>
        /// </summary>
        public static string GetPersistentDeviceKey(InputDevice device)
        {
            if (device == null) return "keyboard_mouse";

            string key = GetDeviceKey(device);
            if (string.IsNullOrEmpty(key)) return null;

            // product 本身可能包含 '#'，只把末尾的 "#dup<数字>" 视为会话序号
            int dupIndex = key.LastIndexOf("#dup", StringComparison.Ordinal);
            if (dupIndex < 0) return key;

            for (int i = dupIndex + 4; i < key.Length; i++)
            {
                if (!char.IsDigit(key[i])) return key; // 不是序号后缀，按稳定键处理
            }
            return null;
        }

        /// <summary>
        /// 玩家分组键：同一个物理设备的多个 HID 子设备（底座、踏板、轮缘等）
        /// 应占用同一个玩家槽位，不能因为枚举出多个 InputDevice 就多出 P2/P3。
        /// 有 serial 时按 manufacturer#serial 分组；没有 serial 时退回 product 键。
        /// </summary>
        public static string GetPlayerGroupKey(InputDevice device)
        {
            if (device == null) return "keyboard_mouse";

            var description = device.description;
            string serial = description.serial;
            if (!string.IsNullOrEmpty(serial))
            {
                string manufacturer = description.manufacturer ?? string.Empty;
                return manufacturer + "#" + serial;
            }

            return GetDeviceKey(device);
        }

        private static string ComputeDeviceKey(InputDevice device)
        {
            string product = device.description.product;
            string serial = device.description.serial;

            string key;
            if (!string.IsNullOrEmpty(product))
            {
                key = string.IsNullOrEmpty(serial) ? product : product + "#" + serial;
            }
            else if (!string.IsNullOrEmpty(device.displayName)) key = device.displayName;
            else if (!string.IsNullOrEmpty(device.name)) key = device.name;
            else key = device.layout;

            return ApplyDuplicateGamepadOrdinal(device, key, serial);
        }

        /// <summary>
        /// 仅对“同型号且无 serial 的手柄（Gamepad）”追加会话内唯一的序号后缀。
        /// <para>
        /// 为什么只限 Gamepad：方向盘/飞行摇杆常把一个物理设备枚举成底座+踏板+轮缘多个
        /// InputDevice，它们的 product 相同且通常没有 serial，必须继续共用同一个分组键
        /// （否则一个方向盘会被算成多个玩家）；而两个同型号手柄是两台独立设备，
        /// 必须分开。Gamepad 类型能可靠区分这两种情况，且单手柄时序号为 0、键不变，
        /// 已保存的绑定/槽位天然兼容。
        /// </para>
        /// </summary>
        private static string ApplyDuplicateGamepadOrdinal(InputDevice device, string key, string serial)
        {
            if (!(device is Gamepad) || !string.IsNullOrEmpty(serial)) return key;

            if (!s_gamepadOrdinals.TryGetValue(device.deviceId, out int ordinal))
            {
                ordinal = s_nextGamepadOrdinal.TryGetValue(key, out int next) ? next : 0;
                s_nextGamepadOrdinal[key] = ordinal + 1;
                s_gamepadOrdinals[device.deviceId] = ordinal;
            }
            return ordinal == 0 ? key : key + "#dup" + ordinal;
        }
    }
}
