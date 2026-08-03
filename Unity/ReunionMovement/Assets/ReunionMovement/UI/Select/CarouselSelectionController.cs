using System;
using R3;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using ReunionMovement.Core.UI;
using ReunionMovement.Core.UIInput;

namespace ReunionMovement.UI.Select
{
    /// <summary>
    /// 轮播式选择控制器 —— 适用于"选择角色/车辆"等界面。
    /// 左右键/手柄左摇杆切换选项，Enter/手柄A 确认，Esc/手柄B 关闭。
    ///
    /// 使用说明：
    /// 1. 挂到选择窗口根节点（继承 UIController）。
    /// 2. 将每个选项的 Image/Button 填入 options（若用 Button，请把其 Navigation 设为 None，
    ///    避免与 NavigateSubject 双重响应；确认逻辑写在选项 Button 的 onClick 或本类 Confirm 中）。
    /// 3. previews 为按选项索引显示的角色模型/立绘（可空）。
    /// 4. 在 UIController.firstSelected 中填入任一选项，窗口打开时会被自动聚焦。
    ///
    /// 注意：输入订阅在 OnEnable/OnDisable 中建立/释放，不依赖 UISystem.OpenWindow，
    /// 场景中直接激活窗口同样生效。
    /// </summary>
    public class CarouselSelectionController : UIController
    {
        [Header("选项列表（按索引顺序）")]
        [SerializeField] private Button[] options;

        [Header("预览对象（与选项一一对应，切换时显示当前项）")]
        [SerializeField] private GameObject[] previews;

        [Header("普通高亮色 / 当前选中高亮色")]
        [SerializeField] private Color normalColor = new Color(0.8f, 0.8f, 0.8f, 1f);
        [SerializeField] private Color selectedColor = Color.white;

        private int currentIndex = 0;
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
            submitSub = UIInputSystem.Instance.SubmitSubject.Subscribe(_ => Confirm());
            cancelSub = UIInputSystem.Instance.CancelSubject.Subscribe(_ => CloseWindow());

            // 聚焦当前项（默认第一项）
            if (options != null && options.Length > 0)
            {
                currentIndex = Mathf.Clamp(currentIndex, 0, options.Length - 1);
                FocusCurrent();
            }
            Refresh();
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
            // 只响应横向（左右键 / 手柄左摇杆 X 轴）；如需纵向改为 dir.y
            if (Mathf.Abs(dir.x) < 0.5f) return;
            Move(dir.x > 0 ? 1 : -1);
        }

        /// <summary>按步长移动（正=下一项，负=上一项），循环</summary>
        public void Move(int step)
        {
            if (options == null || options.Length == 0) return;
            SetIndex((currentIndex + step + options.Length) % options.Length);
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
            if (selectable == null || !selectable.interactable) return;
            if (EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(selectable.gameObject);
            }
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
                // 切换预览（角色模型/立绘）
                if (previews != null && i < previews.Length && previews[i] != null)
                {
                    previews[i].SetActive(i == currentIndex);
                }
            }
        }

        /// <summary>确认当前选择（可被选项 Button 的 onClick 复用）</summary>
        public void Confirm()
        {
            if (options == null || options.Length == 0) return;
            var selected = options[currentIndex];
            Debug.Log($"[CarouselSelection] 确认选择：{selected.name} (index={currentIndex})");

            // TODO: 通知数据层 / 打开确认弹窗，然后 CloseWindow();
        }
    }
}
