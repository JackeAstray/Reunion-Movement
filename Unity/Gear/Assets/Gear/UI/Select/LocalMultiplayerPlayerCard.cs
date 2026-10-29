using System;
using System.Collections.Generic;
using Gear.Framework.Runtime.UIInput;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Gear.UI.Select
{
    /// <summary>
    /// 本地多人选择界面的单个玩家卡片。
    /// 每个玩家独立推进：颜色 → 位置 → 准备，不需要等待 P1 推进阶段。
    /// 键盘/鼠标固定为 P1；其他实体控制器按 UIInputSystem 的 PlayerSlot 对应 P2-P4。
    /// </summary>
    public class LocalMultiplayerPlayerCard : MonoBehaviour, IUINavigationTarget
    {
        private enum CardRow
        {
            Color,
            Position,
            Ready,
        }

        [SerializeField] private int playerSlot;
        [SerializeField] private Text playerLabel;
        [SerializeField] private Text statusLabel;
        [SerializeField] private Text hintLabel;
        [SerializeField] private Button[] positionButtons;
        [SerializeField] private Button[] colorButtons;
        [SerializeField] private Button readyButton;
        [SerializeField] private Image readyBadge;
        [SerializeField] private Image background;

        [Header("颜色")]
        [SerializeField] private Color normalColor = new Color(0.18f, 0.18f, 0.18f, 1f);
        [SerializeField] private Color selectedColor = new Color(0.95f, 0.72f, 0.15f, 1f);
        [SerializeField] private Color disabledColor = new Color(0.08f, 0.08f, 0.08f, 1f);
        [SerializeField] private Color readyColor = new Color(0.2f, 0.75f, 0.3f, 1f);

        private LocalMultiplayerSelectionManager manager;
        private int positionCursor;
        private int colorCursor;
        private bool colorHasNoneOption;
        private bool legacyPositionButtonsWithSkip;
        private CardRow activeRow = CardRow.Color;
        private bool registered;
        private bool bindRequested;
        private int lastInitVersion = -1;

        private readonly List<UnityAction> buttonActions = new List<UnityAction>(16);

        public int PlayerSlot => playerSlot;
        public int InputPriority => 100;

        internal void Initialize(LocalMultiplayerSelectionManager owner, int slot)
        {
            manager = owner;
            playerSlot = slot;
            colorHasNoneOption = manager != null && colorButtons != null &&
                                 colorButtons.Length == manager.ColorCount + 1;
            legacyPositionButtonsWithSkip = manager != null && positionButtons != null &&
                                            positionButtons.Length == manager.PositionCount + 1;

            if (legacyPositionButtonsWithSkip)
            {
                // 兼容旧场景：旧版位置行第一个是「不参加」，运行时隐藏并把剩余按钮排成 4 个位置。
                if (positionButtons[0] != null) positionButtons[0].gameObject.SetActive(false);
                for (int i = 1; i < positionButtons.Length; i++)
                {
                    var button = positionButtons[i];
                    if (button == null) continue;
                    var rect = button.GetComponent<RectTransform>();
                    if (rect != null)
                    {
                        rect.anchoredPosition = new Vector2(-126f + (i - 1) * 84f, rect.anchoredPosition.y);
                    }
                }
            }

            if (playerLabel != null)
            {
                playerLabel.text = $"P{slot + 1}";
            }

            SetupButtonCallbacks();
            SyncActiveRowToState();
            RefreshVisuals();
        }

        private void OnEnable()
        {
            if (!UIInputSystem.Instance.isInited)
            {
                bindRequested = true;
                return;
            }
            Bind();
        }

        private void OnDisable()
        {
            Unbind();
        }

        private void OnDestroy()
        {
            Unbind();
        }

        private void Update()
        {
            var inputSystem = UIInputSystem.Instance;
            if (!inputSystem.isInited || manager == null) return;

            int version = inputSystem.InitVersion;
            if (bindRequested || lastInitVersion != version)
            {
                lastInitVersion = version;
                Bind();
            }
        }

        private void Bind()
        {
            Unbind();
            if (!UIInputSystem.Instance.isInited)
            {
                bindRequested = true;
                return;
            }

            bindRequested = false;
            UIInputSystem.Instance.RegisterNavigationTarget(this, true);
            registered = true;
        }

        private void Unbind()
        {
            bindRequested = false;
            if (!registered) return;

            UIInputSystem.Instance.UnregisterNavigationTarget(this);
            registered = false;
        }

        private void SetupButtonCallbacks()
        {
            ClearButtonCallbacks();

            if (positionButtons != null)
            {
                for (int i = 0; i < positionButtons.Length; i++)
                {
                    if (positionButtons[i] == null) continue;
                    if (legacyPositionButtonsWithSkip && i == 0) continue;

                    int option = legacyPositionButtonsWithSkip ? i - 1 : i;
                    UnityAction action = () => OnPositionClicked(option);
                    positionButtons[i].onClick.AddListener(action);
                    buttonActions.Add(action);
                }
            }

            if (colorButtons != null)
            {
                for (int i = 0; i < colorButtons.Length; i++)
                {
                    if (colorButtons[i] == null) continue;

                    int option = ColorButtonIndexToValue(i);
                    UnityAction action = () => OnColorClicked(option);
                    colorButtons[i].onClick.AddListener(action);
                    buttonActions.Add(action);
                }
            }

            if (readyButton != null)
            {
                UnityAction action = OnReadyClicked;
                readyButton.onClick.AddListener(action);
                buttonActions.Add(action);
            }
        }

        private void ClearButtonCallbacks()
        {
            for (int i = 0; i < buttonActions.Count; i++)
            {
                if (positionButtons != null)
                {
                    for (int b = 0; b < positionButtons.Length; b++)
                    {
                        if (positionButtons[b] != null) positionButtons[b].onClick.RemoveListener(buttonActions[i]);
                    }
                }
                if (colorButtons != null)
                {
                    for (int b = 0; b < colorButtons.Length; b++)
                    {
                        if (colorButtons[b] != null) colorButtons[b].onClick.RemoveListener(buttonActions[i]);
                    }
                }
                if (readyButton != null) readyButton.onClick.RemoveListener(buttonActions[i]);
            }
            buttonActions.Clear();
        }

        private void OnColorClicked(int colorIndex)
        {
            if (manager == null) return;

            if (manager.SelectColor(playerSlot, colorIndex))
            {
                colorCursor = colorIndex < 0 ? 0 : ColorValueToButtonIndex(colorIndex);
                activeRow = colorIndex < 0 ? CardRow.Color : CardRow.Position;
                RefreshVisuals();
            }
        }

        private void OnPositionClicked(int positionIndex)
        {
            if (manager == null) return;

            if (manager.SelectPosition(playerSlot, positionIndex))
            {
                positionCursor = positionIndex;
                activeRow = CardRow.Ready;
                RefreshVisuals();
            }
        }

        private void OnReadyClicked()
        {
            if (manager == null) return;

            if (manager.SetReady(playerSlot))
            {
                activeRow = CardRow.Ready;
                RefreshVisuals();
            }
        }

        public bool AcceptsPlayer(int playerIndex)
        {
            if (manager == null || !isActiveAndEnabled) return false;
            if (playerSlot < 0 || playerIndex != playerSlot) return false;
            return manager.CanPlayerAct(playerSlot);
        }

        public bool HandleNavigate(in UINavigateEvent e)
        {
            if (!AcceptsPlayer(e.PlayerIndex) || manager == null) return false;

            Vector2Int step = e.Step;
            if (step == Vector2Int.zero) return false;

            int direction = step.x != 0 ? step.x : step.y;
            if (direction == 0) return false;

            switch (activeRow)
            {
                case CardRow.Color:
                    return MoveColor(direction);
                case CardRow.Position:
                    return MovePosition(direction);
                default:
                    return false;
            }
        }

        public bool HandleSubmit(in UIButtonEvent e)
        {
            if (!AcceptsPlayer(e.PlayerIndex) || manager == null) return false;

            switch (activeRow)
            {
                case CardRow.Color:
                {
                    int value = ColorButtonIndexToValue(colorCursor);
                    if (!manager.SelectColor(playerSlot, value)) return false;

                    colorCursor = value < 0 ? 0 : ColorValueToButtonIndex(value);
                    activeRow = value < 0 ? CardRow.Color : CardRow.Position;
                    RefreshVisuals();
                    return true;
                }
                case CardRow.Position:
                {
                    int value = positionCursor;
                    if (!manager.SelectPosition(playerSlot, value)) return false;

                    activeRow = CardRow.Ready;
                    RefreshVisuals();
                    return true;
                }
                case CardRow.Ready:
                    if (!manager.SetReady(playerSlot)) return false;
                    RefreshVisuals();
                    return true;
                default:
                    return false;
            }
        }

        public bool HandleCancel(in UIButtonEvent e)
        {
            if (!AcceptsPlayer(e.PlayerIndex) || manager == null) return false;

            if (!manager.CancelPlayer(playerSlot)) return false;

            activeRow = CardRow.Color;
            positionCursor = 0;
            colorCursor = 0;
            RefreshVisuals();
            return true;
        }

        private bool MoveColor(int direction)
        {
            int count = ColorCursorCount();
            if (count <= 0) return false;

            for (int offset = 1; offset <= count; offset++)
            {
                int next = Wrap(colorCursor + direction * offset, count);
                int value = ColorButtonIndexToValue(next);
                if (!manager.CanSelectColor(playerSlot, value)) continue;

                colorCursor = next;
                RefreshVisuals();
                return true;
            }

            return false;
        }

        private bool MovePosition(int direction)
        {
            int count = manager.PositionCount;
            if (count <= 0) return false;

            for (int offset = 1; offset <= count; offset++)
            {
                int next = Wrap(positionCursor + direction * offset, count);
                if (!manager.CanSelectPosition(playerSlot, next)) continue;

                positionCursor = next;
                RefreshVisuals();
                return true;
            }

            return false;
        }

        private static int Wrap(int value, int count)
        {
            return (value % count + count) % count;
        }

        private int ColorButtonIndexToValue(int buttonIndex)
        {
            return colorHasNoneOption ? buttonIndex - 1 : buttonIndex;
        }

        private int ColorValueToButtonIndex(int value)
        {
            return colorHasNoneOption ? value + 1 : value;
        }

        private int ColorCursorCount()
        {
            return manager == null ? 0 : manager.ColorCount + (colorHasNoneOption ? 1 : 0);
        }

        internal void OnStageChanged()
        {
            if (manager != null && manager.Stage != LocalSelectionStage.Selecting)
            {
                activeRow = CardRow.Ready;
            }
            else
            {
                SyncActiveRowToState();
            }

            NormalizeCursors();
            RefreshVisuals();
        }

        private void SyncActiveRowToState()
        {
            var state = manager != null ? manager.GetState(playerSlot) : null;
            if (state == null)
            {
                activeRow = CardRow.Color;
                return;
            }

            if (state.ready || state.positionIndex >= 0)
            {
                activeRow = CardRow.Ready;
            }
            else if (state.participating && state.colorIndex >= 0)
            {
                activeRow = CardRow.Position;
            }
            else
            {
                activeRow = CardRow.Color;
            }
        }

        private void NormalizeCursors()
        {
            if (manager == null) return;

            var state = manager.GetState(playerSlot);
            if (state == null) return;

            if (activeRow == CardRow.Color)
            {
                int value = ColorButtonIndexToValue(colorCursor);
                if (!manager.CanSelectColor(playerSlot, value))
                {
                    colorCursor = FindAvailableColorCursor();
                }
            }
            else
            {
                colorCursor = state.colorIndex >= 0 ? ColorValueToButtonIndex(state.colorIndex) : 0;
            }

            if (activeRow == CardRow.Position)
            {
                if (!manager.CanSelectPosition(playerSlot, positionCursor))
                {
                    positionCursor = FindAvailablePositionCursor();
                }
            }
            else
            {
                positionCursor = state.positionIndex >= 0 ? state.positionIndex : 0;
            }
        }

        private int FindAvailableColorCursor()
        {
            int count = ColorCursorCount();
            for (int i = 0; i < count; i++)
            {
                int value = ColorButtonIndexToValue(i);
                if (manager.CanSelectColor(playerSlot, value)) return i;
            }
            return 0;
        }

        private int FindAvailablePositionCursor()
        {
            int count = manager.PositionCount;
            for (int i = 0; i < count; i++)
            {
                if (manager.CanSelectPosition(playerSlot, i)) return i;
            }
            return 0;
        }

        internal void RefreshVisuals()
        {
            if (manager == null) return;

            var state = manager.GetState(playerSlot);
            if (state == null) return;

            SyncActiveRowToState();
            NormalizeCursors();

            if (playerLabel != null)
            {
                playerLabel.text = $"P{playerSlot + 1}";
            }

            if (statusLabel != null)
            {
                statusLabel.text = BuildStatus(state);
            }

            if (hintLabel != null)
            {
                hintLabel.text = BuildHint(state);
            }

            RefreshPositionButtons(state);
            RefreshColorButtons(state);
            RefreshReadyButton(state);

            if (background != null)
            {
                background.color = state.ready
                    ? new Color(0.12f, 0.22f, 0.14f, 0.95f)
                    : new Color(0.1f, 0.1f, 0.1f, 0.95f);
            }
        }

        private string BuildStatus(LocalPlayerSelectionState state)
        {
            if (!state.connected) return "未连接";
            if (state.ready) return "已准备";
            if (!state.participating) return "不参加";

            if (state.colorIndex < 0) return "未选颜色";
            if (state.positionIndex < 0) return $"颜色 {state.colorIndex + 1}";
            return $"颜色 {state.colorIndex + 1} / 位置 {state.positionIndex + 1}";
        }

        private string BuildHint(LocalPlayerSelectionState state)
        {
            if (state.ready) return "等待其他玩家 / 自动开始";

            return activeRow switch
            {
                CardRow.Color => "确认选择颜色",
                CardRow.Position => "确认选择位置",
                _ => "确认准备（全员准备后自动开始）",
            };
        }

        private void RefreshPositionButtons(LocalPlayerSelectionState state)
        {
            if (positionButtons == null) return;

            bool active = activeRow == CardRow.Position;
            for (int i = 0; i < positionButtons.Length; i++)
            {
                if (legacyPositionButtonsWithSkip && i == 0) continue;

                var button = positionButtons[i];
                if (button == null) continue;

                int value = legacyPositionButtonsWithSkip ? i - 1 : i;
                bool selected = active
                    ? positionCursor == value
                    : state.positionIndex == value;
                bool available = active && manager.CanSelectPosition(playerSlot, value);
                SetButtonVisual(button, selected, available);
            }
        }

        private void RefreshColorButtons(LocalPlayerSelectionState state)
        {
            if (colorButtons == null) return;

            bool active = activeRow == CardRow.Color;
            for (int i = 0; i < colorButtons.Length; i++)
            {
                var button = colorButtons[i];
                if (button == null) continue;

                int value = ColorButtonIndexToValue(i);
                bool selected = active
                    ? colorCursor == i
                    : (value < 0 ? !state.participating : state.participating && state.colorIndex == value);
                bool available = active && manager.CanSelectColor(playerSlot, value);
                SetButtonVisual(button, selected, available);
            }
        }

        private void RefreshReadyButton(LocalPlayerSelectionState state)
        {
            if (readyButton == null) return;

            bool active = activeRow == CardRow.Ready;
            bool canReady = active &&
                            manager.CanPlayerAct(playerSlot) &&
                            state.participating &&
                            state.colorIndex >= 0 &&
                            state.positionIndex >= 0 &&
                            !state.ready;

            readyButton.interactable = canReady;
            SetButtonVisual(readyButton, state.ready, canReady);

            if (readyBadge != null)
            {
                readyBadge.color = state.ready ? readyColor : disabledColor;
            }

            var label = readyButton.GetComponentInChildren<Text>();
            if (label != null)
            {
                if (!state.participating)
                {
                    label.text = "未参加";
                }
                else
                {
                    label.text = state.ready ? "已准备" : "准备";
                }
            }
        }

        private void SetButtonVisual(Button button, bool selected, bool available)
        {
            if (button == null) return;

            button.interactable = available;

            var image = button.targetGraphic as Image;
            if (image == null) image = button.GetComponent<Image>();
            if (image != null)
            {
                image.color = !available ? disabledColor : selected ? selectedColor : normalColor;
            }
        }
    }
}
