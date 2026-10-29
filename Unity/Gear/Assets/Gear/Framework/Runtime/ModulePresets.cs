using Gear.Framework.Modules;
using Gear.Utilities;
using Gear.Framework.Modules.Http;
using Gear.Framework.Modules.Timer;
using Gear.Framework.Runtime.Base;
using Gear.Framework.Runtime.EventMessage;
using Gear.Framework.Runtime.Languages;
using Gear.Framework.Runtime.Pause;
using Gear.Framework.Runtime.Resources;
using Gear.Framework.Runtime.Scene;
using Gear.Framework.Runtime.Sound;
using Gear.Framework.Runtime.Terminal;
using Gear.Framework.Runtime.UI;
using Gear.Framework.Runtime.UIInput;
using Gear.Framework.Runtime.UIToolkit;
using System;
using System.Collections.Generic;

namespace Gear.Framework.Runtime
{
    /// <summary>
    /// 模块清单预设 —— 把"这次启动注册哪些模块"从背依赖表变成选一行。
    ///
    /// <para>
    /// 三个入口覆盖接入时的常见起点：
    /// <list type="bullet">
    /// <item><see cref="Full"/>：14 个模块，与改造前的 <c>StartGame.CreateModules()</c> 完全一致；</item>
    /// <item><see cref="Minimal"/>：资源 + Addressables + 场景 + UI，够跑起一个界面；</item>
    /// <item><see cref="Without{T}"/>：从任意清单里去掉某类模块，缺的依赖由
    ///       <see cref="ModuleCatalog.ExpandDependencies"/> 在启动时自动补回。</item>
    /// </list>
    /// </para>
    ///
    /// <code>
    /// // 全量（默认）
    /// public override IList&lt;ICustomSystem&gt; CreateModules() =&gt; ModulePresets.Full();
    ///
    /// // 只要最小集，再去掉终端和网络
    /// public override IList&lt;ICustomSystem&gt; CreateModules()
    ///     =&gt; ModulePresets.Minimal().Without&lt;TerminalSystem&gt;().Without&lt;NetworkMgr&gt;();
    /// </code>
    ///
    /// <para>
    /// 注意：预设返回的是<b>新的 List</b>，可以随意增删，不影响后续调用；
    /// 但里面装的是各模块的<b>单例实例</b>，同一类型不要重复注册（<see cref="ModuleCatalog"/> 会报重复）。
    /// </para>
    /// </summary>
    public static class ModulePresets
    {
        /// <summary>
        /// 全部 14 个模块 = <see cref="Minimal"/> 的 4 个 + 另外 10 个。
        /// 清单顺序不代表初始化顺序：实际顺序由各模块的 <c>[ModuleOrder]</c> 拓扑排序决定。
        /// </summary>
        public static IList<ICustomSystem> Full()
        {
            var modules = Minimal();

            modules.Add(EventMessageSystem.Instance);  // 40  事件总线
            modules.Add(LanguagesSystem.Instance);     // 50  多语言
            modules.Add(PauseSystem.Instance);         // 60  暂停管理（Time.timeScale 统一切换）
            modules.Add(SoundSystem.Instance);         // 70  音频（需要 Update 驱动淡入淡出）
            modules.Add(TimerMgr.Instance);            // 80  计时器（需要 Update 驱动）
            modules.Add(UIInputSystem.Instance);       // 100 UI 输入（需要 Update 驱动导航）
            modules.Add(UIToolkitSystem.Instance);     // 110 UI Toolkit
            modules.Add(TerminalSystem.Instance);      // 120 终端（需要 Update 检测按键）
            modules.Add(NetworkMgr.Instance);          // 130 网络通道管理（需要 Update 消费移除队列）
            modules.Add(HttpMgr.Instance);             // 140 HTTP 请求（需要 Update 轮询进度）

            return modules;
        }

        /// <summary>
        /// 最小可用集合：资源 + Addressables + 场景 + UI（4 个）。
        /// 够跑通"启动 → 打开一个窗口"；音频 / 网络 / 终端等按需用 <see cref="Of"/> 或直接 <c>Add</c> 加回。
        /// <para>这 4 个也是 <see cref="Full"/> 的基底，所以每个模块的单例入口在整个文件里只出现一次。</para>
        /// </summary>
        public static IList<ICustomSystem> Minimal()
        {
            return new List<ICustomSystem>(4)
            {
                ResourcesSystem.Instance,     // 10  资源加载（同步兜底，多个模块依赖它）
                AddressableSystem.Instance,   // 20  Addressables（异步 / 远程）
                SceneSystem.Instance,         // 30  场景管理
                UISystem.Instance,            // 90  UI 管理
            };
        }

        /// <summary>用给定模块构造清单（省掉 <c>new List&lt;ICustomSystem&gt;</c> 样板）。</summary>
        public static IList<ICustomSystem> Of(params ICustomSystem[] modules)
        {
            return new List<ICustomSystem>(modules ?? Array.Empty<ICustomSystem>());
        }

        /// <summary>
        /// 去掉清单里的某类模块（接口 / 基类也可以）。
        /// 返回新列表，不修改入参；被去掉模块的依赖会由启动流程自动补回。
        /// </summary>
        public static IList<ICustomSystem> Without<T>(this IList<ICustomSystem> modules) where T : class, ICustomSystem
        {
            var result = new List<ICustomSystem>();
            if (modules == null) return result;

            for (int i = 0; i < modules.Count; i++)
            {
                if (modules[i] is T) continue;
                result.Add(modules[i]);
            }
            return result;
        }
    }
}
