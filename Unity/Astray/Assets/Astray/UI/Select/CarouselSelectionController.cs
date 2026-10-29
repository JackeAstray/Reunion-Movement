using System;
using System.Collections.Generic;
using Astray.Common;
using Astray.Framework.Runtime.UI;
using Astray.Framework.Runtime.UIInput;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Astray.UI.Select
{
    /// <summary>
    /// 轮播式选择控制器 —— 适用于“选择角色/车辆/颜色”等界面。
    ///
    /// 方向输入（键盘 WASD/方向键、手柄十字键/左摇杆、方向盘 hat、飞行摇杆 stick/hat）切换选项：
    /// - columns &gt; 1：矩阵模式，上下左右同时响应（左右行内/行间切换，上下跨行，均环绕）
    ///   支持不规则行：rowSizes 配置每行个数（如 4,4,2），留空则按 columns 均匀分列（最后一行可不满）
    ///   支持空位（洞）：options 中为 null 或不可交互的项会被导航自动跳过
    /// - columns = 1：线性模式，按 navigateAxis 取横向或纵向
    /// 确认/取消：
    /// - 键盘：Enter / Escape
    /// - 手柄：A / B
    /// - 赛车方向盘：默认 button1（A / ✕）；取消按型号区分：Xbox 为 button2（B），罗技 PS 版为 button3（○）
    /// - 无十字键的方向盘（RS50 等）：左右打方向 = 上一项/下一项，回正后可再次切换
    /// - 飞行摇杆：默认 trigger / button2
    /// 鼠标：
    /// - 悬停切换（可关闭）、点击选中、再次点击确认（可关闭）、滚轮切换
    ///
    /// 多方向盘同时操作：
    /// - 每个选择框设置不同的 Player Slot（0 = 键盘/鼠标 + 第一个实体控制器，1 = 第二个方向盘……）
    /// - 同一玩家需要操作多个选择框时，用 CarouselSelectionGroup 自动切换活动选择框
    ///
    /// 使用说明：
    /// 1. 本组件是普通 MonoBehaviour，挂在窗口节点或子节点上即可（同一窗口可放多个）；
    ///    窗口本身由 UIController 管理，不要把本组件再当成第二个 UIController。
    /// 2. 将每个选项的 Button 填入 options（Button 的 Navigation 建议设为 None；本控制器不再依赖 EventSystem 导航）。
    /// 3. previews 为按选项索引显示的角色模型/立绘（可空）。
    /// 4. UI 控制模式由窗口焦点域（UIFocusScope/UIController）负责；Auto Enable UI Control 只在
    ///    没有 UIController 的纯 Canvas 场景创建焦点域时作为提示。
    /// 5. 多个方向盘控制不同选择框时，给每个选择框分配不同的 Player Slot，并关闭 Set Event System Focus，
    ///    避免多个选择框互相抢焦点。
    /// </summary>
    public class CarouselSelectionController : MonoBehaviour, IUINavigationTarget
    {
        [Tooltip("所属窗口名；留空时向上查找最近的 UIController 窗口")]
        [SerializeField] private string uiName;

        /// <summary>线性模式导航响应轴：横向（左右键/左摇杆 X）或纵向（上下键/左摇杆 Y）</summary>
        private enum NavigateAxis { Horizontal, Vertical }

        [Header("选项列表（按索引顺序）")]
        [SerializeField] private Button[] options;

        [Header("预览对象（与选项一一对应，切换时显示当前项）")]
        [SerializeField] private GameObject[] previews;

        [Header("普通高亮色 / 当前选中高亮色 / 非激活选择框高亮色")]
        [SerializeField] private Color normalColor = new Color(0.8f, 0.8f, 0.8f, 1f);
        [SerializeField] private Color selectedColor = Color.white;
        [SerializeField] private Color inactiveSelectedColor = new Color(0.55f, 0.55f, 0.55f, 1f);

        [Header("布局")]
        [Tooltip("矩阵最大列数：>1 启用矩阵模式；=1 为线性模式。Row Sizes 为空时用它把选项均匀分列")]
        [SerializeField] private int columns = 1;
        [Tooltip("每行实际选项数（不是列号），如 3,2,2,3；总和必须等于 Options 数量。留空则按 Columns 均匀分列")]
        [SerializeField] private int[] rowSizes;
        [Tooltip("线性模式响应导航的方向轴：横向（左右键/左摇杆 X）或纵向（上下键/左摇杆 Y）")]
        [SerializeField] private NavigateAxis navigateAxis = NavigateAxis.Horizontal;

        [Header("多设备输入")]
        [Tooltip("玩家槽位：0 = 键盘/鼠标 + 第一个实体控制器；1 = 第二个方向盘/手柄；-1 = 任意玩家")]
        [SerializeField] private int playerSlot = 0;
        [Tooltip("激活时自动进入 UI 控制模式（选择角色等纯 UI 场景建议开启）")]
        [SerializeField] private bool autoEnableUIControl = true;
        [Tooltip("导航时是否移动 EventSystem 焦点。多个选择框（多个方向盘）同时操作时建议关闭，避免互相抢焦点")]
        [SerializeField] private bool setEventSystemFocus = true;
        [Tooltip("鼠标悬停到选项时是否切换选中项")]
        [SerializeField] private bool mouseSelectOnHover = true;
        [Tooltip("鼠标点击已选中的选项时是否确认（若 Button 的 onClick 已包含确认逻辑，请关闭此项避免重复确认）")]
        [SerializeField] private bool confirmOnSecondClick = true;
        [Tooltip("与其它导航目标冲突时的优先级，越大越先收到输入")]
        [SerializeField] private int inputPriority = 0;

        private int currentIndex = 0;

        // 矩阵行布局缓存（由 rowSizes 或 columns 推导），避免每次按键重复分配
        private int[] cachedRowTable;
        private int cachedOptionsLength = -1;
        private int[] cachedRowSizesRef;
        private int cachedColumns;

        /// <summary>上一次显示的预览索引（-1 表示尚未显示过，用于只切换变化的预览对象）</summary>
        private int lastPreviewIndex = -1;

        /// <summary>上一次高亮的选项索引（-1 表示尚未 Refresh 过）</summary>
        private int lastHighlightedIndex = -1;

        /// <summary>UIInputSystem 就绪前是否已请求订阅（防止初始化时序问题）</summary>
        private bool bindRequested;

        /// <summary>是否已注册到 UIInputSystem</summary>
        private bool registered;

        /// <summary>上次绑定时的 UIInputSystem 初始化代数（Clear/Init 循环后重新注册）</summary>
        private int lastInitVersion = -1;

        /// <summary>选择组内是否为当前活动选择框（非组模式恒为 true）</summary>
        private bool groupActive = true;

        /// <summary>是否接收输入（选择组会关闭组内选择框的独立输入）</summary>
        private bool inputEnabled = true;

        private readonly List<Button> pointerButtons = new List<Button>(8);
        private readonly List<UnityAction> pointerActions = new List<UnityAction>(8);
        private readonly List<CarouselPointerBridge> pointerBridges = new List<CarouselPointerBridge>(8);

        /// <summary>所属选择组（鼠标操作时同步活动选择框）</summary>
        private CarouselSelectionGroup ownerGroup;

        /// <summary>当前选中的索引（外部可读）</summary>
        public int CurrentIndex => currentIndex;

        /// <summary>玩家槽位（可运行时修改）</summary>
        public int PlayerSlot { get => playerSlot; set => playerSlot = value; }

        /// <summary>是否请求窗口焦点域自动进入 UI 控制模式（仅作创建焦点域时的提示）。</summary>
        public bool AutoEnableUIControl => autoEnableUIControl;

        /// <summary>是否启用独立输入（选择组会关闭组内选择框的独立输入）</summary>
        public bool InputEnabled { get => inputEnabled; private set => inputEnabled = value; }

        /// <summary>选择组内是否为当前活动选择框</summary>
        public bool GroupActive => groupActive;

        /// <summary>矩阵模式（上下左右都用于选择项，而不是切换选择框）</summary>
        public bool IsMatrixNavigation => columns > 1;

        /// <summary>线性横向模式（左右选项，上下可留给选择组切换）</summary>
        public bool IsHorizontalNavigation => columns <= 1 && navigateAxis == NavigateAxis.Horizontal;

        /// <summary>线性纵向模式（上下选项，左右可留给选择组切换）</summary>
        public bool IsVerticalNavigation => columns <= 1 && navigateAxis == NavigateAxis.Vertical;

        /// <summary>确认选择事件（参数：选中项索引），外部订阅即可拿到选择结果</summary>
        public event Action<int> onConfirmed;

        /// <summary>选项变化事件（参数：新索引）</summary>
        public event Action<int> onSelectionChanged;

        /// <summary>取消事件（关闭窗口前触发）</summary>
        public event Action onCancelled;

        [Header("关闭行为")]
        [Tooltip("确认后是否自动关闭窗口（默认 true；多个选择框时建议关闭，由外部统一处理）")]
        [SerializeField] private bool closeOnConfirm = true;
        [Tooltip("取消时是否自动关闭窗口/子面板")]
        [SerializeField] private bool closeOnCancel = true;
        [Tooltip("确认/取消时关闭的对象（可指向子面板根节点）；留空则关闭所属 UIController 窗口")]
        [SerializeField] private GameObject closeTarget;

        public bool CloseOnConfirm { get => closeOnConfirm; set => closeOnConfirm = value; }

        public bool CloseOnCancel { get => closeOnCancel; set => closeOnCancel = value; }

        /// <summary>子菜单面板：确认/取消时只关闭该对象，而不是整个父窗口。</summary>
        public GameObject CloseTarget { get => closeTarget; set => closeTarget = value; }

        private void OnEnable()
        {
            // UIInputSystem 可能在启动早期尚未初始化，等它就绪后再注册
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
            if (!inputSystem.isInited) return;

            // UIInputSystem 初始化前激活，或引擎 Clear/Init 循环后，重新注册到当前焦点域
            int version = inputSystem.InitVersion;
            if (bindRequested || lastInitVersion != version)
            {
                lastInitVersion = version;
                Bind();
            }
        }

        /// <summary>注册导航目标并初始化显示（窗口激活时自动调用，不依赖 UISystem.OpenWindow）。</summary>
        private void Bind()
        {
            // 总是先清理上一次的指针/注册状态，组内模式也不会重复添加 onClick
            Unbind();

            if (!UIInputSystem.Instance.isInited)
            {
                bindRequested = true;
                return;
            }

            bindRequested = false;

            // 组内选择框由 CarouselSelectionGroup 统一注册；组外才直接注册到窗口焦点域
            if (ownerGroup == null)
            {
                UIInputSystem.Instance.RegisterNavigationTarget(this, autoEnableUIControl);
                registered = true;
            }
            else
            {
                registered = false;
            }

            ValidateFields();
            SetupPointer();
            InitializeSelection();
        }

        private void Unbind()
        {
            bindRequested = false;

            if (registered)
            {
                UIInputSystem.Instance.UnregisterNavigationTarget(this);
                registered = false;
            }

            RemovePointer();
        }

        private void InitializeSelection()
        {
            if (options == null || options.Length == 0)
            {
                Refresh();
                return;
            }

            currentIndex = Mathf.Clamp(currentIndex, 0, options.Length - 1);
            NormalizeCurrentIndex();
            FocusCurrent();
            Refresh();
        }

        /// <summary>把当前索引收敛到最近的可导航项，避免 options 变化后停在空位/禁用项上。</summary>
        private void NormalizeCurrentIndex()
        {
            if (options == null || options.Length == 0)
            {
                currentIndex = 0;
                return;
            }

            currentIndex = Mathf.Clamp(currentIndex, 0, options.Length - 1);
            if (IsNavigable(currentIndex)) return;

            int nearest = FindNearestInteractableIndex(currentIndex);
            if (nearest >= 0)
            {
                currentIndex = nearest;
            }
        }

        /// <summary>
        /// 校验配置（仅告警，不阻断运行）：options 空位、previews 长度不一致。
        /// </summary>
        private void ValidateFields()
        {
            if (options == null || options.Length == 0)
            {
                Log.Warning("[CarouselSelection] options 为空，请至少配置一个选项。");
                return;
            }

            for (int i = 0; i < options.Length; i++)
            {
                if (options[i] == null)
                {
                    Log.Warning($"[CarouselSelection] options[{i}] 为空，请检查配置。");
                }
            }

            if (previews != null && previews.Length != options.Length)
            {
                Log.Warning($"[CarouselSelection] previews 长度({previews.Length})与 options 长度({options.Length})不一致，" +
                            "多余的预览对象永远不会被隐藏，缺失的选项没有预览。");
            }

            if (rowSizes != null && rowSizes.Length > 0)
            {
                int total = 0;
                for (int i = 0; i < rowSizes.Length; i++) total += Mathf.Max(0, rowSizes[i]);
                if (total != options.Length)
                {
                    Log.Warning($"[CarouselSelection] rowSizes 总和({total})与 options 数量({options.Length})不一致，" +
                                "矩阵导航将回退为按 columns 均匀分列。");
                }
            }
        }

        /// <summary>
        /// Inspector 右键菜单：打印每个选项的索引、屏幕坐标与矩阵行列，
        /// 用来核对“视觉位置”和 columns/rowSizes 是否一致。
        /// </summary>
        [ContextMenu("打印布局索引")]
        private void LogLayoutIndices()
        {
            if (options == null || options.Length == 0)
            {
                Log.Warning("[CarouselSelection] options 为空，无法打印布局。");
                return;
            }

            string rowSizeText = rowSizes == null || rowSizes.Length == 0
                ? "空"
                : string.Join(",", rowSizes);
            Log.Info("[CarouselSelection] 布局检查：options={0}，columns={1}，rowSizes={2}",
                options.Length, columns, rowSizeText);

            if (columns > 1)
            {
                var table = BuildRowTable();
                int index = 0;
                for (int row = 0; row < table.Length; row++)
                {
                    for (int col = 0; col < table[row] && index < options.Length; col++, index++)
                    {
                        LogOptionIndex(index, row, col);
                    }
                }

                if (index < options.Length)
                {
                    Log.Warning("[CarouselSelection] 行表只覆盖 {0}/{1} 个选项，请检查 rowSizes 总和。",
                        index, options.Length);
                }
            }
            else
            {
                for (int i = 0; i < options.Length; i++)
                {
                    LogOptionIndex(i, i, 0);
                }
            }

            LogSuggestedRowSizes();
        }

        /// <summary>按按钮实际坐标推导行数，提示应填写的 Row Sizes（仅矩阵模式）。</summary>
        private void LogSuggestedRowSizes()
        {
            if (columns <= 1 || options == null || options.Length == 0) return;

            var rowYs = new List<float>();
            var rowIndices = new List<List<int>>();
            const float tolerance = 1f;

            for (int i = 0; i < options.Length; i++)
            {
                var button = options[i];
                var rect = button != null ? button.transform as RectTransform : null;
                if (rect == null) continue;

                float y = rect.position.y;
                int row = -1;
                for (int r = 0; r < rowYs.Count; r++)
                {
                    if (Mathf.Abs(rowYs[r] - y) <= tolerance)
                    {
                        row = r;
                        break;
                    }
                }

                if (row < 0)
                {
                    row = rowYs.Count;
                    rowYs.Add(y);
                    rowIndices.Add(new List<int>());
                }
                rowIndices[row].Add(i);
            }

            if (rowYs.Count <= 1) return;

            var rowOrder = new List<int>();
            for (int r = 0; r < rowYs.Count; r++) rowOrder.Add(r);
            rowOrder.Sort((a, b) => rowYs[b].CompareTo(rowYs[a]));

            var ordered = new List<int>();
            var counts = new List<int>();
            for (int i = 0; i < rowOrder.Count; i++)
            {
                var indices = rowIndices[rowOrder[i]];
                indices.Sort((a, b) => GetOptionWorldX(a).CompareTo(GetOptionWorldX(b)));
                counts.Add(indices.Count);
                ordered.AddRange(indices);
            }

            bool matches = ordered.Count == options.Length;
            for (int i = 0; matches && i < ordered.Count; i++)
            {
                if (ordered[i] != i) matches = false;
            }

            string countsText = string.Join(",", counts);
            if (matches)
            {
                Log.Info($"[CarouselSelection] 按坐标检测到 {rowYs.Count} 行，Options 顺序已经是行优先。建议 Row Sizes = {countsText}");
            }
            else
            {
                Log.Warning($"[CarouselSelection] 按坐标的行优先顺序是 [{string.Join(",", ordered)}]，与当前 Options 顺序不一致；" +
                            $"请先重排 Options，再填 Row Sizes = {countsText}");
            }
        }

        private float GetOptionWorldX(int index)
        {
            var button = options[index];
            var rect = button != null ? button.transform as RectTransform : null;
            return rect != null ? rect.position.x : 0f;
        }

        private void LogOptionIndex(int index, int row, int col)
        {
            var button = options[index];
            string name = button != null ? button.name : "<null>";
            var rect = button != null ? button.transform as RectTransform : null;
            string position = rect != null
                ? $"({rect.anchoredPosition.x:0},{rect.anchoredPosition.y:0})"
                : "";

            Log.Info("[CarouselSelection] [{0}] {1} {2} -> row {3}, col {4}",
                index, name, position, row, col);
        }

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

        #region 鼠标

        private void SetupPointer()
        {
            if (options == null) return;

            for (int i = 0; i < options.Length; i++)
            {
                var btn = options[i];
                if (btn == null) continue;

                int index = i;
                var action = new UnityAction(() => OnOptionClicked(index));
                btn.onClick.AddListener(action);
                pointerButtons.Add(btn);
                pointerActions.Add(action);

                var bridge = btn.GetComponent<CarouselPointerBridge>();
                if (bridge == null)
                {
                    bridge = btn.gameObject.AddComponent<CarouselPointerBridge>();
                }
                bridge.Owner = this;
                bridge.Index = i;
                pointerBridges.Add(bridge);
            }
        }

        private void RemovePointer()
        {
            // 按注册时保存的 Button 引用精确移除，运行时替换 options 也不会残留旧 listener
            for (int i = 0; i < pointerButtons.Count; i++)
            {
                var button = pointerButtons[i];
                var action = i < pointerActions.Count ? pointerActions[i] : null;
                if (button != null && action != null)
                {
                    button.onClick.RemoveListener(action);
                }
            }
            pointerButtons.Clear();
            pointerActions.Clear();

            for (int i = 0; i < pointerBridges.Count; i++)
            {
                if (pointerBridges[i] != null)
                {
                    pointerBridges[i].Owner = null;
                }
            }
            pointerBridges.Clear();
        }

        internal void OnPointerEnterOption(int index)
        {
            // 鼠标不受选择组的 inputEnabled 限制：指针直接指向某个选择框，
            // 组模式下也应能悬停/点击任意选择框。
            if (!isActiveAndEnabled || !mouseSelectOnHover) return;
            if (!IsNavigable(index)) return;

            if (ownerGroup != null) ownerGroup.NotifyPointerFocus(this);
            if (index != currentIndex)
            {
                SetIndex(index);
            }
        }

        internal void OnOptionClicked(int index)
        {
            if (!isActiveAndEnabled || !IsNavigable(index)) return;

            if (ownerGroup != null) ownerGroup.NotifyPointerFocus(this);

            if (index != currentIndex)
            {
                SetIndex(index);
                return;
            }

            if (confirmOnSecondClick)
            {
                Confirm();
            }
        }

        internal void OnPointerScroll(float delta)
        {
            if (!isActiveAndEnabled || Mathf.Abs(delta) < 0.01f) return;
            if (ownerGroup != null) ownerGroup.NotifyPointerFocus(this);
            Move(delta > 0f ? -1 : 1);
        }

        #endregion

        /// <summary>确认当前选择（可被选项 Button 的 onClick 复用）</summary>
        public void Confirm()
        {
            if (options == null || options.Length == 0) return;

            int indexBeforeNormalize = currentIndex;
            NormalizeCurrentIndex();
            if (indexBeforeNormalize != currentIndex)
            {
                FocusCurrent();
                Refresh();
            }

            var selected = options[currentIndex];
            if (selected == null || !selected.interactable)
            {
                Log.Warning($"[CarouselSelection] options[{currentIndex}] 为空或不可交互，无法确认。");
                return;
            }
            Log.Debug($"[CarouselSelection] 确认选择：{selected.name} (index={currentIndex})");

            try { onConfirmed?.Invoke(currentIndex); }
            catch (Exception ex) { Log.Warning($"[CarouselSelection] onConfirmed 订阅者异常: {ex.Message}"); }

            if (closeOnConfirm)
            {
                CloseOwningWindow();
            }
        }
    }
}
