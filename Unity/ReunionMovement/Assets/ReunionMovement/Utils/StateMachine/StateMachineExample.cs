using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using ReunionMovement.Common;
using ReunionMovement.Common.Util.StateMachine;

namespace ReunionMovement.Example
{
    /// <summary>
    /// 状态机示例
    /// </summary>
    public class StateMachineExample : MonoBehaviour
    {
        private StateMachine<StateMachineExampleState> stateMachine;
        void Start()
        {
            Invoke("Init", 3);
        }

        public void Init()
        {
            stateMachine = new StateMachine<StateMachineExampleState>();

            stateMachine.AddState(StateMachineExampleState.Idle, OnIdleEnter, OnIdleUpdate, OnIdleExit);
            stateMachine.AddState(StateMachineExampleState.Running, OnRunningEnter, OnRunningUpdate, OnRunningExit);
            stateMachine.AddState(StateMachineExampleState.Jumping, OnJumpingEnter, OnJumpingUpdate, OnJumpingExit);
            stateMachine.AddState(StateMachineExampleState.Attacking, OnAttackingEnter, OnAttackingUpdate, OnAttackingExit);

            stateMachine.CurrentState = StateMachineExampleState.Idle;
            stateMachine.SetDefaultState(StateMachineExampleState.Idle);

            stateMachine.AddTransitionCondition(StateMachineExampleState.Idle, StateMachineExampleState.Running, () => Input.GetKey(KeyCode.W));
            stateMachine.AddTransitionCondition(StateMachineExampleState.Running, StateMachineExampleState.Idle, () => !Input.GetKey(KeyCode.W));
            stateMachine.AddTransitionCondition(StateMachineExampleState.Running, StateMachineExampleState.Jumping, () => Input.GetKeyDown(KeyCode.Space));
            stateMachine.AddTransitionCondition(StateMachineExampleState.Idle, StateMachineExampleState.Attacking, () => Input.GetMouseButtonDown(0));
        }

        void Update()
        {
            if (stateMachine != null)
            {
                stateMachine.Update();
            }

            if (Input.GetKeyDown(KeyCode.W))
            {
                stateMachine.CurrentState = StateMachineExampleState.Running;
            }

            if (Input.GetKeyUp(KeyCode.W))
            {
                stateMachine.CurrentState = StateMachineExampleState.Idle;
            }

            if (Input.GetKey(KeyCode.Space))
            {

            }
        }

        private void OnIdleEnter() { Log.Debug("进入 Idle 状态"); }
        private void OnIdleUpdate() { Log.Debug("更新 Idle 状态"); }
        private void OnIdleExit() { Log.Debug("退出 Idle 状态"); }

        private void OnRunningEnter() { Log.Debug("进入 Running 状态"); }
        private void OnRunningUpdate() { Log.Debug("更新 Running 状态"); }
        private void OnRunningExit() { Log.Debug("退出 Running 状态"); }

        private void OnJumpingEnter() { Log.Debug("进入 Jumping 状态"); }
        private void OnJumpingUpdate() { Log.Debug("更新 Jumping 状态"); }
        private void OnJumpingExit() { Log.Debug("退出 Jumping 状态"); }

        private void OnAttackingEnter() { Log.Debug("进入 Attacking 状态"); }
        private void OnAttackingUpdate() { Log.Debug("更新 Attacking 状态"); }
        private void OnAttackingExit() { Log.Debug("退出 Attacking 状态"); }
    }
}
