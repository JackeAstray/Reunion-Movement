using Astray.Framework.Modules.Download;
using Astray.Framework.Modules.Http;
using Astray.Framework.Modules.Timer;

using Astray.Common;
using Astray.Utilities;
namespace Astray.Framework.Modules
{
    /// <summary>
    /// MonoBehaviour 单例的跨 Play 会话复位清单。
    /// <para>
    /// 关闭 Domain Reload 时静态字段（<c>SingletonMgr&lt;T&gt;.instance</c> 与两个事件）不会自动清空，
    /// 会把上一会话的已销毁对象带进新会话。这里集中复位所有管理器，由
    /// <c>Bootstrap.ResetStaticStateOnPlay()</c> 调用。
    /// </para>
    /// <para>
    /// <b>新增一个 <c>SingletonMgr&lt;T&gt;</c> 子类时，请在这里补一行。</b>
    /// 放在本文件而不是 Bootstrap，是为了让这份清单和它所管理的类型待在同一个程序集里。
    /// </para>
    /// </summary>
    public static class SingletonResets
    {
        /// <summary>复位所有 MonoBehaviour 单例的静态状态。</summary>
        public static void ResetAll()
        {
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
