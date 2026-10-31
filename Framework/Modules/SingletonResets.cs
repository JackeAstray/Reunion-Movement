using Gear.Framework.Modules.Download;
using Gear.Framework.Modules.Http;
using Gear.Framework.Modules.Timer;

using Gear.Common;
using Gear.Framework.Runtime.Base;
using Gear.Utilities;
using UnityEngine;

namespace Gear.Framework.Modules
{
    /// <summary>
    /// 单例的跨 Play 会话复位清单（MonoBehaviour 单例 + 纯 C# 的 TimerMgr / HttpMgr / NetworkMgr）。
    /// <para>
    /// 关闭 Domain Reload 时静态字段（<c>SingletonMgr&lt;T&gt;.instance</c> 与两个事件）不会自动清空，
    /// 会把上一会话的已销毁对象带进新会话。这里集中复位所有管理器，由本程序集的
    /// <c>[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]</c> 调用
    /// （Runtime 不再直接引用 Modules，也就不再负责这份清单）。
    /// </para>
    /// <para>
    /// <b>新增一个 <c>SingletonMgr&lt;T&gt;</c> 子类或纯 C# 单例时，请在这里补一行。</b>
    /// 放在本文件而不是 Bootstrap，是为了让这份清单和它所管理的类型待在同一个程序集里。
    /// </para>
    /// </summary>
    public static class SingletonResets
    {
        /// <summary>进入 Play 时的复位入口（Runtime 不引用本程序集）。</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            ResetAll();
        }

        /// <summary>复位所有 MonoBehaviour 单例的静态状态。</summary>
        public static void ResetAll()
        {
            ResolutionBridge.Reset();
            PerformanceStatsBridge.ClearCurrent();
            HttpMgr.ResetStatics();
            NetworkMgr.ResetStatics();
            TimerMgr.ResetStatics();
            DownloadMgr.ResetStatics();
            DeadlineMgr.ResetStatics();
            PerformanceMonitor.ResetStatics();
            ResolutionMgr.ResetStatics();
        }
    }
}
