using NUnit.Framework;
using Gear.Framework.Modules.StateMachine;
using System.Collections.Generic;

namespace Gear.Tests
{
    /// <summary>
    /// 状态机（StateMachine&lt;TLabel&gt;）的 EditMode 测试。
    ///
    /// 这些用例只依赖显式时间步 <see cref="StateMachine{TLabel}.Update(float)"/>，
    /// 不读 Unity 的 Time.deltaTime，因此既能在 Unity 里跑，也能在无 Unity 环境里跑。
    /// 覆盖：进入/切换/回退、暂停、并行状态超时、主状态超时、Reset 清理。
    /// </summary>
    public class StateMachineTests
    {
        [Test]
        public void Enter_State_Raises_Start_Callback()
        {
            var events = new List<string>();
            var machine = new StateMachine<string>();
            machine.AddState("Idle",
                onStart: () => events.Add("start"),
                onStop: () => events.Add("stop"));

            machine.CurrentState = "Idle";

            Assert.AreEqual("Idle", machine.CurrentState);
            Assert.AreEqual("Idle", machine.ToString());
            CollectionAssert.AreEqual(new[] { "start" }, events);
        }

        [Test]
        public void Transition_Raises_Exit_Enter_And_Changed_Event()
        {
            var events = new List<string>();
            var machine = new StateMachine<string>();
            machine.AddState("A", onStart: () => events.Add("A+"), onStop: () => events.Add("A-"));
            machine.AddState("B", onStart: () => events.Add("B+"));

            string from = null;
            string to = null;
            machine.OnStateChanged += (oldState, newState) => { from = oldState; to = newState; };

            machine.CurrentState = "A";
            events.Clear();
            machine.CurrentState = "B";

            CollectionAssert.AreEqual(new[] { "A-", "B+" }, events);
            Assert.AreEqual("A", from);
            Assert.AreEqual("B", to);
        }

        [Test]
        public void Transition_Condition_Allows_Change()
        {
            var machine = new StateMachine<string>();
            machine.AddState("A");
            machine.AddState("B");
            machine.AddTransitionCondition("A", "B", () => true);

            machine.CurrentState = "A";

            machine.CurrentState = "B";

            Assert.AreEqual("B", machine.CurrentState);
        }

        [Test]
        public void Pause_Blocks_Update_Until_Resume()
        {
            var machine = new StateMachine<string>();
            int updates = 0;
            machine.AddState("A", onUpdate: () => updates++);
            machine.CurrentState = "A";

            machine.Pause();
            machine.Update(1f);
            Assert.AreEqual(0, updates, "暂停时不应驱动当前状态");

            machine.Resume();
            machine.Update(1f);
            Assert.AreEqual(1, updates);
        }

        [Test]
        public void Global_Update_Runs_Without_Current_State()
        {
            var machine = new StateMachine<string>();
            int globalUpdates = 0;
            machine.SetGlobalUpdate(() => globalUpdates++);

            machine.Update(0.5f);

            Assert.AreEqual(1, globalUpdates);
        }

        [Test]
        public void Parallel_State_Updates_And_Times_Out()
        {
            var machine = new StateMachine<string>();
            int starts = 0;
            int updates = 0;
            int stops = 0;

            machine.AddState("A");
            machine.CurrentState = "A";
            machine.AddParallelState("Buff",
                onStart: () => starts++,
                onUpdate: () => updates++,
                onStop: () => stops++,
                timeout: 1f);

            Assert.AreEqual(1, starts);

            machine.Update(0.6f);
            Assert.AreEqual(1, updates);
            Assert.AreEqual(0, stops);

            machine.Update(0.5f); // 累计 1.1s，超过 timeout
            Assert.AreEqual(1, stops);

            machine.Update(0.1f);
            Assert.AreEqual(1, updates, "超时移除后不应再收到 OnUpdate");
        }

        [Test]
        public void Parallel_State_Runs_Without_Main_State()
        {
            var machine = new StateMachine<string>();
            int updates = 0;
            int stops = 0;
            machine.AddParallelState("Buff",
                onUpdate: () => updates++,
                onStop: () => stops++,
                timeout: 1f);

            machine.Update(0.6f);
            Assert.AreEqual(1, updates, "没有主状态时并行状态也应更新");

            machine.Update(0.5f);
            Assert.AreEqual(1, stops, "没有主状态时并行状态也应超时移除");
        }

        [Test]
        public void Replacing_Parallel_State_Stops_The_Old_One()
        {
            var machine = new StateMachine<string>();
            machine.AddState("A");
            machine.CurrentState = "A";

            int oldStopped = 0;
            int newStarted = 0;
            machine.AddParallelState("Buff", onStop: () => oldStopped++);
            machine.AddParallelState("Buff", onStart: () => newStarted++);

            Assert.AreEqual(1, oldStopped);
            Assert.AreEqual(1, newStarted);
        }

        [Test]
        public void RemoveParallelState_Raises_Stop()
        {
            var machine = new StateMachine<string>();
            int stopped = 0;
            machine.AddParallelState("Buff", onStop: () => stopped++);

            machine.RemoveParallelState("Buff");

            Assert.AreEqual(1, stopped);
        }

        [Test]
        public void Revert_Restores_Previous_State()
        {
            var machine = new StateMachine<string>();
            machine.AddState("A");
            machine.AddState("B");
            machine.CurrentState = "A";
            machine.CurrentState = "B";

            machine.RevertToPreviousState();

            Assert.AreEqual("A", machine.CurrentState);
        }

        [Test]
        public void Reentering_Same_State_Does_Not_Push_History()
        {
            var machine = new StateMachine<string>();
            machine.AddState("A");
            machine.AddState("B");
            machine.CurrentState = "A";
            machine.CurrentState = "B";
            machine.CurrentState = "B"; // 同状态重入

            machine.RevertToPreviousState();

            Assert.AreEqual("A", machine.CurrentState, "同状态重入不应把 B 再压一次历史");
        }

        [Test]
        public void Reset_Clears_State_Parallel_And_History()
        {
            var machine = new StateMachine<string>();
            string exited = null;
            machine.OnStateExit += state => exited = state;
            machine.AddState("A");
            machine.AddState("B");
            machine.CurrentState = "A";
            machine.CurrentState = "B";

            int parallelStopped = 0;
            machine.AddParallelState("Buff", onStop: () => parallelStopped++);

            machine.Reset();

            Assert.IsNull(machine.CurrentState);
            Assert.AreEqual("B", exited);
            Assert.AreEqual(1, parallelStopped);

            machine.Update(1f);
            machine.RevertToPreviousState();
            Assert.IsNull(machine.CurrentState, "Reset 后不应还能回退到旧状态");
        }

        [Test]
        public void Main_State_Timeout_Switches_To_Default_State()
        {
            var machine = new StateMachine<string>();
            machine.AddState("Idle", timeout: 99f);
            machine.AddState("Attack", timeout: 1f);
            machine.SetDefaultState("Idle");
            machine.CurrentState = "Attack";

            machine.Update(0.6f);
            Assert.AreEqual("Attack", machine.CurrentState);

            machine.Update(0.6f); // 累计 1.2s，超过 Attack 的 timeout
            Assert.AreEqual("Idle", machine.CurrentState);
        }

        [Test]
        public void State_Without_Timeout_Never_Times_Out()
        {
            var machine = new StateMachine<string>();
            machine.AddState("Idle");
            machine.SetDefaultState("Idle");
            machine.CurrentState = "Idle";

            machine.Update(1000f);

            Assert.AreEqual("Idle", machine.CurrentState);
        }
    }
}
