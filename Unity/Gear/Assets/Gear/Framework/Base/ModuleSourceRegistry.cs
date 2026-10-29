using System;
using System.Collections.Generic;

namespace Gear.Framework.Runtime.Base
{
    /// <summary>
    /// 模块来源 —— 让其他程序集把自己的模块贡献给 <c>ModulePresets</c>，
    /// 从而避免 <c>Framework/Runtime</c> 反向引用 <c>Framework/Modules</c>。
    /// <para>
    /// 实现方应在程序集加载/进入 Play 时调用 <see cref="ModuleSourceRegistry.Register"/>；
    /// <see cref="GetModules"/> 在 <c>StartGame.CreateModules()</c> 期间被调用一次（主线程）。
    /// </para>
    /// </summary>
    public interface IModuleSource
    {
        /// <summary>返回本程序集贡献的模块实例（不要在这里做耗时初始化）。</summary>
        IEnumerable<ICustomSystem> GetModules();
    }

    /// <summary>
    /// 模块来源注册表。刻意保持无 Unity 依赖、无日志依赖（Base 不能引用 Common），
    /// 贡献方抛异常时跳过该来源，不让一个可选模块拖垮启动。
    /// <para>只在主线程使用；重复注册同一类型时后注册的实例覆盖先前的。</para>
    /// </summary>
    public static class ModuleSourceRegistry
    {
        private static readonly List<IModuleSource> Sources = new List<IModuleSource>();

        /// <summary>当前注册的来源数量（测试/诊断用）。</summary>
        public static int Count => Sources.Count;

        /// <summary>注册一个来源；同类型重复注册时替换旧实例（兼容关闭 Domain Reload 的重复初始化）。</summary>
        public static void Register(IModuleSource source)
        {
            if (source == null) return;

            for (int i = 0; i < Sources.Count; i++)
            {
                if (Sources[i].GetType() == source.GetType())
                {
                    Sources[i] = source;
                    return;
                }
            }

            Sources.Add(source);
        }

        /// <summary>注销来源；只移除同一类型的条目。</summary>
        public static void Unregister(IModuleSource source)
        {
            if (source == null) return;

            for (int i = 0; i < Sources.Count; i++)
            {
                if (Sources[i].GetType() == source.GetType())
                {
                    Sources.RemoveAt(i);
                    return;
                }
            }
        }

        /// <summary>清空全部来源（测试用）。</summary>
        public static void Reset()
        {
            Sources.Clear();
        }

        /// <summary>把所有来源贡献的模块追加到 <paramref name="target"/>，返回追加数量。</summary>
        public static int Collect(IList<ICustomSystem> target)
        {
            if (target == null) return 0;

            int added = 0;
            for (int i = 0; i < Sources.Count; i++)
            {
                IEnumerable<ICustomSystem> modules;
                try
                {
                    modules = Sources[i].GetModules();
                }
                catch (Exception)
                {
                    // 可选模块的提供方异常不应阻断启动；Base 无日志依赖，故静默跳过。
                    continue;
                }

                if (modules == null) continue;
                foreach (ICustomSystem module in modules)
                {
                    if (module == null) continue;
                    target.Add(module);
                    added++;
                }
            }

            return added;
        }
    }
}
