using ReunionMovement.Common;
using ReunionMovement.Core.Base;
using ReunionMovement.Core.Resources;
using ReunionMovement.Core.UI;
using Cysharp.Threading.Tasks;
using R3;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;

namespace ReunionMovement.Core.UIInput
{
    public partial class UIInputSystem
    {
        #region 输入 Action 加载与绑定

        /// <summary>
        /// 加载 InputActionAsset（从 Settings 文件夹）
        /// </summary>
        private async UniTask LoadInputActionsAsync()
        {
            // 尝试从 UISystem 的 InputSystemUIInputModule 获取已赋值的 actions。
            // 与 Resources 路径一致：Instantiate 克隆后使用，避免 ApplyBindingOverride/AddBinding
            // 直接修改共享资产（域重载关闭时跨 Play 会话残留、其他系统共用该资产被污染）。
            if (inputModule != null && inputModule.actionsAsset != null)
            {
                inputActions = UnityEngine.Object.Instantiate(inputModule.actionsAsset);
            }
            else
            {
                // 否则通过 ResourcesSystem 加载（需放在 Resources 文件夹下）
                var loaded = ResourcesSystem.Instance.Load<InputActionAsset>("InputSystem_Actions");
                if (loaded != null)
                {
                    try
                    {
                        inputActions = UnityEngine.Object.Instantiate(loaded);
                    }
                    finally
                    {
                        ResourcesSystem.Instance.DeleteAssetCache("InputSystem_Actions");
                    }
                    if (inputModule != null)
                    {
                        inputModule.actionsAsset = inputActions;
                    }
                }
                else
                {
                    Log.Warning("UIInputSystem: 无法加载 InputSystem_Actions.inputactions，键盘导航将依赖默认绑定");
                }
            }

            // 恢复官方格式绑定快照（InputActionAsset.ToJson/LoadFromJson，完整保留手柄/composite override）
            RestoreBindingsFromAssetJson();

            await UniTask.CompletedTask;
        }

        /// <summary>
        /// 从官方 JSON 快照恢复 InputActionAsset 绑定覆盖（Input System 标准持久化方案）。
        /// 快照不存在时静默跳过；恢复后 Init 的 ApplyBindingsToActions 仍会用 CurrentBinding 覆盖
        /// （两者来源一致，不会打架）。
        /// </summary>
        private void RestoreBindingsFromAssetJson()
        {
            if (inputActions == null) return;
            string json = PlayerPrefs.GetString("ui_bind_asset_json", null);
            if (string.IsNullOrEmpty(json)) return;
            try
            {
                inputActions.LoadFromJson(json);
                Log.Debug("UIInputSystem: 已从官方 JSON 快照恢复绑定覆盖");
            }
            catch (Exception ex)
            {
                Log.Warning("UIInputSystem: 绑定 JSON 快照恢复失败（将使用 PlayerPrefs 简化字段）: {0}", ex.Message);
            }
        }

        /// <summary>
        /// 将 CurrentBinding 中的自定义按键应用到 InputAction
        /// 注意：Navigate 的默认 .inputactions 已包含 WASD + 方向键，此处不做覆盖仅确保存在
        /// </summary>
        public void ApplyBindingsToActions()
        {
            if (inputActions == null) return;

            try
            {
                // Navigate: 默认 2D Vector composite 已含 WASD + 方向键 + 手柄，
                // 仅在用户自定义键与默认不同时才追加（不覆盖原有绑定）
                EnsureNavigateBindings();
                // Submit: 确保 Enter + NumpadEnter 都可用
                EnsureSubmitBindings();
                // 应用自定义 Submit/Cancel 覆盖（此前遗漏 Submit → SetBinding/交互重绑定不生效、重启后丢失）
                ApplyBindingOverride("UI", "Submit", CurrentBinding.submit);
                ApplyBindingOverride("UI", "Cancel", CurrentBinding.cancel);
            }
            catch (Exception ex)
            {
                Log.Error("UIInputSystem: 应用按键绑定失败: {0}", ex.Message);
            }
        }

        /// <summary>
        /// 确保 Navigate action 同时支持 WASD 和方向键
        /// 默认 .inputactions 的 composite 已包含全部，仅在自定义键时覆盖对应方向 part
        /// </summary>
        private void EnsureNavigateBindings()
        {
            var action = inputActions.FindAction("UI/Navigate");
            if (action == null) return;

            // 修复：Vector2 复合动作不能通过追加独立绑定来添加方向（单键无法解析出方向向量，
            // 原 EnsureKeyInAction 的追加实际无效）。自定义方向键必须覆盖 2DVector composite
            // 中对应命名的键盘 part（asset 中名为 up/down/left/right，多个同名 part 共存）。
            OverrideNavigatePart(action, "up", CurrentBinding.navigateUp);
            OverrideNavigatePart(action, "down", CurrentBinding.navigateDown);
            OverrideNavigatePart(action, "left", CurrentBinding.navigateLeft);
            OverrideNavigatePart(action, "right", CurrentBinding.navigateRight);
        }

        /// <summary>
        /// 覆盖 2DVector composite 中指定方向 part 的按键路径（覆盖第一个匹配的键盘 part）。
        /// 同名 part 有多个（如 up 有 w 与 upArrow）时只覆盖第一个，方向键仍保留。幂等：已是目标键则跳过。
        /// </summary>
        private void OverrideNavigatePart(InputAction action, string partName, string keyName)
        {
            if (string.IsNullOrEmpty(keyName)) return;
            var targetPath = $"<Keyboard>/{keyName}";

            for (int i = 0; i < action.bindings.Count; i++)
            {
                var binding = action.bindings[i];
                if (!binding.isComposite) continue;

                for (int j = i + 1; j < action.bindings.Count; j++)
                {
                    var part = action.bindings[j];
                    if (!part.isPartOfComposite) break;
                    if (!part.path.Contains("Keyboard")) continue;
                    if (!string.Equals(part.name, partName, StringComparison.OrdinalIgnoreCase)) continue;

                    if (part.path == targetPath) return; // 已是目标键，无需覆盖
                    action.ApplyBindingOverride(j, targetPath);
                    return;
                }
            }
        }

        /// <summary>
        /// 确保 Submit action 同时支持 Enter 和 NumpadEnter
        /// </summary>
        private void EnsureSubmitBindings()
        {
            var action = inputActions.FindAction("UI/Submit");
            if (action == null) return;

            // 确保 numpadEnter 绑定存在（不在 {Submit} 通配符范围内）
            bool hasNumpadEnter = false;
            for (int i = 0; i < action.bindings.Count; i++)
            {
                if (action.bindings[i].path == "<Keyboard>/numpadEnter")
                {
                    hasNumpadEnter = true;
                    break;
                }
            }
            if (!hasNumpadEnter)
            {
                action.AddBinding("<Keyboard>/numpadEnter");
            }
        }

        /// <summary>
        /// 对指定 Action 覆盖提交/取消按键
        /// </summary>
        private void ApplyBindingOverride(string actionMap, string actionName, string bindingKey)
        {
            if (string.IsNullOrEmpty(bindingKey)) return;

            var action = inputActions.FindAction($"{actionMap}/{actionName}");
            if (action == null) return;

            // 完整路径（如 "<Gamepad>/buttonSouth"，交互式重绑定手柄产生）按路径处理：
            // 覆盖同设备的已有绑定，否则追加
            if (bindingKey.StartsWith("<"))
            {
                int close = bindingKey.IndexOf('>');
                string device = close > 0 ? bindingKey.Substring(0, close + 1) : bindingKey;
                for (int i = 0; i < action.bindings.Count; i++)
                {
                    var binding = action.bindings[i];
                    if (!binding.isComposite && !binding.isPartOfComposite
                        && binding.path != null && binding.path.StartsWith(device, StringComparison.Ordinal))
                    {
                        action.ApplyBindingOverride(i, bindingKey);
                        return;
                    }
                }
                action.AddBinding(bindingKey);
                return;
            }

            // 键盘按键名（如 "enter"）：覆盖第一个键盘绑定
            for (int i = 0; i < action.bindings.Count; i++)
            {
                var binding = action.bindings[i];
                if (!binding.isComposite && !binding.isPartOfComposite
                    && binding.path != null && binding.path.Contains("Keyboard"))
                {
                    action.ApplyBindingOverride(i, $"<Keyboard>/{bindingKey}");
                    return;
                }
            }

            action.AddBinding($"<Keyboard>/{bindingKey}");
        }

        #endregion
    }
}
