using System;
using System.Collections.Generic;
using ReunionMovement.Core.UIInput;
using UnityEngine;
using UnityEngine.UI;

namespace ReunionMovement.UI.Select
{
    public enum LocalSelectionStage
    {
        Selecting,
        Countdown,
        Started,
    }

    [Serializable]
    public class LocalPlayerSelectionState
    {
        public bool connected;
        public bool participating;
        public int positionIndex = -1;
        public int colorIndex = -1;
        public bool ready;

        /// <summary>是否已经明确选择过（颜色/不参加/位置/准备）。</summary>
        public bool decided;
    }

    /// <summary>
    /// 本地多人选择界面（格斗/赛车风格）：P1-Pn 同时选颜色、位置并准备。
    ///
    /// 每个玩家独立推进：
    /// 1. Color：选择颜色即参加；选「不参加」则退出；
    /// 2. Position：选完颜色后立即进入位置选择，不需要等其他人或 P1；
    /// 3. Ready：选完位置后确认准备；准备后锁定；
    /// 4. 所有已连接玩家都准备/已明确不参加后自动倒计时。
    ///
    /// 颜色和位置默认唯一：已被占用的选项对其他玩家置灰。
    /// 其他菜单仍然只由 P1 操作；只有这个选择界面开放 P1-Pn 输入。
    /// </summary>
    public class LocalMultiplayerSelectionManager : MonoBehaviour
    {
        [Header("玩家卡片（按 P1、P2、P3、P4 顺序）")]
        [SerializeField] private LocalMultiplayerPlayerCard[] cards;

        [Header("规则")]
        [SerializeField] private int positionCount = 4;
        [SerializeField] private int colorCount = 8;
        [SerializeField] private bool includeKeyboardMouseAsP1 = true;
        [Tooltip("允许不同玩家选择同一个颜色/位置；关闭后保持唯一占用")]
        [SerializeField] private bool allowDuplicateSelections = false;
        [Tooltip("所有已连接玩家都准备/不参加后自动倒计时")]
        [SerializeField] private bool autoStartWhenAllReady = true;
        [SerializeField] private float countdownSeconds = 3f;

        [Header("界面文本")]
        [SerializeField] private Text stageLabel;
        [SerializeField] private Text countdownLabel;
        [SerializeField] private Text hintLabel;

        private readonly List<UIInputDeviceInfo> deviceBuffer = new List<UIInputDeviceInfo>(8);
        private LocalPlayerSelectionState[] states = Array.Empty<LocalPlayerSelectionState>();
        private LocalSelectionStage stage = LocalSelectionStage.Selecting;
        private float deviceRefreshTimer;
        private float countdownRemaining;
        private bool countdownRunning;
        private bool initialized;
        private bool playersScanned;

        public event Action<LocalSelectionStage> onStageChanged;
        public event Action<int> onPlayerStateChanged;
        public event Action<int> onCountdownChanged;
        public event Action onGameStarted;

        public LocalSelectionStage Stage => stage;
        public int PositionCount => positionCount;
        public int ColorCount => colorCount;
        public bool IsCountdownRunning => countdownRunning;
        public float CountdownRemaining => countdownRemaining;
        public int PlayerCount => CountConnectedPlayers();

        public bool IsSelectionOpen =>
            stage != LocalSelectionStage.Started && gameObject.activeInHierarchy;

        private void Awake()
        {
            Initialize();
        }

        private void OnEnable()
        {
            Initialize();
            ResetForOpen();
            RefreshConnectedPlayers(force: true);
            UpdateStageTexts();
        }

        private void OnDisable()
        {
            countdownRunning = false;
            countdownRemaining = 0f;
        }

        private void Update()
        {
            if (UIInputSystem.Instance.isInited)
            {
                deviceRefreshTimer -= Time.unscaledDeltaTime;
                if (deviceRefreshTimer <= 0f)
                {
                    deviceRefreshTimer = 0.5f;
                    RefreshConnectedPlayers(force: false);
                }
            }

            if (!countdownRunning) return;

            countdownRemaining -= Time.unscaledDeltaTime;
            if (countdownRemaining <= 0f)
            {
                countdownRunning = false;
                countdownRemaining = 0f;
                SetStage(LocalSelectionStage.Started);
                try { onGameStarted?.Invoke(); }
                catch (Exception ex) { Debug.LogWarning($"[LocalMultiplayerSelection] onGameStarted 订阅者异常: {ex.Message}"); }
                return;
            }

            UpdateCountdownText();
            try { onCountdownChanged?.Invoke(Mathf.CeilToInt(countdownRemaining)); }
            catch (Exception ex) { Debug.LogWarning($"[LocalMultiplayerSelection] onCountdownChanged 订阅者异常: {ex.Message}"); }
        }

        private void Initialize()
        {
            if (cards == null || cards.Length == 0)
            {
                cards = GetComponentsInChildren<LocalMultiplayerPlayerCard>(true);
            }

            if (states.Length != cards.Length)
            {
                states = new LocalPlayerSelectionState[cards.Length];
                for (int i = 0; i < states.Length; i++)
                {
                    states[i] = new LocalPlayerSelectionState();
                }
            }

            for (int i = 0; i < cards.Length; i++)
            {
                if (cards[i] != null)
                {
                    cards[i].Initialize(this, i);
                }
            }

            initialized = true;
        }

        private void ResetForOpen()
        {
            if (!initialized) Initialize();

            stage = LocalSelectionStage.Selecting;
            countdownRunning = false;
            countdownRemaining = 0f;
            deviceRefreshTimer = 0f;
            playersScanned = false;

            for (int i = 0; i < states.Length; i++)
            {
                ResetState(states[i]);
                if (cards[i] != null)
                {
                    cards[i].OnStageChanged();
                    cards[i].RefreshVisuals();
                }
            }

            UpdateStageTexts();
            try { onStageChanged?.Invoke(stage); }
            catch (Exception ex) { Debug.LogWarning($"[LocalMultiplayerSelection] onStageChanged 订阅者异常: {ex.Message}"); }
        }

        private static void ResetState(LocalPlayerSelectionState state)
        {
            state.participating = false;
            state.positionIndex = -1;
            state.colorIndex = -1;
            state.ready = false;
            state.decided = false;
        }

        private void RefreshConnectedPlayers(bool force)
        {
            if (cards.Length == 0)
            {
                playersScanned = true;
                return;
            }

            if (!UIInputSystem.Instance.isInited)
            {
                playersScanned = false;
                return;
            }

            UIInputSystem.Instance.GetConnectedDeviceInfos(deviceBuffer);
            playersScanned = true;
            bool[] connected = new bool[cards.Length];

            for (int i = 0; i < deviceBuffer.Count; i++)
            {
                var info = deviceBuffer[i];
                if (!includeKeyboardMouseAsP1 && info.Kind == UIInputDeviceKind.KeyboardMouse)
                {
                    continue;
                }

                if (info.PlayerIndex >= 0 && info.PlayerIndex < connected.Length)
                {
                    connected[info.PlayerIndex] = true;
                }
            }

            bool changed = false;
            for (int i = 0; i < cards.Length; i++)
            {
                changed |= SetConnected(i, connected[i], force);
            }

            if (changed)
            {
                RefreshAllVisuals();
                UpdateStageTexts();
                CheckAutoStart();
            }
        }

        private bool SetConnected(int slot, bool connected, bool force)
        {
            if (slot < 0 || slot >= states.Length) return false;
            var state = states[slot];
            if (!force && state.connected == connected) return false;

            bool wasConnected = state.connected;
            state.connected = connected;

            if (!connected)
            {
                ResetState(state);
            }
            else if (!wasConnected && stage != LocalSelectionStage.Selecting)
            {
                // 倒计时/开始后才接入的设备不参与本局
                state.decided = true;
            }

            if (cards[slot] != null)
            {
                cards[slot].gameObject.SetActive(connected);
                cards[slot].RefreshVisuals();
            }

            NotifyPlayerChanged(slot, refreshAll: false);
            return true;
        }

        public LocalPlayerSelectionState GetState(int slot)
        {
            if (slot < 0 || slot >= states.Length) return null;
            return states[slot];
        }

        public LocalMultiplayerPlayerCard GetCard(int slot)
        {
            if (cards == null || slot < 0 || slot >= cards.Length) return null;
            return cards[slot];
        }

        public bool IsConnected(int slot) => GetState(slot)?.connected == true;
        public bool IsParticipating(int slot) => GetState(slot)?.participating == true;
        public bool IsPlayerReady(int slot) => GetState(slot)?.ready == true;

        public bool CanPlayerAct(int slot)
        {
            if (!playersScanned) return false;

            var state = GetState(slot);
            if (state == null || !state.connected || state.ready) return false;
            return stage == LocalSelectionStage.Selecting;
        }

        public bool CanSelectColor(int slot, int colorIndex)
        {
            if (!CanPlayerAct(slot)) return false;
            if (colorIndex < 0) return true; // 不参加
            if (colorIndex >= colorCount) return false;
            return allowDuplicateSelections || !IsColorTaken(colorIndex, slot);
        }

        public bool CanSelectPosition(int slot, int positionIndex)
        {
            if (!CanPlayerAct(slot)) return false;

            var state = GetState(slot);
            if (state == null || state.colorIndex < 0) return false;

            if (positionIndex < 0) return true; // 不参加
            if (positionIndex >= positionCount) return false;
            return allowDuplicateSelections || !IsPositionTaken(positionIndex, slot);
        }

        public bool SelectColor(int slot, int colorIndex)
        {
            if (!CanSelectColor(slot, colorIndex)) return false;

            var state = states[slot];
            if (colorIndex < 0)
            {
                ResetState(state);
            }
            else
            {
                state.participating = true;
                state.colorIndex = colorIndex;
                state.ready = false;
            }

            state.decided = true;
            NotifyPlayerChanged(slot, refreshAll: true);
            CheckAutoStart();
            return true;
        }

        public bool SelectPosition(int slot, int positionIndex)
        {
            if (!CanSelectPosition(slot, positionIndex)) return false;

            var state = states[slot];
            if (positionIndex < 0)
            {
                ResetState(state);
            }
            else
            {
                state.participating = true;
                state.positionIndex = positionIndex;
                state.ready = false;
            }

            state.decided = true;
            NotifyPlayerChanged(slot, refreshAll: true);
            CheckAutoStart();
            return true;
        }

        public bool SetReady(int slot)
        {
            if (!CanPlayerAct(slot)) return false;

            var state = states[slot];
            if (!state.participating || state.colorIndex < 0 || state.positionIndex < 0) return false;

            state.ready = true;
            state.decided = true;
            NotifyPlayerChanged(slot, refreshAll: true);
            CheckAutoStart();
            return true;
        }

        public bool CancelPlayer(int slot)
        {
            if (!CanPlayerAct(slot)) return false;

            var state = states[slot];
            ResetState(state);
            state.decided = true;
            NotifyPlayerChanged(slot, refreshAll: true);
            return true;
        }

        private void StartCountdown()
        {
            if (countdownRunning) return;

            countdownRemaining = Mathf.Max(0.01f, countdownSeconds);
            countdownRunning = true;
            SetStage(LocalSelectionStage.Countdown);
            UpdateCountdownText();
            try { onCountdownChanged?.Invoke(Mathf.CeilToInt(countdownRemaining)); }
            catch (Exception ex) { Debug.LogWarning($"[LocalMultiplayerSelection] onCountdownChanged 订阅者异常: {ex.Message}"); }
        }

        private void CheckAutoStart()
        {
            if (!autoStartWhenAllReady) return;
            if (!playersScanned) return;
            if (stage != LocalSelectionStage.Selecting || countdownRunning) return;
            if (ParticipatingCount() == 0) return;

            // 已参加玩家必须准备；不参加玩家必须已经明确选择过「不参加」
            if (!AllConnectedReadyOrDeclined()) return;

            StartCountdown();
        }

        private void SetStage(LocalSelectionStage next)
        {
            if (stage == next) return;

            stage = next;
            countdownRunning = next == LocalSelectionStage.Countdown;
            if (!countdownRunning) countdownRemaining = 0f;

            for (int i = 0; i < cards.Length; i++)
            {
                if (cards[i] != null)
                {
                    cards[i].OnStageChanged();
                    cards[i].RefreshVisuals();
                }
            }

            UpdateStageTexts();
            try { onStageChanged?.Invoke(stage); }
            catch (Exception ex) { Debug.LogWarning($"[LocalMultiplayerSelection] onStageChanged 订阅者异常: {ex.Message}"); }
        }

        private void NotifyPlayerChanged(int slot, bool refreshAll)
        {
            if (refreshAll) RefreshAllVisuals();
            else if (cards != null && slot >= 0 && slot < cards.Length && cards[slot] != null)
            {
                cards[slot].RefreshVisuals();
            }

            try { onPlayerStateChanged?.Invoke(slot); }
            catch (Exception ex) { Debug.LogWarning($"[LocalMultiplayerSelection] onPlayerStateChanged 订阅者异常: {ex.Message}"); }
        }

        private void RefreshAllVisuals()
        {
            if (cards == null) return;
            for (int i = 0; i < cards.Length; i++)
            {
                if (cards[i] != null) cards[i].RefreshVisuals();
            }
        }

        private int ParticipatingCount()
        {
            int count = 0;
            for (int i = 0; i < states.Length; i++)
            {
                if (states[i].connected && states[i].participating) count++;
            }
            return count;
        }

        private bool AllConnectedReadyOrDeclined()
        {
            bool any = false;
            for (int i = 0; i < states.Length; i++)
            {
                if (!states[i].connected) continue;
                any = true;

                if (states[i].participating)
                {
                    if (!states[i].ready) return false;
                }
                else if (!states[i].decided)
                {
                    return false;
                }
            }
            return any;
        }

        private int CountConnectedPlayers()
        {
            int count = 0;
            for (int i = 0; i < states.Length; i++)
            {
                if (states[i].connected) count++;
            }
            return count;
        }

        private bool IsPositionTaken(int positionIndex, int exceptSlot)
        {
            for (int i = 0; i < states.Length; i++)
            {
                if (i == exceptSlot) continue;
                if (states[i].connected && states[i].participating && states[i].positionIndex == positionIndex)
                {
                    return true;
                }
            }
            return false;
        }

        private bool IsColorTaken(int colorIndex, int exceptSlot)
        {
            for (int i = 0; i < states.Length; i++)
            {
                if (i == exceptSlot) continue;
                if (states[i].connected && states[i].participating && states[i].colorIndex == colorIndex)
                {
                    return true;
                }
            }
            return false;
        }

        private void UpdateStageTexts()
        {
            if (stageLabel != null)
            {
                stageLabel.text = stage switch
                {
                    LocalSelectionStage.Selecting => "选择颜色 → 位置 → 准备",
                    LocalSelectionStage.Countdown => "即将开始",
                    _ => "游戏开始",
                };
            }

            if (hintLabel != null)
            {
                hintLabel.text = stage switch
                {
                    LocalSelectionStage.Selecting => "选颜色后自动进入位置；所有玩家准备后自动开始",
                    LocalSelectionStage.Countdown => "准备开始……",
                    _ => "游戏开始",
                };
            }

            UpdateCountdownText();
        }

        private void UpdateCountdownText()
        {
            if (countdownLabel == null) return;

            if (countdownRunning)
            {
                countdownLabel.text = Mathf.CeilToInt(countdownRemaining).ToString();
            }
            else if (stage == LocalSelectionStage.Started)
            {
                countdownLabel.text = "GO";
            }
            else
            {
                countdownLabel.text = string.Empty;
            }
        }
    }
}
