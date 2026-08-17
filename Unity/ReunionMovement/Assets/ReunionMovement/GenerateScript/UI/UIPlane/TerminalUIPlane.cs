//此脚本是由工具自动生成，请勿手动创建

using Cysharp.Threading.Tasks;
using ReunionMovement.Common;
using ReunionMovement.Common.Util;
using ReunionMovement.Core.Terminal;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace ReunionMovement.Core.UI
{
    public class TerminalUIPlane : UIController
    {
        string openWindow = UINames.Terminal;
        string closeWindow = UINames.Terminal;

        string command;
        public Button clear;    //清除
        public Button close;    //关闭
        public TMP_InputField input;//输入

        public GameObject root;
        public GameObject itemGo;
        // 终端条目的最大数量，防止无限增长导致内存泄漏
        private const int MaxTerminalItems = 100;
        // 布局重建合批标记：高频刷屏时每条日志都 ForceRebuildLayoutImmediate 是 O(n²)，合并到下一帧执行
        private bool layoutRebuildPending = false;

        // ============================================================
        //  命令历史（↑/↓ 导航）
        // ============================================================
        private readonly List<string> commandHistory = new List<string>();
        private const int MaxHistoryCount = 50;
        // -1 = 当前编辑行；>=0 表示正导航历史（0=最新一条）
        private int historyIndex = -1;
        // 进入历史导航前暂存的未提交输入（按 ↓ 回到编辑行时恢复）
        private string pendingInput = null;

        public override void OnInit()
        {
            base.OnInit();

            // Prefab 未绑定时给出明确错误，避免后续 NRE（与 OnDestroy 判空保持一致）
            if (clear == null || close == null || input == null || root == null || itemGo == null)
            {
                Log.Error("TerminalUIPlane.OnInit: 存在未绑定的引用（clear/close/input/root/itemGo），请检查 Prefab 配置");
                return;
            }

            clear.onClick.RemoveAllListeners();
            close.onClick.RemoveAllListeners();
            input.onEndEdit.RemoveAllListeners();

            command = "";

            clear.onClick.AddListener(() =>
            {
                // 清屏时必须跳过模板 itemGo：模板销毁后 Instantiate(itemGo) 抛 MissingReferenceException，终端永久失效
                for (int i = root.transform.childCount - 1; i >= 0; i--)
                {
                    var child = root.transform.GetChild(i);
                    if (child.gameObject == itemGo) continue;
                    Destroy(child.gameObject);
                }
                if (root.TryGetComponent<RectTransform>(out var rootRt))
                {
                    LayoutRebuilder.ForceRebuildLayoutImmediate(rootRt);
                }
            });

            close.onClick.AddListener(() =>
            {
                UISystem.Instance.CloseWindow(UINames.Terminal);
            });

            input.onEndEdit.AddListener(OnEndEdit);
        }

        public override void OnOpen(params object[] args)
        {
            base.OnOpen(args);
        }

        public override void OnSet(params object[] args)
        {
            base.OnSet(args);

            if (args != null && args.Length > 0 && args[0] != null)
            {
                switch (args[0].ToString())
                {
                    case "CreateItem":
                        if (args.Length >= 2 && args[1] != null)
                        {
                            CreateItem(args[1].ToString());
                        }
                        break;
                }
            }
        }

        public override void OnClose()
        {
            base.OnClose();
        }

        /// <summary>输入框聚焦时支持 ↑/↓ 浏览命令历史</summary>
        private void Update()
        {
            if (input == null || !input.isFocused) return;
            var keyboard = Keyboard.current;
            if (keyboard == null) return;
            if (keyboard.upArrowKey.wasPressedThisFrame)
            {
                NavigateHistory(1);
            }
            else if (keyboard.downArrowKey.wasPressedThisFrame)
            {
                NavigateHistory(-1);
            }
        }

        /// <summary>按方向导航历史（+1 更旧 / -1 更新），越界时回到编辑行</summary>
        private void NavigateHistory(int direction)
        {
            if (commandHistory.Count == 0) return;

            if (historyIndex == -1)
            {
                if (direction < 0) return; // 尚未导航时按 ↓ 无效
                pendingInput = input.text;
                historyIndex = 0;
            }
            else
            {
                int next = historyIndex + direction;
                if (next < 0)
                {
                    // 回到编辑行并恢复暂存输入
                    input.text = pendingInput ?? string.Empty;
                    pendingInput = null;
                    historyIndex = -1;
                    input.MoveTextEnd(false);
                    return;
                }
                if (next >= commandHistory.Count) return; // 已到最旧
                historyIndex = next;
            }

            input.text = commandHistory[commandHistory.Count - 1 - historyIndex];
            input.MoveTextEnd(false);
        }

        private void OnDestroy()
        {
            // 清理事件监听器，防止内存泄漏
            if (clear != null) clear.onClick.RemoveAllListeners();
            if (close != null) close.onClick.RemoveAllListeners();
            if (input != null) input.onEndEdit.RemoveAllListeners();
        }

        //打开窗口
        public void OpenWindow()
        {
            UISystem.Instance.OpenWindow(openWindow);
        }

        //关闭窗口
        public void CloseWindow()
        {
            UISystem.Instance.CloseWindow(closeWindow);
        }

        public void OnEndEdit(string text)
        {
            command = text;
            // 空命令不创建空条目
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            // 记录命令历史（与最近一条相同则跳过，避免重复），容量限制防无限增长
            if (commandHistory.Count == 0 || commandHistory[commandHistory.Count - 1] != text)
            {
                commandHistory.Add(text);
                if (commandHistory.Count > MaxHistoryCount)
                {
                    commandHistory.RemoveAt(0);
                }
            }
            historyIndex = -1;
            pendingInput = null;
            // 提交后清空输入框（设置 text 只触发 onValueChanged 不会递归 onEndEdit）
            if (input != null) input.text = string.Empty;

            /*"TestTerminal 2 2"*/
            // terminalRequest 仅在编辑器/开发构建中由 TerminalSystem 创建，
            // Release 构建或系统未初始化时为 null，必须判空避免 NRE
            var terminalRequest = TerminalSystem.Instance?.terminalRequest;
            if (terminalRequest != null)
            {
                terminalRequest.ParseCommand(command);
            }
            else
            {
                Log.Warning("TerminalUIPlane.OnEndEdit: terminalRequest 为 null（终端仅在编辑器/开发构建可用），已跳过命令解析");
            }
            CreateItem(command);
        }

        public void CreateItem(string str)
        {
            if (root == null)
            {
                return;
            }
            if (itemGo == null)
            {
                return;
            }

            // 限制最大条目数，超过上限时删除最旧的条目。
            // 必须跳过模板 itemGo：若模板恰好是 root 的第一个子节点，
            // 直接 Destroy(GetChild(0)) 会销毁模板，之后 Instantiate(itemGo) 抛 MissingReferenceException。
            if (root.transform.childCount >= MaxTerminalItems)
            {
                for (int i = 0; i < root.transform.childCount; i++)
                {
                    var child = root.transform.GetChild(i);
                    if (child.gameObject == itemGo) continue; // 跳过模板
                    Destroy(child.gameObject);
                    break;
                }
            }

            GameObject @object = Instantiate(itemGo, root.transform, false);
            // 保持与模板一致的本地变换（原实现按世界原点创建再重挂载，位置随父层级变换错乱）
            @object.transform.localScale = Vector3.one;
            var terminalItem = @object.GetComponent<TerminalItem>();
            if (terminalItem != null)
            {
                terminalItem.SetText(str);
            }

            // 限帧合并布局重建：本帧只排一次，避免高频刷屏时的 O(n²) 全量重建
            if (!layoutRebuildPending)
            {
                layoutRebuildPending = true;
                RebuildLayoutNextFrameAsync().Forget();
            }
        }

        /// <summary>下一帧统一重建一次布局（合批）</summary>
        private async UniTaskVoid RebuildLayoutNextFrameAsync()
        {
            await UniTask.Yield(PlayerLoopTiming.PostLateUpdate);
            layoutRebuildPending = false;
            if (root != null && root.TryGetComponent<RectTransform>(out var rootRt))
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(rootRt);
            }
        }
    }
}
