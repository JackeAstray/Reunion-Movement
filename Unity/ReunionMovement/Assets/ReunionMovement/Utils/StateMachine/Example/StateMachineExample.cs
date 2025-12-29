using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using ReunionMovement.Common;
using ReunionMovement.Common.Util.StateMachine;
using UnityEngine.InputSystem;

namespace ReunionMovement.Example
{
    /// <summary>
    /// 状态机示例
    /// </summary>
    public class StateMachineExample : MonoBehaviour
    {
        private StateMachine<StateMachineExampleState> stateMachine;

        public Keyboard keyboard;
        public Mouse mouse;

        void Start()
        {
            keyboard = Keyboard.current;
            mouse = Mouse.current;

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

            stateMachine.AddTransitionCondition(StateMachineExampleState.Idle, StateMachineExampleState.Running, () => Keyboard.current.wKey.isPressed);
            stateMachine.AddTransitionCondition(StateMachineExampleState.Running, StateMachineExampleState.Idle, () => !Keyboard.current.wKey.isPressed);
            stateMachine.AddTransitionCondition(StateMachineExampleState.Running, StateMachineExampleState.Jumping, () => Keyboard.current.spaceKey.wasPressedThisFrame);
            stateMachine.AddTransitionCondition(StateMachineExampleState.Idle, StateMachineExampleState.Attacking, () => Mouse.current.leftButton.wasPressedThisFrame);
            stateMachine.AddTransitionCondition(StateMachineExampleState.Attacking, StateMachineExampleState.Attacking, () => Mouse.current.leftButton.wasPressedThisFrame);
            stateMachine.AddTransitionCondition(StateMachineExampleState.Attacking, StateMachineExampleState.Running, () => Keyboard.current.wKey.isPressed);
            stateMachine.AddTransitionCondition(StateMachineExampleState.Attacking, StateMachineExampleState.Idle, () => !Keyboard.current.wKey.isPressed);
        }

        void Update()
        {
            if (stateMachine == null)
            {
                return;
            }

            stateMachine.Update();

            if (keyboard != null)
            {
                if (keyboard.wKey.wasPressedThisFrame)
                {
                    stateMachine.CurrentState = StateMachineExampleState.Running;
                }

                if (keyboard.wKey.wasReleasedThisFrame)
                {
                    stateMachine.CurrentState = StateMachineExampleState.Idle;
                }

                if (keyboard.spaceKey.isPressed)
                {
                    stateMachine.CurrentState = StateMachineExampleState.Jumping;
                }
            }

            if (mouse != null && mouse.leftButton.wasPressedThisFrame)
            {
                stateMachine.CurrentState = StateMachineExampleState.Attacking;
            }
        }

        private void OnIdleEnter() { Log.Debug("进入 Idle 状态"); }
        private void OnIdleUpdate() { }
        private void OnIdleExit() { Log.Debug("退出 Idle 状态"); }

        private void OnRunningEnter() { Log.Debug("进入 Running 状态"); }
        private void OnRunningUpdate() { }
        private void OnRunningExit() { Log.Debug("退出 Running 状态"); }

        private void OnJumpingEnter() { Log.Debug("进入 Jumping 状态"); }
        private void OnJumpingUpdate() { }
        private void OnJumpingExit() { Log.Debug("退出 Jumping 状态"); }

        private void OnAttackingEnter() { Log.Debug("进入 Attacking 状态"); }
        private void OnAttackingUpdate() { }
        private void OnAttackingExit() { Log.Debug("退出 Attacking 状态"); }
    }
}
