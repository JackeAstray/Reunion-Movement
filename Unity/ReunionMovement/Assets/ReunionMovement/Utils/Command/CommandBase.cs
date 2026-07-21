using System;
using System.Collections;
using UnityEngine;

namespace ReunionMovement.Common.Util
{
    /// <summary>
    /// 命令基类（纯 C#）—— 不依赖 MonoBehaviour，可在任意上下文中使用（游戏逻辑、服务端、测试等）。
    /// 子类需实现 Execute 方法（可选实现 Undo）。
    /// 提供执行状态、CanExecute 检查与事件回调。
    ///
    /// 如需在 Unity Inspector 中配置命令参数，请使用 MonoCommandBehaviour 适配器。
    /// </summary>
    public abstract class Command
    {
        /// <summary>命令名称</summary>
        public string CommandName
        {
            get => string.IsNullOrEmpty(_commandName) ? GetType().Name : _commandName;
            set => _commandName = value;
        }
        private string _commandName;

        /// <summary>命令描述</summary>
        public string Description
        {
            get => _description;
            set => _description = value;
        }
        private string _description;

        /// <summary>当前命令是否已被执行（供撤销或查询使用）</summary>
        public bool IsExecuted { get; private set; }

        /// <summary>执行完成事件（在 MarkExecuted 被调用时触发）</summary>
        public event Action<Command> OnExecuted;

        /// <summary>撤销完成事件（在 MarkUndone 被调用时触发）</summary>
        public event Action<Command> OnUndone;

        /// <summary>判断命令当前是否可以执行。子类可覆盖以实现条件检查。</summary>
        public virtual bool CanExecute(params object[] args) => true;

        /// <summary>执行命令。子类必须实现此方法并在适当时调用 MarkExecuted()。</summary>
        public abstract void Execute(params object[] args);

        /// <summary>撤销命令。子类可重写实现具体撤销逻辑，并在适当时调用 MarkUndone()。</summary>
        public virtual void Undo() { }

        /// <summary>重做命令。默认行为是再次调用 Execute(params)。</summary>
        public virtual void Redo(params object[] args) => Execute(args);

        /// <summary>标记命令为已执行并触发事件</summary>
        protected void MarkExecuted()
        {
            IsExecuted = true;
            try { OnExecuted?.Invoke(this); }
            catch (Exception ex) { Log.Error("[{0}] OnExecuted 回调异常: {1}", CommandName, ex); }
        }

        /// <summary>标记命令为已撤销并触发事件</summary>
        protected void MarkUndone()
        {
            IsExecuted = false;
            try { OnUndone?.Invoke(this); }
            catch (Exception ex) { Log.Error("[{0}] OnUndone 回调异常: {1}", CommandName, ex); }
        }

        /// <summary>可由外部用于清理命令状态（例如从命令池取出前调用）</summary>
        public virtual void ResetCommand()
        {
            IsExecuted = false;
            OnExecuted = null;
            OnUndone = null;
        }
    }

    /// <summary>
    /// [已废弃] 旧版命令基类 —— 保留以兼容现有 MonoBehaviour 命令代码。
    /// 新代码请直接继承 Command（纯 C#），需要协程辅助时使用 MonoCommandBehaviour。
    /// </summary>
    [Obsolete("请直接继承 Command（纯 C#）。如需 Unity 协程支持，使用 MonoCommandBehaviour 适配器。", false)]
    public abstract class CommandBase : MonoBehaviour
    {
        [SerializeField] private string commandName;
        [SerializeField, TextArea] private string description;

        public string CommandName
        {
            get => string.IsNullOrEmpty(commandName) ? GetType().Name : commandName;
            protected set => commandName = value;
        }

        public string Description
        {
            get => description;
            protected set => description = value;
        }

        public bool IsExecuted { get; private set; }

        public event Action<CommandBase> OnExecuted;
        public event Action<CommandBase> OnUndone;

        public virtual bool CanExecute(params object[] args) => true;

        public abstract void Execute(params object[] args);

        public virtual void Undo() { }

        public virtual void Redo(params object[] args) => Execute(args);

        protected void MarkExecuted()
        {
            IsExecuted = true;
            try { OnExecuted?.Invoke(this); }
            catch (Exception ex) { Log.Error("[{0}] OnExecuted 回调异常: {1}", CommandName, ex); }
        }

        protected void MarkUndone()
        {
            IsExecuted = false;
            try { OnUndone?.Invoke(this); }
            catch (Exception ex) { Log.Error("[{0}] OnUndone 回调异常: {1}", CommandName, ex); }
        }

        /// <summary>在命令中方便地启动协程</summary>
        protected Coroutine StartCommandCoroutine(IEnumerator routine)
        {
            if (routine == null) return null;
            return StartCoroutine(routine);
        }

        /// <summary>停止由 StartCommandCoroutine 启动的协程</summary>
        protected void StopCommandCoroutine(Coroutine coroutine)
        {
            if (coroutine == null) return;
            StopCoroutine(coroutine);
        }

        public virtual void ResetCommand()
        {
            IsExecuted = false;
            OnExecuted = null;
            OnUndone = null;
        }
    }

    /// <summary>
    /// Unity 命令适配器 —— 将纯 C# Command 挂载到 GameObject 上，提供协程支持和 Inspector 配置。
    /// 使用方式：gameObject.AddComponent&lt;MonoCommandBehaviour&gt;().Bind(yourCommand);
    /// </summary>
    public class MonoCommandBehaviour : MonoBehaviour
    {
        [SerializeField] private string commandName;
        [SerializeField, TextArea] private string description;

        /// <summary>绑定的纯 C# 命令</summary>
        public Command Command { get; private set; }

        /// <summary>绑定命令实例</summary>
        public void Bind(Command command)
        {
            Command = command;
            if (!string.IsNullOrEmpty(commandName))
                Command.CommandName = commandName;
            if (!string.IsNullOrEmpty(description))
                Command.Description = description;
        }

        /// <summary>在适配器中方便地启动协程（供 Command 的 Execute/Undo 中通过回调使用）</summary>
        public Coroutine StartManagedCoroutine(IEnumerator routine)
        {
            if (routine == null) return null;
            return StartCoroutine(routine);
        }

        /// <summary>停止托管协程</summary>
        public void StopManagedCoroutine(Coroutine coroutine)
        {
            if (coroutine == null) return;
            StopCoroutine(coroutine);
        }
    }
}
