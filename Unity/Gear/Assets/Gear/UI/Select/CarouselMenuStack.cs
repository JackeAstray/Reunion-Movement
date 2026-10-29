using System;
using System.Collections.Generic;
using UnityEngine;

namespace Gear.UI.Select
{
    /// <summary>
    /// 菜单层级栈：管理开始菜单各面板的打开、确认进入下一级、取消返回上一级。
    ///
    /// 适用场景：
    /// - 同一 Canvas 内的开始菜单面板（开始页面 → 首页 → 开始游戏/设置）；
    /// - 每一级放一个 CarouselSelectionController，面板默认 inactive；
    /// - 本组件接管轮播的确认/取消，统一处理层级栈，避免每个轮播各自关闭窗口。
    ///
    /// 独立 UIController 窗口也可以作为一级，但那种情况更推荐用
    /// CarouselSelectionController 的 Close Target 交给 UISystem 管理。
    /// </summary>
    public class CarouselMenuStack : MonoBehaviour
    {
        [Serializable]
        public class Level
        {
            [Tooltip("该级面板根节点；打开时 SetActive(true)，关闭时 SetActive(false)")]
            public GameObject panel;

            [Tooltip("该级选择框；留空则从 panel 下自动查找")]
            public CarouselSelectionController carousel;

            [Tooltip("确认后是否自动前进；关闭时只触发 onLevelConfirmed，由外部决定跳转")]
            public bool advanceOnConfirm = true;

            [Tooltip("按选项索引指定确认后进入的层级；留空则进入下一级。值 -1 表示完成")]
            public int[] nextLevelByOption;
        }

        [Tooltip("按层级顺序排列：0 为主菜单，1 为第一级子菜单，依此类推")]
        [SerializeField] private Level[] levels;

        [Tooltip("启用时自动打开第 0 级")]
        [SerializeField] private bool openFirstLevelOnEnable = true;

        [Tooltip("在根级按取消时是否关闭整个菜单栈")]
        [SerializeField] private bool closeRootOnCancel = false;

        [Tooltip("最后一级确认后是否关闭整个菜单栈")]
        [SerializeField] private bool closeOnCompleted = true;

        private readonly List<int> openLevels = new List<int>(4);
        private Action<int>[] confirmHandlers;
        private Action[] cancelHandlers;
        private bool listenersBound;

        /// <summary>某一级被打开（参数为层级索引）。</summary>
        public event Action<int> onLevelOpened;

        /// <summary>某一级被关闭（参数为层级索引）。</summary>
        public event Action<int> onLevelClosed;

        /// <summary>某一级确认（参数：层级索引、选项索引）；可用于主菜单分支跳转。</summary>
        public event Action<int, int> onLevelConfirmed;

        /// <summary>最后一级确认完成。</summary>
        public event Action onCompleted;

        public int CurrentLevel => openLevels.Count > 0 ? openLevels[openLevels.Count - 1] : -1;

        public bool IsOpen => openLevels.Count > 0;

        private void Awake()
        {
            if (levels == null) return;

            for (int i = 0; i < levels.Length; i++)
            {
                var level = levels[i];
                if (level == null || level.panel == null) continue;
                if (level.carousel == null)
                {
                    level.carousel = level.panel.GetComponentInChildren<CarouselSelectionController>(true);
                }
            }
        }

        private void OnEnable()
        {
            BindListeners();

            // 收起所有子级，避免编辑器里遗留的 active 状态导致多层同时显示
            if (levels != null)
            {
                for (int i = 1; i < levels.Length; i++)
                {
                    SetPanelActive(i, false);
                }
            }

            if (openFirstLevelOnEnable && !IsOpen)
            {
                OpenLevel(0);
            }
        }

        private void OnDisable()
        {
            UnbindListeners();
            CloseAll(notify: false);
        }

        private void OnDestroy()
        {
            UnbindListeners();
        }

        /// <summary>打开指定层级；如果它已在栈中，会先关闭它上面的层级。</summary>
        public void OpenLevel(int index)
        {
            if (!IsValidLevel(index)) return;

            if (CurrentLevel == index)
            {
                SetPanelActive(index, true);
                return;
            }

            int existing = openLevels.IndexOf(index);
            if (existing >= 0)
            {
                while (openLevels.Count > existing + 1)
                {
                    CloseTop(notify: true);
                }
                SetPanelActive(index, true);
                RaiseLevelOpened(index);
                return;
            }

            if (openLevels.Count > 0)
            {
                SetPanelActive(openLevels[openLevels.Count - 1], false);
            }

            openLevels.Add(index);
            SetPanelActive(index, true);
            RaiseLevelOpened(index);
        }

        /// <summary>关闭当前层级并返回上一级。</summary>
        public void CloseCurrent()
        {
            if (openLevels.Count == 0) return;

            CloseTop(notify: true);

            if (openLevels.Count > 0)
            {
                SetPanelActive(openLevels[openLevels.Count - 1], true);
            }
        }

        /// <summary>关闭整个菜单栈（父菜单关闭时调用）。</summary>
        public void CloseAll()
        {
            CloseAll(notify: true);
        }

        private void CloseAll(bool notify)
        {
            for (int i = openLevels.Count - 1; i >= 0; i--)
            {
                int index = openLevels[i];
                SetPanelActive(index, false);
                if (notify) RaiseLevelClosed(index);
            }
            openLevels.Clear();
        }

        private void CloseTop(bool notify)
        {
            int index = openLevels[openLevels.Count - 1];
            openLevels.RemoveAt(openLevels.Count - 1);
            SetPanelActive(index, false);
            if (notify) RaiseLevelClosed(index);
        }

        private void RaiseLevelOpened(int index)
        {
            try { onLevelOpened?.Invoke(index); }
            catch (Exception ex) { Debug.LogWarning($"[CarouselMenuStack] onLevelOpened 订阅者异常: {ex.Message}"); }
        }

        private void RaiseLevelClosed(int index)
        {
            try { onLevelClosed?.Invoke(index); }
            catch (Exception ex) { Debug.LogWarning($"[CarouselMenuStack] onLevelClosed 订阅者异常: {ex.Message}"); }
        }

        private void HandleConfirmed(int levelIndex, int selectedIndex)
        {
            if (levelIndex != CurrentLevel) return;

            try { onLevelConfirmed?.Invoke(levelIndex, selectedIndex); }
            catch (Exception ex) { Debug.LogWarning($"[CarouselMenuStack] onLevelConfirmed 订阅者异常: {ex.Message}"); }

            // 外部在 onLevelConfirmed 里可能已经跳转/关闭，避免再自动前进一次
            if (levelIndex != CurrentLevel) return;

            var level = levels[levelIndex];
            if (!level.advanceOnConfirm) return;

            int next = levelIndex + 1;
            var branch = level.nextLevelByOption;
            if (branch != null && selectedIndex >= 0 && selectedIndex < branch.Length)
            {
                next = branch[selectedIndex];
            }

            if (next < 0 || next >= levels.Length)
            {
                Complete();
                return;
            }

            OpenLevel(next);
        }

        private void Complete()
        {
            try { onCompleted?.Invoke(); }
            catch (Exception ex) { Debug.LogWarning($"[CarouselMenuStack] onCompleted 订阅者异常: {ex.Message}"); }

            if (closeOnCompleted)
            {
                CloseAll();
            }
        }

        private void HandleCancelled(int levelIndex)
        {
            if (levelIndex != CurrentLevel) return;

            if (openLevels.Count > 1)
            {
                CloseCurrent();
                return;
            }

            if (closeRootOnCancel)
            {
                CloseAll();
            }
        }

        private void BindListeners()
        {
            if (listenersBound || levels == null) return;

            confirmHandlers = new Action<int>[levels.Length];
            cancelHandlers = new Action[levels.Length];

            for (int i = 0; i < levels.Length; i++)
            {
                var level = levels[i];
                if (level == null || level.carousel == null) continue;

                int index = i;
                confirmHandlers[i] = selected => HandleConfirmed(index, selected);
                cancelHandlers[i] = () => HandleCancelled(index);

                // 由菜单栈统一处理层级，轮播不再自己关闭窗口
                level.carousel.CloseOnConfirm = false;
                level.carousel.CloseOnCancel = false;
                level.carousel.onConfirmed += confirmHandlers[i];
                level.carousel.onCancelled += cancelHandlers[i];
            }

            listenersBound = true;
        }

        private void UnbindListeners()
        {
            if (!listenersBound || levels == null) return;

            for (int i = 0; i < levels.Length; i++)
            {
                var level = levels[i];
                if (level == null || level.carousel == null) continue;

                if (confirmHandlers != null && i < confirmHandlers.Length && confirmHandlers[i] != null)
                {
                    level.carousel.onConfirmed -= confirmHandlers[i];
                }
                if (cancelHandlers != null && i < cancelHandlers.Length && cancelHandlers[i] != null)
                {
                    level.carousel.onCancelled -= cancelHandlers[i];
                }
            }

            confirmHandlers = null;
            cancelHandlers = null;
            listenersBound = false;
        }

        private bool IsValidLevel(int index)
        {
            return levels != null && index >= 0 && index < levels.Length &&
                   levels[index] != null && levels[index].panel != null;
        }

        private void SetPanelActive(int index, bool active)
        {
            if (!IsValidLevel(index)) return;

            var panel = levels[index].panel;
            if (panel != null && panel.activeSelf != active)
            {
                panel.SetActive(active);
            }
        }
    }
}
