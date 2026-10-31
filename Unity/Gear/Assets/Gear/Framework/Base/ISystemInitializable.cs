using Cysharp.Threading.Tasks;
using System.Threading;

namespace Gear.Framework.Runtime.Base
{
    /// <summary>
    /// 可初始化系统 —— 仅需 Init 的轻量接口。
    /// 遵循接口隔离原则 (ISP)：不强制实现 Update 和 Clear。
    /// </summary>
    public interface ISystemInitializable
    {
        /// <summary>初始化进度（0~100）</summary>
        double InitProgress { get; }

        /// <summary>初始化（UniTask 零 GC 异步）</summary>
        UniTask Init();
    }

    /// <summary>
    /// 可取消初始化（可选实现）：模块 Init 内部含耗时 await 时实现本接口，
    /// 引擎在启动超时/Dispose 时会取消该 token，让模块自己中止资源加载，
    /// 而不是只停止等待、把异步续体留在后台继续写状态。
    /// <para>
    /// 未实现本接口的模块仍走 <see cref="ISystemInitializable.Init"/> + 外部取消等待，
    /// 由模块自己的代次校验兜底。
    /// </para>
    /// </summary>
    public interface ICustomSystemCancellableInit
    {
        /// <summary>可取消的初始化；token 触发时应尽快返回（抛 OperationCanceledException 或直接 return）</summary>
        UniTask Init(CancellationToken ct);
    }
}
