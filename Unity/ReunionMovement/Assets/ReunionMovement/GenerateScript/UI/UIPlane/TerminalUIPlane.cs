//此脚本是由工具自动生成，请勿手动创建

using ReunionMovement.Common;
using ReunionMovement.Common.Util;
using ReunionMovement.Core.Terminal;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ReunionMovement.Core.UI
{
    public class TerminalUIPlane : UIController
    {
        string openWindow = "TerminalUIPlane";
        string closeWindow = "TerminalUIPlane";

        string command;
        public Button clear;    //清除
        public Button close;    //关闭
        public TMP_InputField input;//输入

        public GameObject root;
        public GameObject itemGo;
        // 终端条目的最大数量，防止无限增长导致内存泄漏
        private const int MaxTerminalItems = 100;

        public override void OnInit()
        {
            base.OnInit();

            clear.onClick.RemoveAllListeners();
            close.onClick.RemoveAllListeners();
            input.onEndEdit.RemoveAllListeners();

            command = "";

            clear.onClick.AddListener(() =>
            {
                root.ClearChild();
            });

            close.onClick.AddListener(() =>
            {
                UISystem.Instance.CloseWindow("TerminalUIPlane");
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

            if (args.Length > 0)
            {
                switch (args[0].ToString())
                {
                    case "CreateItem":
                        if (args.Length >= 2)
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

            GameObject @object = Instantiate(itemGo, Vector3.zero, Quaternion.identity);
            @object.transform.SetParent(root.transform);
            var terminalItem = @object.GetComponent<TerminalItem>();
            if (terminalItem != null)
            {
                terminalItem.SetText(str);
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(root.GetComponent<RectTransform>());
        }
    }
}
