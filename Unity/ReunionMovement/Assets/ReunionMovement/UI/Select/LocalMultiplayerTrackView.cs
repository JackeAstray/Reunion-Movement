using System;
using UnityEngine;
using UnityEngine.UI;

namespace ReunionMovement.UI.Select
{
    /// <summary>
    /// 赛道位置示意：按玩家选择的颜色，在每个位置槽里显示 P1/P2... 的颜色标记。
    /// 因为位置允许重复选择，一个位置槽可以同时显示多个玩家标记。
    /// </summary>
    public class LocalMultiplayerTrackView : MonoBehaviour
    {
        [Serializable]
        public class SlotView
        {
            [Tooltip("该位置最多显示的玩家标记")]
            public Image[] markers;

            [Tooltip("与 markers 一一对应的 P1/P2 文本")]
            public Text[] labels;
        }

        [SerializeField] private LocalMultiplayerSelectionManager manager;
        [SerializeField] private SlotView[] slots;
        [SerializeField] private Color[] colors;

        private void Awake()
        {
            if (manager == null)
            {
                manager = GetComponentInParent<LocalMultiplayerSelectionManager>();
            }
        }

        private void OnEnable()
        {
            Subscribe();
            Refresh();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void Subscribe()
        {
            if (manager == null) return;
            manager.onPlayerStateChanged += OnPlayerStateChanged;
            manager.onStageChanged += OnStageChanged;
        }

        private void Unsubscribe()
        {
            if (manager == null) return;
            manager.onPlayerStateChanged -= OnPlayerStateChanged;
            manager.onStageChanged -= OnStageChanged;
        }

        private void OnPlayerStateChanged(int slot) => Refresh();
        private void OnStageChanged(LocalSelectionStage stage) => Refresh();

        public void Refresh()
        {
            if (slots == null) return;

            for (int i = 0; i < slots.Length; i++)
            {
                ClearSlot(slots[i]);
            }

            if (manager == null) return;

            // 最多 4 个玩家；GetState 会自动忽略不存在的卡片
            for (int player = 0; player < 4; player++)
            {
                var state = manager.GetState(player);
                if (state == null || !state.connected || !state.participating) continue;
                if (state.positionIndex < 0 || state.positionIndex >= slots.Length) continue;

                var slot = slots[state.positionIndex];
                if (slot == null) continue;

                int markerIndex = FindFreeMarker(slot);
                if (markerIndex < 0) continue;

                var marker = slot.markers[markerIndex];
                marker.gameObject.SetActive(true);
                marker.color = GetColor(state.colorIndex);

                if (slot.labels != null && markerIndex < slot.labels.Length && slot.labels[markerIndex] != null)
                {
                    var label = slot.labels[markerIndex];
                    label.gameObject.SetActive(true);
                    label.text = $"P{player + 1}";
                }
            }
        }

        private static void ClearSlot(SlotView slot)
        {
            if (slot == null) return;

            if (slot.markers != null)
            {
                for (int i = 0; i < slot.markers.Length; i++)
                {
                    if (slot.markers[i] != null) slot.markers[i].gameObject.SetActive(false);
                }
            }

            if (slot.labels != null)
            {
                for (int i = 0; i < slot.labels.Length; i++)
                {
                    if (slot.labels[i] != null) slot.labels[i].gameObject.SetActive(false);
                }
            }
        }

        private static int FindFreeMarker(SlotView slot)
        {
            if (slot.markers == null) return -1;

            for (int i = 0; i < slot.markers.Length; i++)
            {
                if (slot.markers[i] != null && !slot.markers[i].gameObject.activeSelf)
                {
                    return i;
                }
            }
            return -1;
        }

        private Color GetColor(int colorIndex)
        {
            if (colors != null && colorIndex >= 0 && colorIndex < colors.Length)
            {
                return colors[colorIndex];
            }
            return Color.white;
        }
    }
}
