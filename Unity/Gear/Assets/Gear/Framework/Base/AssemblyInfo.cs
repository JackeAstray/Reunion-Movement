using System.Runtime.CompilerServices;

// Runtime / Modules 程序集需要访问 Base 的少量 internal 成员。
// 历史 ModuleRuntime 已删除；这里的 InternalsVisibleTo 保留给其他内部类型。
[assembly: InternalsVisibleTo("Gear.Framework.Runtime")]
[assembly: InternalsVisibleTo("Gear.Framework.Modules")]
