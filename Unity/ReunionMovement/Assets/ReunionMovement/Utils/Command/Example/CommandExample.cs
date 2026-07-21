using ReunionMovement.Common;
using ReunionMovement.Common.Util;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ReunionMovement.Example
{
    /// <summary>
    /// 命令模式示例（纯 C# 版）：
    /// - MoveCommand 继承自 Command（纯 C#），记录移动前后位置，实现 Execute/Undo/Redo。
    /// - CommandManager 管理 undo/redo 栈并执行命令（纯 C#，不依赖 MonoBehaviour）。
    /// - CommandExample 用键盘测试：按 1 执行移动，U 撤销，R 重做。
    /// </summary>
    public class CommandExample : MonoBehaviour
    {
        public Transform target;
        public float moveDistance = 2f;

        private CommandManager commandManager = new CommandManager();
        private Keyboard keyboard;

        void Awake()
        {
            if (target == null) target = transform;
            keyboard = Keyboard.current;
        }

        void Update()
        {
            if (keyboard == null)
                keyboard = Keyboard.current;
            if (keyboard == null) return;

            // 按 1 执行移动命令
            if (keyboard.digit1Key.wasPressedThisFrame)
            {
                Vector3 to = target.position + target.forward * moveDistance;
                var cmd = new MoveCommand(target, to);
                commandManager.Execute(cmd);
                Log.Debug("Executed MoveCommand -> " + to);
            }

            // 撤销
            if (keyboard.uKey.wasPressedThisFrame)
            {
                if (commandManager.CanUndo)
                {
                    commandManager.Undo();
                    Log.Debug("Undo");
                }
                else Log.Debug("Nothing to undo");
            }

            // 重做
            if (keyboard.rKey.wasPressedThisFrame)
            {
                if (commandManager.CanRedo)
                {
                    commandManager.Redo();
                    Log.Debug("Redo");
                }
                else Log.Debug("Nothing to redo");
            }
        }
    }

    /// <summary>
    /// 简单移动命令（纯 C#）—— 记录执行前后位置，支持 Undo/Redo。
    /// 不再依赖 MonoBehaviour，可通过 new 直接创建，适配对象池和测试。
    /// </summary>
    public class MoveCommand : Command
    {
        private readonly Transform target;
        private readonly Vector3 to;
        private Vector3 from;

        public MoveCommand(Transform target, Vector3 to)
        {
            this.target = target;
            this.to = to;
        }

        public override void Execute(params object[] args)
        {
            if (target == null)
            {
                Log.Error("MoveCommand: target is null");
                return;
            }
            from = target.position;
            target.position = to;
            MarkExecuted();
        }

        public override void Undo()
        {
            if (target == null) return;
            target.position = from;
            MarkUndone();
        }

        public override void Redo(params object[] args)
        {
            if (target == null) return;
            target.position = to;
            MarkExecuted();
        }
    }

    /// <summary>
    /// 简单 CommandManager（纯 C#）—— 执行命令并维护 undo/redo 栈。
    /// 不再继承 MonoBehaviour，可作为纯逻辑对象使用或由 DI 容器管理。
    /// </summary>
    public class CommandManager
    {
        private readonly Stack<Command> undoStack = new Stack<Command>();
        private readonly Stack<Command> redoStack = new Stack<Command>();

        public bool CanUndo => undoStack.Count > 0;
        public bool CanRedo => redoStack.Count > 0;

        /// <summary>执行命令并将其推入 undo 栈，清空 redo 栈</summary>
        public void Execute(Command cmd, params object[] args)
        {
            if (cmd == null) return;
            if (!cmd.CanExecute(args)) return;

            cmd.Execute(args);
            undoStack.Push(cmd);
            redoStack.Clear();
        }

        /// <summary>撤销最近命令并将其推入 redo 栈</summary>
        public void Undo()
        {
            if (undoStack.Count == 0) return;
            var cmd = undoStack.Pop();
            cmd.Undo();
            redoStack.Push(cmd);
        }

        /// <summary>重做最近撤销的命令并将其推回 undo 栈</summary>
        public void Redo()
        {
            if (redoStack.Count == 0) return;
            var cmd = redoStack.Pop();
            cmd.Redo();
            undoStack.Push(cmd);
        }
    }
}
