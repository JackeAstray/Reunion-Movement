//此脚本是由工具自动生成，请勿手动创建

using ReunionMovement.Common;
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ReunionMovement.Core.UI
{
    public class PopupUIPlane : UIController
    {
        string openWindow;
        string closeWindow;

        public TextMeshProUGUI titleStr;
        public TextMeshProUGUI containerStr;
        public TextMeshProUGUI cancelStr;
        public TextMeshProUGUI confirmStr;

        public Button closeBtn;
        public Button cancelBtn;
        public Button confirmBtn;

        public Action cancelAction;
        public Action confirmAction;

        public override void OnInit()
        {
            base.OnInit();
        }

        public override void OnOpen(params object[] args)
        {
            base.OnOpen(args);

            switch (args.Length)
            {
                case 1:
                    containerStr.text = args[0] as string;
                    break;
                case 2:
                    titleStr.text = args[0] as string;
                    containerStr.text = args[1] as string;
                    break;
                case 3:
                    titleStr.text = args[0] as string;
                    containerStr.text = args[1] as string;
                    confirmStr.text = args[2] as string;
                    break;
                case 4:
                    titleStr.text = args[0] as string;
                    containerStr.text = args[1] as string;
                    confirmStr.text = args[2] as string;
                    cancelStr.text = args[3] as string;
                    break;
                case 5:
                    titleStr.text = args[0] as string;
                    containerStr.text = args[1] as string;
                    confirmStr.text = args[2] as string;
                    cancelStr.text = args[3] as string;
                    confirmAction = args[4] as Action;
                    break;
                case 6:
                    titleStr.text = args[0] as string;
                    containerStr.text = args[1] as string;
                    confirmStr.text = args[2] as string;
                    cancelStr.text = args[3] as string;
                    confirmAction = args[4] as Action;
                    cancelAction = args[5] as Action;
                    break;
                default:
                    Log.Error("参数错误");
                    break;
            }

            cancelBtn.onClick.RemoveAllListeners();
            confirmBtn.onClick.RemoveAllListeners();
            closeBtn.onClick.RemoveAllListeners();

            cancelBtn.onClick.AddListener(() =>
            {
                cancelAction?.Invoke();
                closeWindow = "PopupUIPlane";
                CloseWindow();
            });

            confirmBtn.onClick.AddListener(() =>
            {
                confirmAction?.Invoke();
                closeWindow = "PopupUIPlane";
                CloseWindow();
            });

            closeBtn.onClick.AddListener(() =>
            {
                closeWindow = "PopupUIPlane";
                CloseWindow();
            });
        }

        public override void OnSet(params object[] args)
        {
            base.OnSet(args);
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
    }
}
