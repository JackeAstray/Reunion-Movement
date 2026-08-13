using Cysharp.Threading.Tasks;
using R3;
using System.Threading;
using ReunionMovement.Common;

namespace ReunionMovement.Core.Resources
{
    /// <summary>
    /// Addressables 热更流程状态机 —— 封装"检查 Catalog 更新 → 下载 → 应用"标准流程。
    /// 状态与进度为 R3 可观测属性（UI 可直接绑定，配合热更下载界面）。
    /// 用法：
    ///   var flow = new AddressableUpdateFlow();
    ///   flow.State.Subscribe(s => { ... });
    ///   await flow.RunAsync();
    /// </summary>
    public class AddressableUpdateFlow
    {
        /// <summary>热更流程状态</summary>
        public enum FlowState
        {
            /// <summary>空闲（未开始）</summary>
            Idle,
            /// <summary>正在检查远端 Catalog</summary>
            Checking,
            /// <summary>正在下载更新内容</summary>
            Downloading,
            /// <summary>无可用更新</summary>
            UpToDate,
            /// <summary>更新完成（可进入游戏）</summary>
            Completed,
            /// <summary>更新失败（调用方可降级/重试）</summary>
            Failed,
        }

        /// <summary>当前流程状态（可观测）</summary>
        public ReactiveProperty<FlowState> State { get; } = new ReactiveProperty<FlowState>(FlowState.Idle);

        /// <summary>下载进度 0~1（可观测）</summary>
        public ReactiveProperty<float> Progress { get; } = new ReactiveProperty<float>(0f);

        /// <summary>最近一次运行的检查结果（供诊断）</summary>
        public AddressableUpdateResult LastResult { get; private set; }

        /// <summary>是否正在运行</summary>
        public bool IsRunning => State.Value == FlowState.Checking || State.Value == FlowState.Downloading;

        /// <summary>
        /// 运行完整热更流程（重复调用安全：运行中直接返回当前状态）。
        /// 仅 Remote 模式有意义；Off/LocalOnly 模式直接返回 UpToDate。
        /// </summary>
        public async UniTask<FlowState> RunAsync(CancellationToken ct = default)
        {
            if (IsRunning)
            {
                Log.Warning("[AddressableUpdateFlow] 流程已在运行中，忽略重复调用");
                return State.Value;
            }

            Progress.Value = 0f;
            State.Value = FlowState.Checking;
            var system = AddressableSystem.Instance;

            if (system.Mode != AddressablesMode.Remote)
            {
                Log.Debug("[AddressableUpdateFlow] 非 Remote 模式，跳过热更检查");
                State.Value = FlowState.UpToDate;
                return State.Value;
            }

            try
            {
                LastResult = await system.CheckUpdateAsync(ct);
                if (ct.IsCancellationRequested)
                {
                    State.Value = FlowState.Failed;
                    return State.Value;
                }

                if (!LastResult.hasUpdate)
                {
                    State.Value = FlowState.UpToDate;
                    return State.Value;
                }

                State.Value = FlowState.Downloading;
                var progress = new System.Progress<float>(p => Progress.Value = p);
                bool ok = await system.UpdateContentAsync(LastResult, progress, ct);
                if (!ok || ct.IsCancellationRequested)
                {
                    State.Value = FlowState.Failed;
                    return State.Value;
                }

                Progress.Value = 1f;
                State.Value = FlowState.Completed;
                return State.Value;
            }
            catch (System.Exception ex)
            {
                Log.Error("[AddressableUpdateFlow] 热更流程异常: {0}", ex.Message);
                State.Value = FlowState.Failed;
                return State.Value;
            }
        }

        /// <summary>重置为 Idle（下次 RunAsync 重新检查）</summary>
        public void Reset()
        {
            State.Value = FlowState.Idle;
            Progress.Value = 0f;
        }
    }
}
