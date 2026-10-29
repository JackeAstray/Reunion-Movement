using System;

namespace ReunionMovement.Framework.Runtime.Base
{
    /// <summary>
    /// 声明本程序集使用的游戏入口（<see cref="ReunionMovement.Framework.Runtime.GameEntry"/> 子类）。
    /// <para>
    /// 用法：在<b>你自己的程序集</b>里写一行（放在任意 .cs 文件的最外层，命名空间之外）：
    /// </para>
    /// <code>
    /// [assembly: ReunionGameEntry(typeof(MyGame.MyGameEntry))]
    /// </code>
    /// <para>
    /// Bootstrap 会在启动时扫描已加载程序集读取该声明，用它替代框架内置的 StartGame。
    /// 没有声明时使用内置默认入口，行为与旧版本一致。
    /// </para>
    /// <para>
    /// 注意：入口类型必须是 <c>GameEntry</c> 的具体子类，且带无参构造函数。
    /// 若同时存在多个声明，非框架程序集（名称不以 ReunionMovement. 开头）的声明优先。
    /// </para>
    /// </summary>
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false, Inherited = false)]
    public sealed class ReunionGameEntryAttribute : Attribute
    {
        /// <summary>入口类型</summary>
        public Type EntryType { get; }

        /// <param name="entryType">GameEntry 子类</param>
        public ReunionGameEntryAttribute(Type entryType)
        {
            EntryType = entryType ?? throw new ArgumentNullException(nameof(entryType));
        }
    }
}
