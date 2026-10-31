using System;

namespace Gear.Framework.Runtime.Base
{
    /// <summary>
    /// 声明模块的初始化顺序与依赖，由 <see cref="Gear.Framework.Runtime.ModuleCatalog"/> 在启动前校验。
    /// <para>
    /// 用法（加在模块类上，依赖项必须也在本次启动的模块列表里）：
    /// </para>
    /// <code>
    /// [ModuleOrder(20, typeof(ResourcesSystem))]
    /// public class AddressableSystem : ICustomSystem { ... }
    /// </code>
    /// <para>
    /// 语义：
    /// - <c>Order</c>：同一批次内排序，小的先执行；未声明时视为 0。
    /// - <c>DependsOn</c>：被依赖方的模块必须先初始化，否则启动日志会给出明确错误。
    /// </para>
    /// <para>
    /// 不声明本特性的模块保持注册顺序，因此既有工程不需要一次性补齐。
    /// </para>
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class ModuleOrderAttribute : Attribute
    {
        /// <summary>同批次排序值，小的先初始化。</summary>
        public int Order { get; }

        /// <summary>必须先于本模块初始化的模块类型。</summary>
        public Type[] DependsOn { get; }

        /// <param name="order">排序值，小的先初始化</param>
        /// <param name="dependsOn">必须先初始化的模块类型</param>
        public ModuleOrderAttribute(int order, params Type[] dependsOn)
        {
            Order = order;
            DependsOn = dependsOn ?? Type.EmptyTypes;
        }
    }
}
