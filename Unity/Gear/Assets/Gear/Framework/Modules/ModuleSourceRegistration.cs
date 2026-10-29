using System.Collections.Generic;
using Gear.Framework.Modules.Http;
using Gear.Framework.Modules.Timer;
using Gear.Framework.Runtime.Base;
using UnityEngine;

namespace Gear.Framework.Modules
{
    /// <summary>
    /// <c>Framework/Modules</c> 贡献给 <c>ModulePresets</c> 的模块来源。
    /// 这样 Runtime 不需要引用 Modules 程序集，接入方删掉 Modules 时也不会启动失败。
    /// </summary>
    public sealed class ModulesModuleSource : IModuleSource
    {
        public IEnumerable<ICustomSystem> GetModules()
        {
            yield return TimerMgr.Instance;     // 80  计时器
            yield return NetworkMgr.Instance;   // 130 网络通道管理
            yield return HttpMgr.Instance;      // 140 HTTP 请求
        }
    }

    /// <summary>
    /// 把 <see cref="ModulesModuleSource"/> 注册到 <see cref="ModuleSourceRegistry"/>。
    /// <para>
    /// 注册只存一个来源对象，不创建任何 Unity 对象；真正的单例在 <c>CreateModules()</c> 时才取。
    /// Editor 里手动调用 <see cref="EnsureRegistered"/> 可以让 <c>ModulePresets.Full()</c> 立即包含这三个模块。
    /// </para>
    /// </summary>
    public static class ModuleSourceRegistration
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void RegisterOnPlay()
        {
            EnsureRegistered();
        }

        /// <summary>幂等注册（同类型重复注册会替换旧实例）。</summary>
        public static void EnsureRegistered()
        {
            ModuleSourceRegistry.Register(new ModulesModuleSource());
        }
    }
}
