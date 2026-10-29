using System;

namespace Gear.Framework.Runtime.UI
{
    /// <summary>
    /// 窗口元数据 —— 挂在 <see cref="UIController"/> 派生类上，声明这个窗口"是什么"。
    ///
    /// <para>
    /// 之前窗口的身份靠三处命名巧合（资源名 = 类名 = 预制体名），层级/是否池化散落在预制体的
    /// <see cref="UIWindowAsset"/> 组件上，运行时既不可枚举也不可校验。加上这个特性之后：
    /// <list type="bullet">
    /// <item>身份由 <see cref="Id"/> 明确声明，改类名不会再悄悄打不开；</item>
    /// <item>层级、是否模态、是否池化变成编译期可见的声明，由 <see cref="UIWindowCatalog"/> 收集；</item>
    /// <item>不写特性的老窗口保持原行为（Id 回退类名、层级读预制体、不池化）。</item>
    /// </list>
    /// </para>
    ///
    /// <code>
    /// [UIWindow("ShopUI", Layer = PanelType.NormalUI, Pooled = true)]
    /// public sealed class ShopUI : UIController { }
    /// </code>
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class UIWindowAttribute : Attribute
    {
        /// <summary>窗口标识（同时是资源名：<c>Resources/Prefabs/UIs/{Id}.prefab</c>）。</summary>
        public string Id { get; }

        /// <summary>层级。默认 <see cref="PanelType.NormalUI"/>，与既有窗口一致。</summary>
        public PanelType Layer { get; set; } = PanelType.NormalUI;

        /// <summary>是否模态窗口（打开后压栈，返回键优先关它）。默认 false。</summary>
        public bool Modal { get; set; }

        /// <summary>是否允许对象池复用（关闭后进池而不是销毁）。默认 false。</summary>
        public bool Pooled { get; set; }

        /// <param name="id">窗口标识；不传则回退到类名</param>
        public UIWindowAttribute(string id = null)
        {
            Id = id;
        }
    }
}
