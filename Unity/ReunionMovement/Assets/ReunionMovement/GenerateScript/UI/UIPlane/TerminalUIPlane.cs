//此脚本是由工具自动生成，请勿手动创建

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
        string openWindow;
        string closeWindow;

        string command;
        public Button clear;    //清除
        public Button close;    //关闭
        public TMP_InputField input;//输入

        public GameObject root;
        public GameObject itemGo;

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

        public void OnDestroy()
        {

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
            TerminalSystem.Instance.terminalRequest.ParseCommand(command);
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

            GameObject @object = Instantiate(itemGo, Vector3.zero, Quaternion.identity);
            @object.transform.SetParent(root.transform);
            @object.GetComponent<TerminalItem>().SetText(str);

            LayoutRebuilder.ForceRebuildLayoutImmediate(root.GetComponent<RectTransform>());
        }
    }
}
