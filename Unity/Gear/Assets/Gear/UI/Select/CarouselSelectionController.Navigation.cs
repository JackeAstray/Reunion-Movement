using System;
using System.Collections.Generic;
using Gear.Common;
using Gear.Framework.Runtime.UI;
using Gear.Framework.Runtime.UIInput;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Gear.UI.Select
{
    /// <summary>
    /// <see cref="CarouselSelectionController"/> 的 IUINavigationTarget 实现部分：
    /// 导航/确认/取消、玩家槽位判定与焦点处理。按职责从主文件拆出，逻辑未改。
    /// </summary>
    public partial class CarouselSelectionController
    {
        #region IUINavigationTarget

        /// <summary>选择组优先级高于组内选择框；组外选择框使用自身配置值。</summary>
        public int InputPriority => inputPriority;

        public bool AcceptsPlayer(int playerIndex)
        {
            if (!isActiveAndEnabled || !inputEnabled) return false;
            return playerSlot < 0 || playerIndex == playerSlot;
        }

        public bool HandleNavigate(in UINavigateEvent e)
        {
            if (!AcceptsPlayer(e.PlayerIndex)) return false;

            // 没有十字键的方向盘：左右转向 = 上一项/下一项（与布局轴无关）
            if (e.IsWheelSteering)
            {
                return ApplySteering(e.Step.x);
            }

            return ApplyStep(e.Step);
        }

        /// <summary>
        /// 方向盘转向导航：右转 = 下一项，左转 = 上一项。
        /// 矩阵选择框按行优先顺序移动，保证只用转向也能遍历全部选项。
        /// </summary>
        public bool ApplySteering(int direction)
        {
            if (direction == 0) return false;
            Move(direction);
            return true;
        }

        public bool HandleSubmit(in UIButtonEvent e)
        {
            if (!AcceptsPlayer(e.PlayerIndex) || !groupActive) return false;
            Confirm();
            return true;
        }

        public bool HandleCancel(in UIButtonEvent e)
        {
            if (!AcceptsPlayer(e.PlayerIndex) || !groupActive) return false;
            CancelSelection();
            return true;
        }

        /// <summary>执行取消（关闭窗口）。选择组会直接调用此方法。</summary>
        public void CancelSelection()
        {
            try { onCancelled?.Invoke(); }
            catch (Exception ex) { Log.Warning($"[CarouselSelection] onCancelled 订阅者异常: {ex.Message}"); }

            if (closeOnCancel)
            {
                CloseOwningWindow();
            }
        }

        /// <summary>
        /// 关闭所属窗口：优先使用自身 uiName；作为子节点使用时向上找最近的 UIController 窗口。
        /// 找不到窗口时只隐藏自身。
        /// </summary>
        private void CloseOwningWindow()
        {
            var uiSystem = UISystem.Instance;

            // 子菜单：只关闭指定面板；面板若带 UIFocusScope，SetActive(false) 会自动出栈并恢复父面板输入
            if (closeTarget != null)
            {
                var targetController = closeTarget.GetComponent<UIController>();
                if (targetController != null)
                {
                    if (!string.IsNullOrEmpty(targetController.uiName) &&
                        uiSystem != null && uiSystem.IsOpen(targetController.uiName))
                    {
                        uiSystem.CloseWindow(targetController.uiName);
                    }
                    else
                    {
                        targetController.OnClose();
                    }
                }
                else
                {
                    closeTarget.SetActive(false);
                }
                return;
            }

            // 只关闭确实由 UISystem 加载的窗口；场景里自带的 UI 没有注册时直接隐藏自身，
            // 避免 CloseWindow 打印“没有加载的 UIWindow”错误。
            if (!string.IsNullOrEmpty(uiName) && uiSystem != null && uiSystem.IsOpen(uiName))
            {
                uiSystem.CloseWindow(uiName);
                return;
            }

            var controllers = GetComponentsInParent<UIController>(true);
            for (int i = 0; i < controllers.Length; i++)
            {
                var controller = controllers[i];
                if (controller == null) continue;

                if (!string.IsNullOrEmpty(controller.uiName) &&
                    uiSystem != null && uiSystem.IsOpen(controller.uiName))
                {
                    uiSystem.CloseWindow(controller.uiName);
                    return;
                }

                // 场景自带的 UI 没有注册到 UISystem：走窗口控制器自己的关闭逻辑
                controller.OnClose();
                return;
            }

            gameObject.SetActive(false);
        }

        /// <summary>
        /// 直接应用一个四向步进（选择组调用，不检查玩家槽位）。
        /// 返回 true 表示该轴由本选择框消费。
        /// </summary>
        public bool ApplyStep(Vector2Int step)
        {
            if (step == Vector2Int.zero) return false;

            if (columns > 1)
            {
                // 矩阵模式：斜向输入只取主轴，避免一次推入触发两格
                if (Mathf.Abs(step.x) >= Mathf.Abs(step.y))
                {
                    if (step.x == 0) return false;
                    MoveHorizontal(step.x);
                }
                else
                {
                    MoveVertical(step.y);
                }
                return true;
            }

            if (navigateAxis == NavigateAxis.Horizontal)
            {
                if (step.x == 0) return false;
                Move(step.x);
                return true;
            }

            if (step.y != 0)
            {
                Move(-step.y); // 上 = 上一项，下 = 下一项
                return true;
            }

            // 纵向列表允许横向输入作为兜底（例如被识别成 Gamepad 的方向盘只有转向轴）
            if (step.x != 0)
            {
                Move(step.x);
                return true;
            }
            return false;
        }

        /// <summary>选择组控制：关闭/开启独立输入。</summary>
        public void SetInputEnabled(bool enabled)
        {
            inputEnabled = enabled;
        }

        /// <summary>选择组控制：标记当前活动选择框。非活动选择框仍显示自己的选中项（灰色）。</summary>
        public void SetGroupActive(bool active)
        {
            if (groupActive == active) return;
            groupActive = active;
            Refresh();
            if (active)
            {
                FocusCurrent();
            }
        }

        /// <summary>选择组控制：修改玩家槽位。</summary>
        public void SetPlayerSlot(int slot)
        {
            if (playerSlot == slot) return;

            bool reRegister = registered && ownerGroup == null;
            if (reRegister)
            {
                UIInputSystem.Instance.UnregisterNavigationTarget(this);
                registered = false;
            }

            playerSlot = slot;

            if (reRegister && isActiveAndEnabled && UIInputSystem.Instance.isInited)
            {
                UIInputSystem.Instance.RegisterNavigationTarget(this, autoEnableUIControl);
                registered = true;
            }
        }

        /// <summary>选择组控制：绑定/解绑所属选择组。</summary>
        public void AttachGroup(CarouselSelectionGroup group)
        {
            if (ownerGroup == group) return;

            if (group != null)
            {
                // 组内由组统一接收输入，选择框不再直接注册到焦点域
                if (registered)
                {
                    UIInputSystem.Instance.UnregisterNavigationTarget(this);
                    registered = false;
                }
                ownerGroup = group;
                return;
            }

            ownerGroup = null;
            if (isActiveAndEnabled && UIInputSystem.Instance.isInited && !registered)
            {
                UIInputSystem.Instance.RegisterNavigationTarget(this, autoEnableUIControl);
                registered = true;
            }
        }

        #endregion

        /// <summary>矩阵模式横向移动（正=向右，负=向左）：行尾自动换到下一行行首、行首回上一行行尾，末尾环绕；跳过空位/不可交互项（洞）</summary>
        public void MoveHorizontal(int step)
        {
            if (options == null || options.Length == 0) return;
            if (step == 0) return;
            int n = options.Length;
            for (int i = 1; i <= n; i++)
            {
                int next = (currentIndex + step * i + n) % n;
                if (IsNavigable(next))
                {
                    SetIndex(next);
                    return;
                }
            }
        }

        /// <summary>矩阵模式纵向移动（正=向下，负=向上）：按行布局跨行，顶部/底部环绕；目标行更短时列号自动收拢；跳过空位/不可交互项（洞）</summary>
        public void MoveVertical(int step)
        {
            if (options == null || options.Length == 0) return;
            if (step == 0) return;
            var table = BuildRowTable();
            int row = GetRow(table, currentIndex);
            int col = currentIndex - RowOffset(table, row);

            int rows = table.Length;
            for (int i = 1; i <= rows; i++)
            {
                int r = ((row + step * i) % rows + rows) % rows;
                int count = table[r];
                // 空行（rowSizes 配置为 0）无实际元素：跳过，避免列号计算错位
                if (count <= 0) continue;

                int rowStart = RowOffset(table, r);
                int clampedCol = Mathf.Clamp(col, 0, count - 1);
                int best = -1;
                int bestDistance = int.MaxValue;

                // 目标行的同一列可能是洞：在该行内按列距离找最近的可导航项
                for (int c = 0; c < count; c++)
                {
                    int candidate = rowStart + c;
                    if (!IsNavigable(candidate)) continue;

                    int distance = Mathf.Abs(c - clampedCol);
                    if (distance < bestDistance)
                    {
                        bestDistance = distance;
                        best = candidate;
                    }
                }

                if (best < 0) continue;
                if (best == currentIndex) break; // 整列扫描一圈无其他可导航项
                SetIndex(best);
                return;
            }
        }

        /// <summary>
        /// 构建行布局表（每行选项数）。rowSizes 有配置则按它（支持不规则行）；
        /// 否则按 columns 均匀分列，最后一行允许不满。结果缓存，options/rowSizes/columns 变化时重建。
        /// </summary>
        private int[] BuildRowTable()
        {
            int len = options != null ? options.Length : 0;
            if (cachedRowTable != null && cachedOptionsLength == len && cachedRowSizesRef == rowSizes && cachedColumns == columns)
            {
                return cachedRowTable;
            }

            int[] table;
            if (rowSizes != null && rowSizes.Length > 0)
            {
                int total = 0;
                bool valid = true;
                for (int i = 0; i < rowSizes.Length; i++)
                {
                    if (rowSizes[i] < 0)
                    {
                        valid = false;
                        break;
                    }
                    total += rowSizes[i];
                }

                if (valid && total == len)
                {
                    table = rowSizes;
                }
                else
                {
                    Log.Warning($"[CarouselSelection] rowSizes 无效或总和({total})与 options 数量({len})不一致，已回退为按 columns={columns} 均匀分列。");
                    table = BuildUniformRowTable(len);
                }
            }
            else
            {
                table = BuildUniformRowTable(len);
            }

            cachedRowTable = table;
            cachedOptionsLength = len;
            cachedRowSizesRef = rowSizes;
            cachedColumns = columns;
            return table;
        }

        /// <summary>均匀分列的行布局（最后一行可不满）</summary>
        private int[] BuildUniformRowTable(int len)
        {
            if (len <= 0) return Array.Empty<int>();

            int cols = Mathf.Max(1, columns);
            int rows = (len + cols - 1) / cols;
            var table = new int[rows];
            for (int i = 0; i < rows - 1; i++) table[i] = cols;
            table[rows - 1] = len - cols * (rows - 1);
            return table;
        }

        /// <summary>由扁平索引换算行号（行优先）</summary>
        private int GetRow(int[] table, int index)
        {
            int acc = 0;
            for (int i = 0; i < table.Length; i++)
            {
                if (index < acc + table[i]) return i;
                acc += table[i];
            }
            return table.Length - 1;
        }

        /// <summary>行偏移：前 row 行的选项总数</summary>
        private int RowOffset(int[] table, int row)
        {
            int acc = 0;
            for (int i = 0; i < row; i++) acc += table[i];
            return acc;
        }

        /// <summary>按步长移动（正=下一项，负=上一项），循环；跳过空位/不可交互项（洞）</summary>
        public void Move(int step)
        {
            if (options == null || options.Length == 0) return;
            if (step == 0) return;
            int n = options.Length;
            for (int i = 1; i <= n; i++)
            {
                int next = (currentIndex + step * i + n) % n;
                if (IsNavigable(next))
                {
                    SetIndex(next);
                    return;
                }
            }
        }

        /// <summary>索引是否可导航（选项非空且可交互；null 或不可交互 = 洞，导航跳过）</summary>
        private bool IsNavigable(int index)
        {
            if (options == null || index < 0 || index >= options.Length) return false;
            var opt = options[index];
            return opt != null && opt.interactable;
        }

        public void SetIndex(int index)
        {
            if (options == null || options.Length == 0) return;

            int previous = currentIndex;
            currentIndex = Mathf.Clamp(index, 0, options.Length - 1);
            NormalizeCurrentIndex();

            FocusCurrent();
            Refresh();

            if (previous != currentIndex)
            {
                try { onSelectionChanged?.Invoke(currentIndex); }
                catch (Exception ex) { Log.Warning($"[CarouselSelection] onSelectionChanged 订阅者异常: {ex.Message}"); }
            }
        }

        /// <summary>
        /// 设置当前选项为 EventSystem 焦点。
        /// 注意：不走 UIInputSystem.SetSelectedGameObject —— 该方法会压入焦点栈（用于弹窗
        /// 层级跳转）；轮播式窗口内部切换焦点不应压栈，否则焦点栈会无限增长。
        /// 多个选择框同时操作时请关闭 setEventSystemFocus，避免互相抢焦点。
        /// </summary>
        private void FocusCurrent()
        {
            if (!setEventSystemFocus || !groupActive) return;
            if (options == null || options.Length == 0) return;

            var selectable = options[currentIndex];
            int targetIndex = currentIndex;
            if (selectable != null && !selectable.interactable)
            {
                // 当前项不可交互时，就近回退到最近的可交互项，避免焦点丢失
                targetIndex = FindNearestInteractableIndex(currentIndex);
                if (targetIndex < 0) return;
                selectable = options[targetIndex];
            }
            if (selectable == null) return;

            // 回退后同步 currentIndex：否则 Refresh 高亮的仍是不可交互项，焦点与高亮错位
            if (currentIndex != targetIndex)
            {
                currentIndex = targetIndex;
                Refresh();
            }

            if (EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(selectable.gameObject);
            }
        }

        /// <summary>从 index 向两侧寻找最近的可交互项索引（无则返回 -1）</summary>
        private int FindNearestInteractableIndex(int index)
        {
            for (int dist = 1; dist < options.Length; dist++)
            {
                int nextIdx = (index + dist) % options.Length;
                if (options[nextIdx] != null && options[nextIdx].interactable) return nextIdx;
                int prevIdx = (index - dist + options.Length) % options.Length;
                if (options[prevIdx] != null && options[prevIdx].interactable) return prevIdx;
            }
            return -1;
        }

        /// <summary>
        /// 通过 Button 的 ColorBlock 设置高亮。当前项同时设置 selectedColor，
        /// 避免 EventSystem 选中时用默认 selectedColor 覆盖 normalColor。
        /// </summary>
        private static void SetButtonColor(Button button, Color color, bool alsoSelected)
        {
            var colors = button.colors;
            bool changed = false;

            if (colors.normalColor != color)
            {
                colors.normalColor = color;
                changed = true;
            }
            if (alsoSelected && colors.selectedColor != color)
            {
                colors.selectedColor = color;
                changed = true;
            }

            if (changed)
            {
                button.colors = colors;
            }
        }

        private void Refresh()
        {
            if (options == null || options.Length == 0)
            {
                if (previews != null)
                {
                    for (int i = 0; i < previews.Length; i++)
                    {
                        if (previews[i] != null) previews[i].SetActive(false);
                    }
                }
                lastPreviewIndex = -1;
                lastHighlightedIndex = -1;
                return;
            }

            // 首次 Refresh 或选项长度变化时重置全部普通色；之后只更新上一项/当前项，避免每步 O(n)
            if (lastHighlightedIndex < 0 || lastHighlightedIndex >= options.Length)
            {
                for (int i = 0; i < options.Length; i++)
                {
                    if (i == currentIndex) continue;
                    var btn = options[i];
                    if (btn != null) SetButtonColor(btn, normalColor, alsoSelected: false);
                }
            }
            else if (lastHighlightedIndex != currentIndex)
            {
                var previous = options[lastHighlightedIndex];
                if (previous != null) SetButtonColor(previous, normalColor, alsoSelected: false);
            }

            if (currentIndex >= 0 && currentIndex < options.Length && options[currentIndex] != null)
            {
                SetButtonColor(
                    options[currentIndex],
                    groupActive ? selectedColor : inactiveSelectedColor,
                    alsoSelected: true);
            }
            lastHighlightedIndex = currentIndex;

            // 只切换变化的预览对象，避免大量 SetActive 触发 Canvas 重建
            if (previews == null || previews.Length == 0) return;

            if (lastPreviewIndex < 0)
            {
                // 首次刷新：先隐藏全部预览，确保只有当前项可见
                for (int i = 0; i < previews.Length; i++)
                {
                    if (previews[i] != null)
                    {
                        previews[i].SetActive(false);
                    }
                }
            }
            else if (lastPreviewIndex < previews.Length && previews[lastPreviewIndex] != null)
            {
                previews[lastPreviewIndex].SetActive(false);
            }

            lastPreviewIndex = currentIndex;
            if (lastPreviewIndex < previews.Length && previews[lastPreviewIndex] != null)
            {
                previews[lastPreviewIndex].SetActive(true);
            }
        }

    }
}
