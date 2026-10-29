using Astray.Framework.Runtime.UIInput;
using UnityEngine;

namespace Astray.UI.Select
{
    /// <summary>
    /// 多个选择框的输入分组。
    ///
    /// 适用场景：
    /// - 同一个玩家（一个方向盘/手柄/键盘）需要在“角色、颜色、涂装”等多个选择框之间切换；
    /// - 多个方向盘分别控制不同选择框时，不需要本组件，直接给每个 CarouselSelectionController
    ///   设置不同的 Player Slot 即可。
    ///
    /// 工作方式：
    /// - 组内选择框的独立输入被关闭，由本组件统一接收输入并转发给“活动选择框”；
    /// - 主轴输入（横向列表的左右、纵向列表的上下）操作当前选择框的选项；
    /// - 副轴输入（横向列表的上下、纵向列表的左右）在多个选择框之间切换；
    /// - 矩阵模式（columns &gt; 1）的选择框会消费所有方向，避免同时切换选择框造成误操作；
    /// - 无十字键方向盘（RS50 等）的转向用于操作当前选择框；如需切换选择框，
    ///   可用 SetDeviceNavigation 把上下绑定到方向盘按钮（横向列表时上下即副轴）。
    /// </summary>
    public class CarouselSelectionGroup : MonoBehaviour, IUINavigationTarget
    {
        [Tooltip("组内选择框（按切换顺序排列）")]
        [SerializeField] private CarouselSelectionController[] carousels;

        [Tooltip("玩家槽位：0 = 键盘/鼠标 + 第一个实体控制器；1 = 第二个方向盘/手柄；-1 = 任意玩家")]
        [SerializeField] private int playerSlot = 0;

        [Tooltip("是否用副轴在多个选择框之间切换（横向列表用上下，纵向列表用左右）")]
        [SerializeField] private bool switchWithSecondaryAxis = true;

        [Tooltip("切换选择框时是否首尾循环")]
        [SerializeField] private bool wrap = true;

        [Tooltip("是否自动关闭组内选择框的独立输入并同步 Player Slot")]
        [SerializeField] private bool autoConfigure = true;

        [Tooltip("优先级，需高于组内选择框（默认即可）")]
        [SerializeField] private int inputPriority = 1000;

        [Tooltip("窗口焦点域创建时是否请求 UI 控制模式")]
        [SerializeField] private bool autoEnableUIControl = true;

        private int activeIndex;
        private bool registered;
        private int lastInitVersion = -1;

        // 组启用前的选择框配置，禁用组时原样恢复
        private int[] cachedPlayerSlots;
        private bool[] cachedInputEnabled;
        private bool hasCachedState;

        public int InputPriority => inputPriority;

        public int PlayerSlot => playerSlot;

        public int ActiveIndex => activeIndex;

        public CarouselSelectionController ActiveCarousel
        {
            get
            {
                if (carousels == null || carousels.Length == 0) return null;
                if (activeIndex < 0 || activeIndex >= carousels.Length) return null;
                return carousels[activeIndex];
            }
        }

        private void OnEnable()
        {
            if (autoConfigure)
            {
                ConfigureCarousels();
            }

            if (UIInputSystem.Instance.isInited)
            {
                Register();
            }
        }

        private void OnDisable()
        {
            Unregister();

            if (autoConfigure)
            {
                RestoreCarousels();
            }
        }

        private void Update()
        {
            var inputSystem = UIInputSystem.Instance;
            if (!inputSystem.isInited) return;

            int version = inputSystem.InitVersion;
            if (lastInitVersion == version && registered) return;

            lastInitVersion = version;
            Unregister();
            Register();
        }

        private void Register()
        {
            if (registered) return;
            registered = true;
            UIInputSystem.Instance.RegisterNavigationTarget(this, autoEnableUIControl);
        }

        private void Unregister()
        {
            if (!registered) return;
            registered = false;
            UIInputSystem.Instance.UnregisterNavigationTarget(this);
        }

        private void ConfigureCarousels()
        {
            if (carousels == null || carousels.Length == 0) return;

            if (!hasCachedState)
            {
                cachedPlayerSlots = new int[carousels.Length];
                cachedInputEnabled = new bool[carousels.Length];
                for (int i = 0; i < carousels.Length; i++)
                {
                    var carousel = carousels[i];
                    cachedPlayerSlots[i] = carousel != null ? carousel.PlayerSlot : -1;
                    cachedInputEnabled[i] = carousel == null || carousel.InputEnabled;
                }
                hasCachedState = true;
            }

            activeIndex = Mathf.Clamp(activeIndex, 0, carousels.Length - 1);
            for (int i = 0; i < carousels.Length; i++)
            {
                var carousel = carousels[i];
                if (carousel == null) continue;
                carousel.SetInputEnabled(false);
                carousel.SetPlayerSlot(playerSlot);
                carousel.SetGroupActive(i == activeIndex);
                carousel.AttachGroup(this);
            }
        }

        private void RestoreCarousels()
        {
            if (carousels == null) return;
            bool restoreCached = hasCachedState && cachedPlayerSlots != null &&
                                 cachedPlayerSlots.Length == carousels.Length;
            for (int i = 0; i < carousels.Length; i++)
            {
                var carousel = carousels[i];
                if (carousel == null) continue;
                carousel.AttachGroup(null);
                if (restoreCached)
                {
                    carousel.SetPlayerSlot(cachedPlayerSlots[i]);
                    carousel.SetInputEnabled(cachedInputEnabled[i]);
                }
                else
                {
                    carousel.SetInputEnabled(true);
                }
                carousel.SetGroupActive(true);
            }

            hasCachedState = false;
            cachedPlayerSlots = null;
            cachedInputEnabled = null;
        }

        /// <summary>鼠标操作某个选择框时，把它同步为当前活动选择框。</summary>
        internal void NotifyPointerFocus(CarouselSelectionController carousel)
        {
            if (carousels == null || carousel == null) return;
            for (int i = 0; i < carousels.Length; i++)
            {
                if (carousels[i] == carousel)
                {
                    SetActiveIndex(i);
                    return;
                }
            }
        }

        /// <summary>切换活动选择框（外部也可调用）。</summary>
        public void SetActiveIndex(int index)
        {
            if (carousels == null || carousels.Length == 0) return;
            if (index < 0 || index >= carousels.Length) return;

            activeIndex = Mathf.Clamp(activeIndex, 0, carousels.Length - 1);
            if (index == activeIndex) return;

            var previous = carousels[activeIndex];
            if (previous != null) previous.SetGroupActive(false);

            activeIndex = index;

            var current = carousels[activeIndex];
            if (current != null) current.SetGroupActive(true);
        }

        private void SwitchActive(int direction)
        {
            if (carousels == null || carousels.Length <= 1 || direction == 0) return;

            int next = activeIndex + direction;
            if (wrap)
            {
                next = (next % carousels.Length + carousels.Length) % carousels.Length;
            }
            else
            {
                next = Mathf.Clamp(next, 0, carousels.Length - 1);
            }

            SetActiveIndex(next);
        }

        public bool AcceptsPlayer(int playerIndex)
        {
            if (!isActiveAndEnabled) return false;
            return playerSlot < 0 || playerIndex == playerSlot;
        }

        public bool HandleNavigate(in UINavigateEvent e)
        {
            if (!AcceptsPlayer(e.PlayerIndex)) return false;
            if (e.Step == Vector2Int.zero) return false;

            var active = ActiveCarousel;
            if (active == null || !active.isActiveAndEnabled) return false;

            // 无十字键方向盘（RS50 等）：转向只操作当前选择框。
            // 多选择框场景请让每只方向盘控制一个选择框，或为方向盘绑定自定义导航按钮。
            if (e.IsWheelSteering)
            {
                active.ApplySteering(e.Step.x);
                return true;
            }

            if (active.IsMatrixNavigation)
            {
                // 矩阵选择框两个轴都用于选项导航，不自动切换选择框
                active.ApplyStep(e.Step);
                return true;
            }

            if (active.IsHorizontalNavigation)
            {
                if (e.Step.x != 0)
                {
                    active.ApplyStep(new Vector2Int(e.Step.x, 0));
                    return true;
                }
                if (switchWithSecondaryAxis && e.Step.y != 0)
                {
                    SwitchActive(-e.Step.y); // 上 = 上一个选择框，下 = 下一个
                    return true;
                }
                return false;
            }

            if (active.IsVerticalNavigation)
            {
                if (e.Step.y != 0)
                {
                    active.ApplyStep(e.Step);
                    return true;
                }
                if (switchWithSecondaryAxis && e.Step.x != 0)
                {
                    SwitchActive(e.Step.x); // 右 = 下一个选择框
                    return true;
                }
                return false;
            }

            return false;
        }

        public bool HandleSubmit(in UIButtonEvent e)
        {
            if (!AcceptsPlayer(e.PlayerIndex)) return false;
            var active = ActiveCarousel;
            if (active == null || !active.isActiveAndEnabled) return false;
            active.Confirm();
            return true;
        }

        public bool HandleCancel(in UIButtonEvent e)
        {
            if (!AcceptsPlayer(e.PlayerIndex)) return false;
            var active = ActiveCarousel;
            if (active == null || !active.isActiveAndEnabled) return false;
            active.CancelSelection();
            return true;
        }
    }
}
