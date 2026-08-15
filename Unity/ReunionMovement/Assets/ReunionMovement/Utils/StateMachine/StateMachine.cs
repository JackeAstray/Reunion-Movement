using System;
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;

namespace ReunionMovement.Common.Util.StateMachine
{
    /// <summary>
    /// 状态机
    /// </summary>
    /// <typeparam name="TLabel"></typeparam>
    public class StateMachine<TLabel>
    {
        private class State
        {
            public readonly TLabel label;       // 状态标签
            [JsonIgnore] public readonly Action OnStart;     // 开始时的回调
            [JsonIgnore] public readonly Action OnStop;      // 结束时的回调
            [JsonIgnore] public readonly Action OnUpdate;    // 更新时的回调

            public readonly int priority;       // 优先级

            public readonly float timeout;      // 超时时间
            public float elapsedTime;           // 已经过去的时间

            public State(TLabel label, Action onStart, Action onUpdate, Action onStop, float timeout = float.MaxValue, int priority = 0)
            {
                this.label = label;
                this.OnStart = onStart;
                this.OnUpdate = onUpdate;
                this.OnStop = onStop;
                this.priority = priority;
                this.timeout = timeout;
                this.elapsedTime = 0f;
            }
        }

        // 状态字典
        private readonly Dictionary<TLabel, State> stateDictionary;
        // 当前状态
        private State currentState;
        // 全局更新（委托不参与序列化，避免 JSON.NET 展开 Target 对象图）
        [JsonIgnore]
        private Action GlobalUpdate;
        // 状态历史（栈），带上限防止无界增长
        private const int MaxStateHistory = 64;
        private Stack<State> stateHistory;
        // 并行状态
        private List<State> parallelStates;
        // 状态改变事件
        public event Action<TLabel, TLabel> OnStateChanged;
        // 状态进入事件
        public event Action<TLabel> OnStateEnter;
        // 状态退出事件
        public event Action<TLabel> OnStateExit;
        // 状态转换条件（委托不参与序列化，避免 JSON.NET 展开 Target 对象图）
        [JsonIgnore]
        private readonly Dictionary<(TLabel, TLabel), Func<bool>> transitionConditions;
        // 默认状态
        private TLabel defaultStateLabel;
        // 状态机是否暂停
        private bool isPaused;

        public TLabel CurrentState
        {
            // 未进入任何状态时返回 default，避免 NRE
            get => currentState == null ? default : currentState.label;
            set => ChangeState(value);
        }

        /// <summary>
        /// 构造函数
        /// </summary>
        public StateMachine()
        {
            stateDictionary = new Dictionary<TLabel, State>();
            stateHistory = new Stack<State>();
            transitionConditions = new Dictionary<(TLabel, TLabel), Func<bool>>();
            parallelStates = new List<State>();
            isPaused = false;
        }

        /// <summary>
        /// 设置全局更新
        /// </summary>
        /// <param name="globalUpdate"></param>
        public void SetGlobalUpdate(Action globalUpdate)
        {
            this.GlobalUpdate = globalUpdate;
        }

        /// <summary>
        /// 更新状态
        /// </summary>
        public void Update()
        {
            if (isPaused)
            {
                return;
            }

            // 回调异常隔离：单个订阅者抛异常不得中断整个状态机本帧更新
            try { GlobalUpdate?.Invoke(); } catch (Exception ex) { Log.Error("StateMachine GlobalUpdate 订阅者异常（已隔离）: {0}", ex.Message); }

            if (currentState == null)
            {
                return;
            }

            // 用局部快照：OnUpdate 回调内可能调用 ChangeState/Reset 修改 currentState。
            // 注意：对局部快照判空是无效防御（快照刚来自非空 currentState），
            // 必须在 OnUpdate 之后重新校验 currentState 是否仍指向同一状态实例，
            // 否则会对已退出的旧状态累加计时并触发虚假的超时切换。
            var state = currentState;
            try { state.OnUpdate?.Invoke(); } catch (Exception ex) { Log.Error("StateMachine OnUpdate 订阅者异常（已隔离）: {0}", ex.Message); }
            if (!ReferenceEquals(currentState, state)) return;

            state.elapsedTime += Time.deltaTime;

            if (state.elapsedTime >= state.timeout)
            {
                HandleStateTimeout(state);
            }

            // 并行状态更新（倒序遍历以支持超时移除）
            for (int i = parallelStates.Count - 1; i >= 0; i--)
            {
                var parallelState = parallelStates[i];
                parallelState.elapsedTime += Time.deltaTime;
                if (parallelState.elapsedTime >= parallelState.timeout)
                {
                    try { parallelState.OnStop?.Invoke(); } catch (Exception ex) { Log.Error("StateMachine 并行状态 OnStop 异常（已隔离）: {0}", ex.Message); }
                    parallelStates.RemoveAt(i);
                    continue;
                }
                try { parallelState.OnUpdate?.Invoke(); } catch (Exception ex) { Log.Error("StateMachine 并行状态 OnUpdate 异常（已隔离）: {0}", ex.Message); }
            }
        }

        /// <summary>
        /// 添加状态
        /// </summary>
        /// <typeparam name="TSubStateLabel"></typeparam>
        /// <param name="label"></param>
        /// <param name="subMachine"></param>
        /// <param name="subMachineStartState"></param>
        public void AddState<TSubStateLabel>(TLabel label, StateMachine<TSubStateLabel> subMachine, TSubStateLabel subMachineStartState)
        {
            AddState(label, () => subMachine.ChangeState(subMachineStartState), subMachine.Update);
        }

        /// <summary>
        /// 添加状态
        /// </summary>
        /// <param name="label"></param>
        /// <param name="onStart"></param>
        /// <param name="onUpdate"></param>
        /// <param name="onStop"></param>
        public void AddState(TLabel label, Action onStart = null, Action onUpdate = null, Action onStop = null)
        {
            stateDictionary[label] = new State(label, onStart, onUpdate, onStop);
        }

        /// <summary>
        /// 添加状态转换条件
        /// </summary>
        /// <param name="fromState"></param>
        /// <param name="toState"></param>
        /// <param name="condition"></param>
        public void AddTransitionCondition(TLabel fromState, TLabel toState, Func<bool> condition)
        {
            transitionConditions[(fromState, toState)] = condition;
        }

        /// <summary>
        /// 添加并行状态。
        /// 若已存在同名并行状态，先调用旧状态的 onStop 并移除。
        /// 加入后立即调用 onStart；超时后自动调用 onStop 并移除。
        /// </summary>
        public void AddParallelState(TLabel label, Action onStart = null, Action onUpdate = null, Action onStop = null, float timeout = float.MaxValue, int priority = 0)
        {
            // 若已存在同名并行状态，先移除旧的（避免重复实例）
            for (int i = 0; i < parallelStates.Count; i++)
            {
                if (EqualityComparer<TLabel>.Default.Equals(parallelStates[i].label, label))
                {
                    parallelStates[i].OnStop?.Invoke();
                    parallelStates.RemoveAt(i);
                    break;
                }
            }

            var newState = new State(label, onStart, onUpdate, onStop, timeout, priority);
            // 按优先级降序线性插入（避免 OrderByDescending + ToList 产生的 GC 分配）
            int insertIndex = 0;
            for (; insertIndex < parallelStates.Count; insertIndex++)
            {
                if (priority > parallelStates[insertIndex].priority) break;
            }
            parallelStates.Insert(insertIndex, newState);

            // 进入时触发 onStart（与主状态行为一致）
            newState.OnStart?.Invoke();
        }

        /// <summary>
        /// 改变状态
        /// </summary>
        /// <param name="newState"></param>
        /// <returns>切换是否成功（条件不满足或目标未注册时返回 false）</returns>
        private bool ChangeState(TLabel newState)
        {
            if (currentState != null && !IsTransitionConditionsMet(newState))
            {
                Log.Error("无法从状态 {0} 转换到 {1}，条件未满足。", currentState.label, newState);
                return false;
            }

            return PerformStateChange(newState);
        }

        /// <summary>
        /// 执行状态切换。若目标状态未注册，则记录错误并保持当前状态。
        /// </summary>
        /// <param name="newState"></param>
        /// <returns>切换是否成功</returns>
        private bool PerformStateChange(TLabel newState)
        {
            // 防御：检查目标状态是否已注册
            if (!stateDictionary.TryGetValue(newState, out State targetState))
            {
                Log.Error("状态切换失败：目标状态 {0} 未注册。保持当前状态。", newState);
                return false;
            }

            TLabel oldLabel = default(TLabel);
            if (currentState != null)
            {
                oldLabel = currentState.label;
                // 旧状态退出回调逐段隔离：单个订阅者异常不得中断切换流程
                // （否则出现“OnStop 已执行但状态未切换”的不一致状态）
                try { currentState.OnStop?.Invoke(); } catch (Exception ex) { Log.Error("StateMachine OnStop 异常（已隔离）: {0}", ex.Message); }
                try { OnStateExit?.Invoke(currentState.label); } catch (Exception ex) { Log.Error("StateMachine OnStateExit 订阅者异常（已隔离）: {0}", ex.Message); }

                // 同状态重入（如 Attacking→Attacking 重启攻击）不压历史栈：
                // 从重启后的状态 Revert 应回到真正的上一个状态，而不是重复的自身条目；
                // 同时避免高频同状态重入导致历史栈无界增长（触发上限告警刷屏）。
                bool isSameState = ReferenceEquals(currentState, targetState);
                if (!isSameState)
                {
                    // 带上限压栈：超出时丢弃最旧记录，防止高频 ChangeState 导致无界增长
                    if (stateHistory.Count >= MaxStateHistory)
                    {
                        var items = stateHistory.ToArray();
                        stateHistory.Clear();
                        // items[0] 为栈顶（最新），末位为最旧；丢弃最旧的一条
                        for (int i = 0; i < items.Length - 1; i++)
                        {
                            stateHistory.Push(items[i]);
                        }
                        Log.Warning("StateMachine: 状态历史超过上限 {0}，已丢弃最旧记录", MaxStateHistory);
                    }
                    stateHistory.Push(currentState);
                }
            }

            currentState = targetState;
            // 进入新状态必须重置计时：否则 timeout 计时跨多次进入累计——
            // 状态 A（timeout=2s）活跃 1.9s→切走→切回 0.1s 后即超时；
            // 超时切换到默认状态成功后旧状态 elapsedTime 保留在 timeout 以上，重进下一帧立即超时
            currentState.elapsedTime = 0f;
            try { currentState.OnStart?.Invoke(); } catch (Exception ex) { Log.Error("StateMachine OnStart 异常（已隔离）: {0}", ex.Message); }
            try { OnStateEnter?.Invoke(newState); } catch (Exception ex) { Log.Error("StateMachine OnStateEnter 订阅者异常（已隔离）: {0}", ex.Message); }
            try { OnStateChanged?.Invoke(oldLabel, newState); } catch (Exception ex) { Log.Error("StateMachine OnStateChanged 订阅者异常（已隔离）: {0}", ex.Message); }
            // 状态已实际切换，即使某个订阅者抛异常也应返回 true（与真实状态一致），
            // 不再像旧实现那样“已切换却返回 false”误导调用方
            return true;
        }

        /// <summary>
        /// 处理状态超时
        /// </summary>
        private void HandleStateTimeout(State state)
        {
            // 只有当 defaultStateLabel 被设置为有效状态时，才允许切换到默认状态
            if (!EqualityComparer<TLabel>.Default.Equals(defaultStateLabel, default(TLabel)))
            {
                Log.Debug("状态 {0} 超时，切换到默认状态", state.label);

                if (!ChangeState(defaultStateLabel))
                {
                    // 切换失败（默认状态未注册或转换条件不满足）：
                    // 重置计时，避免 elapsedTime 持续增长导致每帧重复触发超时并刷错误日志
                    state.elapsedTime = 0f;
                }
            }
            else
            {
                // 未配置默认状态：同样重置计时，避免每帧重复进入超时分支刷日志
                state.elapsedTime = 0f;
            }
        }

        /// <summary>
        /// 设置默认状态
        /// </summary>
        /// <param name="label"></param>
        public void SetDefaultState(TLabel label)
        {
            defaultStateLabel = label;
        }

        /// <summary>
        /// 是否满足状态转换条件
        /// </summary>
        /// <param name="newState"></param>
        /// <returns></returns>
        private bool IsTransitionConditionsMet(TLabel newState)
        {
            if (currentState == null)
                return true;
            // 若未注册条件，默认允许转换；若注册了条件，则按条件判断
            if (transitionConditions.TryGetValue((currentState.label, newState), out var condition))
            {
                return condition();
            }
            return true;
        }

        /// <summary>
        /// 并行状态的移除方法（手动遍历避免 LINQ FirstOrDefault 的委托分配）
        /// </summary>
        /// <param name="label"></param>
        public void RemoveParallelState(TLabel label)
        {
            for (int i = 0; i < parallelStates.Count; i++)
            {
                if (EqualityComparer<TLabel>.Default.Equals(parallelStates[i].label, label))
                {
                    // 与超时移除、AddParallelState 的旧状态移除保持一致：先触发 OnStop 再移除
                    parallelStates[i].OnStop?.Invoke();
                    parallelStates.RemoveAt(i);
                    return;
                }
            }
        }

        /// <summary>
        /// 回退到上一个状态（触发与 ChangeState 一致的事件流）
        /// </summary>
        public void RevertToPreviousState()
        {
            if (stateHistory.Count > 0)
            {
                TLabel oldLabel = currentState != null ? currentState.label : default;
                try { currentState?.OnStop?.Invoke(); } catch (Exception ex) { Log.Error("StateMachine OnStop 异常（已隔离）: {0}", ex.Message); }
                if (currentState != null)
                {
                    try { OnStateExit?.Invoke(currentState.label); } catch (Exception ex) { Log.Error("StateMachine OnStateExit 订阅者异常（已隔离）: {0}", ex.Message); }
                }

                currentState = stateHistory.Pop();
                // 与 ChangeState 一致：进入状态时重置计时，防止 timeout 跨多次进入累计
                currentState.elapsedTime = 0f;
                try { currentState?.OnStart?.Invoke(); } catch (Exception ex) { Log.Error("StateMachine OnStart 异常（已隔离）: {0}", ex.Message); }
                try { OnStateEnter?.Invoke(currentState.label); } catch (Exception ex) { Log.Error("StateMachine OnStateEnter 订阅者异常（已隔离）: {0}", ex.Message); }
                try { OnStateChanged?.Invoke(oldLabel, currentState.label); } catch (Exception ex) { Log.Error("StateMachine OnStateChanged 订阅者异常（已隔离）: {0}", ex.Message); }
            }
        }

        /// <summary>
        /// 暂停状态机
        /// </summary>
        public void Pause()
        {
            isPaused = true;
        }

        /// <summary>
        /// 恢复状态机
        /// </summary>
        public void Resume()
        {
            isPaused = false;
        }

        /// <summary>
        /// 重置状态机
        /// </summary>
        public void Reset()
        {
            currentState?.OnStop?.Invoke();
            currentState = null;
            // 并行状态也需停止并清空，否则 Reset 后仍在 Update 中运行（泄漏）
            for (int i = 0; i < parallelStates.Count; i++)
            {
                parallelStates[i].OnStop?.Invoke();
            }
            parallelStates.Clear();
            stateHistory.Clear();
        }

        /// <summary>
        /// 状态机的序列化方法
        /// 注意：Action 委托无法通过 JSON 正确序列化/反序列化，
        /// OnStart/OnUpdate/OnStop 回调将在反序列化后丢失。
        /// 此方法仅适用于保存状态结构（标签、超时等），不保存行为。
        /// </summary>
        /// <returns></returns>
        // 全局共用的 JSON 序列化设置（禁用 TypeNameHandling 防止反序列化漏洞）
        private static readonly JsonSerializerSettings SafeJsonSettings = new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.None
        };

        public string Serialize()
        {
            // 直接序列化 this 只会得到 CurrentState 标签（私有字段默认不参与序列化），
            // 反序列化后状态机为空壳。这里改为序列化显式 DTO：状态结构（标签/超时/优先级）+ 当前/默认/历史。
            var snapshot = new Snapshot
            {
                states = new List<State>(stateDictionary.Values),
                currentStateLabel = CurrentState,
                defaultStateLabel = defaultStateLabel,
                historyLabels = new List<TLabel>(stateHistory.Count),
            };
            // Stack 枚举顺序为栈顶→栈底，序列化保持该顺序；反序列化时倒序 Push 恢复
            foreach (var s in stateHistory)
            {
                snapshot.historyLabels.Add(s.label);
            }
            return JsonConvert.SerializeObject(snapshot, SafeJsonSettings);
        }

        /// <summary>
        /// 状态机的反序列化方法
        /// 警告：Action 委托无法反序列化，需要重新注册 OnStart/OnUpdate/OnStop 回调。
        /// </summary>
        /// <param name="json"></param>
        public StateMachine<TLabel> Deserialize(string json)
        {
            var machine = new StateMachine<TLabel>();
            var snapshot = JsonConvert.DeserializeObject<Snapshot>(json, SafeJsonSettings);
            if (snapshot == null || snapshot.states == null)
            {
                Log.Warning("StateMachine 反序列化失败：JSON 无效或缺少状态结构");
                return machine;
            }
            // 重建状态结构（回调丢失，由调用方重新 AddState 绑定行为）
            foreach (var s in snapshot.states)
            {
                if (s == null) continue;
                machine.stateDictionary[s.label] = new State(s.label, null, null, null, s.timeout, s.priority);
            }
            machine.defaultStateLabel = snapshot.defaultStateLabel;
            // 恢复历史栈：倒序 Push（historyLabels[0] 是栈顶，最后入栈的才是栈顶）
            if (snapshot.historyLabels != null)
            {
                for (int i = snapshot.historyLabels.Count - 1; i >= 0; i--)
                {
                    if (machine.stateDictionary.TryGetValue(snapshot.historyLabels[i], out var st))
                    {
                        machine.stateHistory.Push(st);
                    }
                }
            }
            // 恢复当前状态（仅当标签有效）
            if (snapshot.currentStateLabel != null && machine.stateDictionary.TryGetValue(snapshot.currentStateLabel, out var cur))
            {
                machine.currentState = cur;
            }
            return machine;
        }

        /// <summary>序列化快照 DTO（仅结构，不含委托）</summary>
        private class Snapshot
        {
            public List<State> states;
            public TLabel currentStateLabel;
            public TLabel defaultStateLabel;
            public List<TLabel> historyLabels;
        }

        /// <summary>
        /// 转换为字符串
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return CurrentState?.ToString();
        }
    }
}
