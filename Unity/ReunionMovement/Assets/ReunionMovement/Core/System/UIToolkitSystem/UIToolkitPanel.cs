using UnityEngine;
using UnityEngine.UIElements;

namespace ReunionMovement.Core.UIToolkit
{
    /// <summary>
    /// UI Toolkit 面板基类 —— 每个基于 UI Toolkit 的界面继承此类。
    /// 类似于 UIController 之于 uGUI，负责管理面板的生命周期和 UI 元素绑定。
    /// 
    /// 使用方式：
    /// <code>
    /// public class MainMenuPanel : UIToolkitPanel
    /// {
    ///     private Button startButton;
    ///
    ///     protected override void OnBind()
    ///     {
    ///         startButton = Q&lt;Button&gt;("start-button");
    ///         startButton.clicked += () => Debug.Log("Start!");
    ///     }
    /// }
    ///
    /// // 打开面板：
    /// UIToolkitSystem.Instance.OpenPanel&lt;MainMenuPanel&gt;("MainMenu");
    /// </code>
    /// </summary>
    public class UIToolkitPanel
    {
        #region 属性

        /// <summary>面板名称（唯一标识，通常与 UXML 文件名一致）</summary>
        public string PanelName { get; private set; }

        /// <summary>面板的根 VisualElement（由 UXML 模板实例化而来）</summary>
        public VisualElement Root { get; private set; }

        /// <summary>所属的 UIToolkitSystem</summary>
        protected UIToolkitSystem System { get; private set; }

        /// <summary>是否已打开</summary>
        public bool IsOpen { get; private set; }

        #endregion

        #region 初始化（由 UIToolkitSystem 调用）

        /// <summary>
        /// 初始化面板 —— 克隆 UXML 模板并将根元素挂入父级。
        /// 由 UIToolkitSystem 内部调用，子类无需关心。
        /// </summary>
        public void Initialize(
            string name,
            VisualTreeAsset asset,
            VisualElement parent,
            UIToolkitSystem system)
        {
            PanelName = name;
            System = system;

            // 实例化 UXML 模板
            Root = asset.CloneTree();
            Root.name = name;

            // 默认隐藏，OnOpen 时再显示
            Root.style.display = DisplayStyle.None;

            // 添加到父级 VisualElement 树
            parent.Add(Root);

            // 调用子类绑定
            OnBind();
        }
        #endregion

        #region 生命周期（子类重写）

        /// <summary>
        /// 绑定 UI 元素 —— 子类在此处通过 Q&lt;T&gt; 查找元素并注册事件回调。
        /// 在 Initialize 末尾自动调用。
        /// </summary>
        protected virtual void OnBind() { }

        /// <summary>
        /// 打开面板时调用
        /// </summary>
        /// <param name="data">外部传入的数据（可选）</param>
        public virtual void OnOpen(object data = null)
        {
            IsOpen = true;
            Root.style.display = DisplayStyle.Flex;
        }

        /// <summary>
        /// 关闭面板时调用 —— 从 VisualElement 树中移除根元素
        /// </summary>
        public virtual void OnClose()
        {
            IsOpen = false;
            Root.RemoveFromHierarchy();
        }

        #endregion

        #region 实用方法

        /// <summary>
        /// 显示/隐藏面板（不触发 OnOpen/OnClose 生命周期）
        /// </summary>
        public void SetVisible(bool visible)
        {
            if (Root != null)
            {
                Root.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        /// <summary>
        /// 将面板置于最顶层（在父级 VisualElement 中排到最后）
        /// </summary>
        public void BringToFront()
        {
            if (Root != null)
            {
                Root.BringToFront();
            }
        }

        #endregion

        #region 便捷查询方法

        /// <summary>按名称查找 VisualElement 子元素</summary>
        protected T Q<T>(string name) where T : VisualElement
        {
            return Root?.Q<T>(name);
        }

        /// <summary>按 USS 类名查找 VisualElement 子元素</summary>
        protected T QByClass<T>(string className) where T : VisualElement
        {
            return Root?.Q<T>(className: className);
        }

        #endregion
    }
}
