//此脚本是由工具自动生成，请勿手动创建

using ReunionMovement.Common;
using ReunionMovement.Common.Util;
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
        string openWindow = "PopupUIPlane";
        string closeWindow = "PopupUIPlane";

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

            // 验证关键组件是否已赋值
            if (titleStr == null) Log.Error("PopupUIPlane: titleStr 未赋值!");
            if (containerStr == null) Log.Error("PopupUIPlane: containerStr 未赋值!");
            if (cancelStr == null) Log.Error("PopupUIPlane: cancelStr 未赋值!");
            if (confirmStr == null) Log.Error("PopupUIPlane: confirmStr 未赋值!");
            if (closeBtn == null) Log.Error("PopupUIPlane: closeBtn 未赋值!");
            if (cancelBtn == null) Log.Error("PopupUIPlane: cancelBtn 未赋值!");
            if (confirmBtn == null) Log.Error("PopupUIPlane: confirmBtn 未赋值!");
        }

        public override void OnOpen(params object[] args)
        {
            base.OnOpen(args);

            if (confirmBtn != null) confirmBtn.gameObject.SetActive(false);
            if (cancelBtn != null) cancelBtn.gameObject.SetActive(false);

            switch (args.Length)
            {
                case 0:
                    Log.Error("PopupUIPlane: 未传入任何参数，窗口内容为空。");
                    break;
                case 1:
                    if (containerStr != null) containerStr.text = args[0] as string;
                    break;
                case 2:
                    if (titleStr != null) titleStr.text = args[0] as string;
                    if (containerStr != null) containerStr.text = args[1] as string;
                    break;
                case 3:
                    if (titleStr != null) titleStr.text = args[0] as string;
                    if (containerStr != null) containerStr.text = args[1] as string;
                    if (confirmStr != null) confirmStr.text = args[2] as string;
                    if (confirmBtn != null) confirmBtn.gameObject.SetActive(true);
                    break;
                case 4:
                    if (titleStr != null) titleStr.text = args[0] as string;
                    if (containerStr != null) containerStr.text = args[1] as string;
                    if (confirmStr != null) confirmStr.text = args[2] as string;
                    if (cancelStr != null) cancelStr.text = args[3] as string;
                    if (confirmBtn != null) confirmBtn.gameObject.SetActive(true);
                    if (cancelBtn != null) cancelBtn.gameObject.SetActive(true);
                    break;
                case 5:
                    if (titleStr != null) titleStr.text = args[0] as string;
                    if (containerStr != null) containerStr.text = args[1] as string;
                    if (confirmStr != null) confirmStr.text = args[2] as string;
                    if (cancelStr != null) cancelStr.text = args[3] as string;
                    confirmAction = args[4] as Action;
                    if (confirmBtn != null) confirmBtn.gameObject.SetActive(true);
                    if (cancelBtn != null) cancelBtn.gameObject.SetActive(true);
                    break;
                case 6:
                    if (titleStr != null) titleStr.text = args[0] as string;
                    if (containerStr != null) containerStr.text = args[1] as string;
                    if (confirmStr != null) confirmStr.text = args[2] as string;
                    if (cancelStr != null) cancelStr.text = args[3] as string;
                    confirmAction = args[4] as Action;
                    cancelAction = args[5] as Action;
                    if (confirmBtn != null) confirmBtn.gameObject.SetActive(true);
                    if (cancelBtn != null) cancelBtn.gameObject.SetActive(true);
                    break;
                default:
                    Log.Error("参数错误");
                    break;
            }

            if (cancelBtn != null)
            {
                cancelBtn.onClick.RemoveAllListeners();
                cancelBtn.onClick.AddListener(() =>
                {
                    cancelAction?.Invoke();
                    closeWindow = "PopupUIPlane";
                    CloseWindow();
                });
            }

            if (confirmBtn != null)
            {
                confirmBtn.onClick.RemoveAllListeners();
                confirmBtn.onClick.AddListener(() =>
                {
                    confirmAction?.Invoke();
                    closeWindow = "PopupUIPlane";
                    CloseWindow();
                });
            }

            if (closeBtn != null)
            {
                closeBtn.onClick.RemoveAllListeners();
                closeBtn.onClick.AddListener(() =>
                {
                    closeWindow = "PopupUIPlane";
                    CloseWindow();
                });
            }
        }

        public override void OnSet(params object[] args)
        {
            base.OnSet(args);
        }

        public override void OnClose()
        {
            // 清理按钮监听器，防止多次开关窗口时累积
            if (cancelBtn != null) cancelBtn.onClick.RemoveAllListeners();
            if (confirmBtn != null) confirmBtn.onClick.RemoveAllListeners();
            if (closeBtn != null) closeBtn.onClick.RemoveAllListeners();

            base.OnClose();
        }

        private void OnDestroy()
        {
            // 清理事件引用，防止内存泄漏
            cancelAction = null;
            confirmAction = null;
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
