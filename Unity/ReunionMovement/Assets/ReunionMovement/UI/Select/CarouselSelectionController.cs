using System;
using R3;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using ReunionMovement.Common;
using ReunionMovement.Core.UI;
using ReunionMovement.Core.UIInput;

namespace ReunionMovement.UI.Select
{
    /// <summary>
    /// 轮播式选择控制器 —— 适用于"选择角色/车辆"等界面。
    /// 方向键/手柄左摇杆切换选项：
    /// - columns &gt; 1：矩阵模式，上下左右同时响应（左右行内/行间切换，上下跨行，均环绕）
    ///   支持不规则行：rowSizes 配置每行个数（如 4,4,2），留空则按 columns 均匀分列（最后一行可不满）
    ///   支持空位（洞）：options 中为 null 或不可交互的项会被导航自动跳过
    /// - columns = 1：线性模式，按 navigateAxis 取横向或纵向
    /// Enter/手柄A 确认，Esc/手柄B 关闭。
    ///
    /// 使用说明：
    /// 1. 挂到选择窗口根节点（继承 UIController）。
    /// 2. 将每个选项的 Image/Button 填入 options（若用 Button，请把其 Navigation 设为 None，
    ///    避免与 NavigateSubject 双重响应；确认逻辑写在选项 Button 的 onClick 或本类 Confirm 中）。
    /// 3. previews 为按选项索引显示的角色模型/立绘（可空）。
    /// 4. 在 UIController.firstSelected 中填入任一选项，窗口打开时会被自动聚焦。
    /// 5. 矩阵模式：把 options 按行优先摆放；规则网格设 Columns 列数；不规则行再填 Row Sizes（每行个数，如 4,4,2）。
    ///
    /// 注意：输入订阅在 OnEnable/OnDisable 中建立/释放，不依赖 UISystem.OpenWindow，
    /// 场景中直接激活窗口同样生效。
    /// </summary>
    public class CarouselSelectionController : UIController
    {
        /// <summary>线性模式导航响应轴：横向（左右键/左摇杆 X）或纵向（上下键/左摇杆 Y）</summary>
        private enum NavigateAxis { Horizontal, Vertical }
        [Header("选项列表（按索引顺序）")]
        [SerializeField] private Button[] options;

        [Header("预览对象（与选项一一对应，切换时显示当前项）")]
        [SerializeField] private GameObject[] previews;

        [Header("普通高亮色 / 当前选中高亮色")]
        [SerializeField] private Color normalColor = new Color(0.8f, 0.8f, 0.8f, 1f);
        [SerializeField] private Color selectedColor = Color.white;

        [Header("交互")]
        [Tooltip("矩阵列数：>1 时启用矩阵模式（上下左右同时响应）；=1 时为线性模式（按 navigateAxis 取轴）")]
        [SerializeField] private int columns = 1;
        [Tooltip("矩阵模式每行的选项数（可选）：支持不规则行（如 4,4,2）。留空则按 Columns 均匀分列，最后一行允许不满")]
        [SerializeField] private int[] rowSizes;
        [Tooltip("线性模式响应导航的方向轴：横向（左右键/左摇杆 X）或纵向（上下键/左摇杆 Y）")]
        [SerializeField] private NavigateAxis navigateAxis = NavigateAxis.Horizontal;
        [Tooltip("导航响应阈值（手柄左摇杆死区，0~1）")]
        [SerializeField, Range(0f, 1f)] private float navigateDeadzone = 0.5f;

        private int currentIndex = 0;

        // 矩阵行布局缓存（由 rowSizes 或 columns 推导），避免每次按键重复分配
        private int[] cachedRowTable;
        private int cachedOptionsLength = -1;
        private int[] cachedRowSizesRef;
        private int cachedColumns;

        /// <summary>上一次显示的预览索引（-1 表示尚未显示过，用于只切换变化的预览对象）</summary>
        private int lastPreviewIndex = -1;
        private IDisposable navSub;
        private IDisposable submitSub;
        private IDisposable cancelSub;

        /// <summary>当前选中的索引（外部可读）</summary>
        public int CurrentIndex => currentIndex;

        /// <summary>UIInputSystem 就绪前是否已请求订阅（防止初始化时序问题）</summary>
        private bool bindRequested = false;

        private void OnEnable()
        {
            // UIInputSystem 可能在启动早期尚未初始化，等它就绪后再订阅
            if (!UIInputSystem.Instance.isInited)
            {
                bindRequested = true;
                return;
            }
            Bind();
        }

        private void OnDisable()
        {
            bindRequested = false;
            Unbind();
        }

        private void Update()
        {
            // 若窗口在 UIInputSystem 初始化前就激活，初始化完成后补订阅
            if (bindRequested && UIInputSystem.Instance.isInited)
            {
                bindRequested = false;
                Bind();
            }
        }

        /// <summary>
        /// 订阅导航/确认/取消并初始化显示（窗口激活时自动调用，
        /// 不依赖 UISystem.OpenWindow —— 场景中直接激活同样生效）
        /// </summary>
        private void Bind()
        {
            Unbind(); // 防重复订阅

            navSub = UIInputSystem.Instance.NavigateSubject.Subscribe(OnNavigate);
            // 全局 Subject 会被所有激活的轮播窗口收到：仅顶层窗口响应确认/取消，
            // 避免叠层窗口同时 Move/Confirm/CloseWindow 互相抢输入
            submitSub = UIInputSystem.Instance.SubmitSubject.Subscribe(_ => { if (IsTopmostWindow()) Confirm(); });
            cancelSub = UIInputSystem.Instance.CancelSubject.Subscribe(_ => { if (IsTopmostWindow()) CloseWindow(); });

            ValidateFields();

            // 聚焦当前项（默认第一项）
            if (options != null && options.Length > 0)
            {
                currentIndex = Mathf.Clamp(currentIndex, 0, options.Length - 1);
                FocusCurrent();
            }
            Refresh();
        }

        /// <summary>
        /// 校验配置（仅告警，不阻断运行）：options 空位、previews 长度不一致、
        /// Button 的 Navigation 未设为 None（会导致 EventSystem 导航与 Move 双重响应）。
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
                    continue;
                }
                if (options[i].navigation.mode != Navigation.Mode.None)
                {
                    Log.Warning($"[CarouselSelection] options[{i}] '{options[i].name}' 的 Navigation 未设为 None，" +
                                "方向键会同时触发 EventSystem 导航与本控制器 Move，造成双重响应。");
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

        private void Unbind()
        {
            navSub?.Dispose();
            submitSub?.Dispose();
            cancelSub?.Dispose();
            navSub = null;
            submitSub = null;
            cancelSub = null;
        }

        private void OnNavigate(Vector2 dir)
        {
            // 叠层窗口同时激活时，只有顶层窗口响应导航
            if (!IsTopmostWindow()) return;

            if (columns > 1)
            {
                // 矩阵模式：左右/上下同时响应（各轴独立判定死区）
                if (Mathf.Abs(dir.x) >= navigateDeadzone)
                {
                    MoveHorizontal(dir.x > 0 ? 1 : -1);
                }
                if (Mathf.Abs(dir.y) >= navigateDeadzone)
                {
                    MoveVertical(dir.y > 0 ? 1 : -1);
                }
                return;
            }

            // 线性模式：按配置的方向轴取输入
            if (navigateAxis == NavigateAxis.Horizontal)
            {
                if (Mathf.Abs(dir.x) < navigateDeadzone) return;
                Move(dir.x > 0 ? 1 : -1); // 右=下一项
            }
            else
            {
                if (Mathf.Abs(dir.y) < navigateDeadzone) return;
                Move(dir.y > 0 ? -1 : 1); // 上=上一项，下=下一项
            }
        }

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
            // 全部不可导航：保持原位
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
                // 目标行可能比当前列短（不规则行），列号收拢；空行兜底 0
                int c = Mathf.Max(0, Mathf.Min(col, table[r] - 1));
                int candidate = RowOffset(table, r) + c;
                if (candidate == currentIndex) break; // 整列扫描一圈无其他可导航项
                if (IsNavigable(candidate))
                {
                    SetIndex(candidate);
                    return;
                }
            }
            // 该列方向无可导航项：保持原位
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
                for (int i = 0; i < rowSizes.Length; i++)
                {
                    total += Mathf.Max(0, rowSizes[i]);
                }
                if (total == len)
                {
                    table = rowSizes;
                }
                else
                {
                    Log.Warning($"[CarouselSelection] rowSizes 总和({total})与 options 数量({len})不一致，已回退为按 columns={columns} 均匀分列。");
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

        /// <summary>
        /// 判断本窗口是否为当前最顶层打开窗口（同父节点下兄弟层级最高）。
        /// 不同父节点（如独立弹窗）不构成遮挡关系，视为可响应。
        /// </summary>
        private bool IsTopmostWindow()
        {
            if (!gameObject.activeInHierarchy) return false;

            var windows = UISystem.Instance?.GetAllOpenWindows();
            if (windows == null || windows.Count == 0) return true;

            Transform myTransform = transform;
            Transform myParent = myTransform.parent;
            for (int i = 0; i < windows.Count; i++)
            {
                var other = windows[i];
                if (other == null || other == this || !other.gameObject.activeInHierarchy) continue;
                // 只比较同父节点下的兄弟窗口（不同父节点不构成遮挡关系）
                if (other.transform.parent != myParent) continue;
                if (other.transform.GetSiblingIndex() > myTransform.GetSiblingIndex())
                {
                    return false;
                }
            }
            return true;
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
            // 全部不可导航：保持原位
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
            currentIndex = Mathf.Clamp(index, 0, options.Length - 1);

            // 焦点跟随到当前项（若使用 Button + Navigation，Enter 会触发该项 onClick）
            FocusCurrent();
            Refresh();
        }

        /// <summary>
        /// 设置当前选项为 EventSystem 焦点。
        /// 注意：不走 UIInputSystem.SetSelectedGameObject —— 该方法会压入焦点栈（用于弹窗
        /// 层级跳转）；轮播式窗口内部切换焦点不应压栈，否则焦点栈会无限增长，
        /// 触发"焦点栈深度异常"警告。直接设置 EventSystem 后，UIInputSystem.Update
        /// 会自动轮询同步 CurrentSelected 与 SelectionChangedSubject。
        /// </summary>
        private void FocusCurrent()
        {
            if (options == null || options.Length == 0) return;
            var selectable = options[currentIndex];
            if (selectable != null && !selectable.interactable)
            {
                // 当前项不可交互时，就近回退到最近的可交互项，避免焦点丢失
                selectable = FindNearestInteractable(currentIndex);
            }
            if (selectable == null) return;
            if (EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(selectable.gameObject);
            }
        }

        /// <summary>从 index 向两侧寻找最近的可交互选项（无则返回 null）</summary>
        private Button FindNearestInteractable(int index)
        {
            for (int dist = 1; dist < options.Length; dist++)
            {
                var next = options[(index + dist) % options.Length];
                if (next != null && next.interactable) return next;
                var prev = options[(index - dist + options.Length) % options.Length];
                if (prev != null && prev.interactable) return prev;
            }
            return null;
        }

        private void Refresh()
        {
            for (int i = 0; i < options.Length; i++)
            {
                // 高亮当前项
                if (options[i] != null && options[i].targetGraphic != null)
                {
                    options[i].targetGraphic.color = i == currentIndex ? selectedColor : normalColor;
                }
            }

            // 只切换变化的预览对象，避免大量 SetActive 触发 Canvas 重建
            if (previews == null || previews.Length == 0) return;

            if (lastPreviewIndex < 0)
            {
                // 首次刷新：先隐藏全部预览，确保只有当前项可见（兼容场景中手动激活了多个预览的情况）
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

        /// <summary>确认当前选择（可被选项 Button 的 onClick 复用）</summary>
        public void Confirm()
        {
            if (options == null || options.Length == 0) return;
            var selected = options[currentIndex];
            if (selected == null)
            {
                Log.Warning($"[CarouselSelection] options[{currentIndex}] 为空，无法确认。");
                return;
            }
            Log.Debug($"[CarouselSelection] 确认选择：{selected.name} (index={currentIndex})");

            // TODO: 通知数据层 / 打开确认弹窗，然后 CloseWindow();
        }
    }
}
