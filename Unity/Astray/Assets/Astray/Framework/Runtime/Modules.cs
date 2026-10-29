using Astray.Common;
using Astray.Framework.Runtime.Base;
using System;
using System.Collections.Generic;
using System.Text;

namespace Astray.Framework.Runtime
{
    /// <summary>
    /// 统一的模块访问入口 —— 只暴露"本次启动真正注册过"的模块。
    ///
    /// <para>
    /// 与 <c>XxxSystem.Instance</c> 的区别：<c>Instance</c> 是 <c>Lazy&lt;T&gt;</c> 自建实例，
    /// 即使模块没被 <c>CreateModules()</c> 注册也能拿到一个"看起来可用但没人 Init/Update"的对象；
    /// 而 <see cref="Get{T}"/> 只返回引擎持有的实例，取不到时直接抛出带清单的异常。
    /// </para>
    ///
    /// <code>
    /// var ui = Modules.Get&lt;UISystem&gt;();
    /// if (Modules.TryGet&lt;SoundSystem&gt;(out var sound)) { ... }
    /// </code>
    /// </summary>
    public static class Modules
    {
        /// <summary>当前引擎持有的模块列表（引擎未启动时为空集合，不返回 null）。</summary>
        public static IList<ICustomSystem> All
        {
            get
            {
                var engine = GameEngine.Current;
                if (engine == null || engine.Modules == null)
                {
                    return Array.Empty<ICustomSystem>();
                }
                return engine.Modules;
            }
        }

        /// <summary>
        /// 取一个已注册的模块。未注册时抛出 <see cref="InvalidOperationException"/>，
        /// 异常信息里会列出当前注册的全部模块，便于定位"忘了注册"。
        /// </summary>
        public static T Get<T>() where T : class, ICustomSystem
        {
            if (TryGet<T>(out var module)) return module;

            var engine = GameEngine.Current;
            if (engine == null)
            {
                throw new InvalidOperationException(
                    $"[Modules] 取 {typeof(T).Name} 时引擎尚未启动。检查是否在 Bootstrap 之前调用了它，或改用入口的 OnBeforeInitAsync。");
            }

            throw new InvalidOperationException(
                $"[Modules] {typeof(T).Name} 未注册。当前已注册: {DescribeAll()}。请在 CreateModules() 中注册它。");
        }

        /// <summary>尝试取一个已注册的模块。</summary>
        public static bool TryGet<T>(out T module) where T : class, ICustomSystem
        {
            var all = All;
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i] is T match)
                {
                    module = match;
                    return true;
                }
            }
            module = null;
            return false;
        }

        /// <summary>是否已注册某模块。</summary>
        public static bool Contains<T>() where T : class, ICustomSystem
        {
            return TryGet<T>(out _);
        }

        /// <summary>已注册模块的简要清单，用于日志与异常信息。</summary>
        public static string DescribeAll()
        {
            var all = All;
            if (all.Count == 0) return "(空)";

            var sb = new StringBuilder();
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i] == null) continue;
                if (sb.Length > 0) sb.Append(", ");
                sb.Append(all[i].GetType().Name);
            }
            return sb.Length == 0 ? "(空)" : sb.ToString();
        }
    }
}
