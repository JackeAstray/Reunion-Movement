using ReunionMovement.Common;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ReunionMovement.Example
{
    /// <summary>
    /// 命令模式示例：
    /// - MoveCommand 继承自 CommandBase，记录移动前后位置，实现 Execute/Undo/Redo。
    /// - CommandManager 管理 undo/redo 栈并执行命令。
    /// - CommandExample 用键盘测试：按 1 执行移动，U 撤销，R 重做。
    /// 将此脚本挂到场景中的任意 GameObject，并在 Inspector 指定 target 或留空以使用自身 transform。
    /// </summary>
    public class CommandExample : MonoBehaviour
    {
        public Transform target; // 目标对象（可在 Inspector 指定）
        public float moveDistance = 2f;

        CommandManager commandManager;
        Keyboard keyboard;

        void Awake()
        {
            if (target == null) target = transform;
            commandManager = new GameObject("CommandManager").AddComponent<CommandManager>();
            // 把 manager 放到本对象下以便层级清晰（可选）
            commandManager.transform.SetParent(transform);

            keyboard = Keyboard.current;
        }

        void Update()
        {
            // 确保 keyboard 可用（运行时可能为 null）
            if (keyboard == null)
                keyboard = Keyboard.current;

            if (keyboard == null)
                return;

            // 按 1 执行一次移动命令（向前）
            if (keyboard.digit1Key.wasPressedThisFrame)
            {
                Vector3 to = target.position + target.forward * moveDistance;
                var cmd = commandManager.CreateMoveCommand(target, to);
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
                else
                {
                    Log.Debug("Nothing to undo");
                }
            }

            // 重做
            if (keyboard.rKey.wasPressedThisFrame)
            {
                if (commandManager.CanRedo)
                {
                    commandManager.Redo();
                    Log.Debug("Redo");
                }
                else
                {
                    Log.Debug("Nothing to redo");
                }
            }
        }
    }

    /// <summary>
    /// 简单移动命令：记录执行前后位置，支持 Undo/Redo。
    /// 注意：继承自 CommandBase（MonoBehaviour），在运行时通过 AddComponent 创建并由 CommandManager 管理生命周期。
    /// </summary>
    public class MoveCommand : ReunionMovement.Common.Util.CommandBase
    {
        public Transform target;
        public Vector3 from;
        public Vector3 to;

        // Execute 使用 params 保持与基类签名一致（此处不使用 args）
        public override void Execute(params object[] args)
        {
            if (target == null)
            {
                Debug.LogError("MoveCommand: target is null");
                return;
            }

            // 记录原始位置
            from = target.position;
            // 应用目标位置
            target.position = to;
            // 标记已执行
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
            // 对于此命令，Redo 与 Execute 的行为相同（也可直接调用 base.Redo(args)）
            if (target == null) return;
            target.position = to;
            MarkExecuted();
        }
    }

    /// <summary>
    /// 简单 CommandManager：执行命令并维护 undo/redo 栈
    /// 注意：此示例把命令作为组件（MonoBehaviour）挂在自身的 GameObject 下。
    /// </summary>
    public class CommandManager : MonoBehaviour
    {
        readonly Stack<ReunionMovement.Common.Util.CommandBase> undoStack = new Stack<ReunionMovement.Common.Util.CommandBase>();
        readonly Stack<ReunionMovement.Common.Util.CommandBase> redoStack = new Stack<ReunionMovement.Common.Util.CommandBase>();

        public bool CanUndo => undoStack.Count > 0;
        public bool CanRedo => redoStack.Count > 0;

        /// <summary>
        /// 执行命令并将其推入 undo 栈，清空 redo 栈
        /// </summary>
        public void Execute(ReunionMovement.Common.Util.CommandBase cmd, params object[] args)
        {
            if (cmd == null) return;

            if (!cmd.CanExecute(args)) return;

            cmd.Execute(args);
            undoStack.Push(cmd);
            // 新操作清空 redo 栈（标准行为）
            redoStack.Clear();
        }

        /// <summary>
        /// 撤销最近命令并将其推入 redo 栈
        /// </summary>
        public void Undo()
        {
            if (undoStack.Count == 0) return;

            var cmd = undoStack.Pop();
            cmd.Undo();
            redoStack.Push(cmd);
        }

        /// <summary>
        /// 重做最近撤销的命令并将其推回 undo 栈
        /// </summary>
        public void Redo()
        {
            if (redoStack.Count == 0) return;

            var cmd = redoStack.Pop();
            cmd.Redo();
            undoStack.Push(cmd);
        }

        /// <summary>
        /// 创建并返回一个 MoveCommand 实例（作为本对象的子组件）
        /// 调用者负责将命令传入 Execute 执行
        /// </summary>
        public MoveCommand CreateMoveCommand(Transform target, Vector3 to)
        {
            var cmd = gameObject.AddComponent<MoveCommand>();
            cmd.target = target;
            cmd.to = to;
            return cmd;
        }
    }
}