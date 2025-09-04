using System;
using System.Collections;
using UnityEngine;

namespace ReunionMovement.Common.Util
{
    /// <summary>
    /// 命令基类：子类继承并实现 Execute 方法（可选实现 Undo）。
    /// 提供执行状态、CanExecute 检查、事件回调与协程辅助。
    /// </summary>
    public abstract class CommandBase : MonoBehaviour
    {
        [SerializeField]
        string commandName;

        [SerializeField, TextArea]
        string description;

        /// <summary>
        /// 命令名称（可在 Inspector 设置）
        /// </summary>
        public string CommandName
        {
            get => string.IsNullOrEmpty(commandName) ? GetType().Name : commandName;
            protected set => commandName = value;
        }

        /// <summary>
        /// 命令描述（可在 Inspector 设置）
        /// </summary>
        public string Description
        {
            get => description;
            protected set => description = value;
        }

        /// <summary>
        /// 当前命令是否已被执行（供撤销或查询使用）
        /// </summary>
        public bool IsExecuted { get; private set; }

        /// <summary>
        /// 执行完成事件（在 MarkExecuted 被调用时触发）
        /// </summary>
        public event Action<CommandBase> OnExecuted;

        /// <summary>
        /// 撤销完成事件（在 MarkUndone 被调用时触发）
        /// </summary>
        public event Action<CommandBase> OnUndone;

        /// <summary>
        /// 判断命令当前是否可以执行。子类可覆盖以实现条件检查。
        /// </summary>
        /// <param name="args">可选参数</param>
        /// <returns>true 则允许执行</returns>
        public virtual bool CanExecute(params object[] args)
        {
            return true;
        }

        /// <summary>
        /// 执行命令。子类必须实现此方法并在适当时调用 MarkExecuted()。
        /// </summary>
        /// <param name="args">执行参数</param>
        public abstract void Execute(params object[] args);

        /// <summary>
        /// 撤销命令。子类可重写实现具体撤销逻辑，并在适当时调用 MarkUndone()。
        /// 默认实现无操作。
        /// </summary>
        public virtual void Undo()
        {
            // 子类实现撤销逻辑后应调用 MarkUndone()
        }

        /// <summary>
        /// 重做命令。默认行为是再次调用 Execute(params)。
        /// 如果子类的 Execute 无法直接作为重做（需要特殊恢复逻辑），请重写此方法并在适当时调用 MarkExecuted()。
        /// </summary>
        /// <param name="args">可选参数（通常与 Execute 相同）</param>
        public virtual void Redo(params object[] args)
        {
            // 默认把 redo 映射到 Execute，子类可覆写以提供更精确的重做逻辑
            Execute(args);
        }

        /// <summary>
        /// 标记命令为已执行并触发事件（子类在 Execute 内在合适位置调用）。
        /// </summary>
        protected void MarkExecuted()
        {
            IsExecuted = true;
            try
            {
                OnExecuted?.Invoke(this);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{CommandName}] OnExecuted 回调异常: {ex}");
            }
        }

        /// <summary>
        /// 标记命令为已撤销并触发事件（子类在 Undo 内在合适位置调用）。
        /// </summary>
        protected void MarkUndone()
        {
            IsExecuted = false;
            try
            {
                OnUndone?.Invoke(this);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{CommandName}] OnUndone 回调异常: {ex}");
            }
        }

        /// <summary>
        /// 在命令中方便地启动协程
        /// </summary>
        protected Coroutine StartCommandCoroutine(IEnumerator routine)
        {
            if (routine == null) return null;
            return StartCoroutine(routine);
        }

        /// <summary>
        /// 停止由 StartCommandCoroutine 启动的协程
        /// </summary>
        protected void StopCommandCoroutine(Coroutine coroutine)
        {
            if (coroutine == null) return;
            StopCoroutine(coroutine);
        }

        /// <summary>
        /// 可由外部用于清理命令状态（例如从命令池取出前调用）
        /// </summary>
        public virtual void ResetCommand()
        {
            IsExecuted = false;
            OnExecuted = null;
            OnUndone = null;
        }
    }
}