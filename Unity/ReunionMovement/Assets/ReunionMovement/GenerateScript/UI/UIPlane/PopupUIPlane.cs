using Cysharp.Threading.Tasks;
//此脚本是由工具自动生成，请勿手动创建

using ReunionMovement.Common;
using ReunionMovement.Framework.Modules;
using ReunionMovement.Utilities;
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ReunionMovement.Framework.Runtime.UI
{
    /// <summary>PopupUIPlane 的打开参数示例（值类型 + IWindowArgs，调用处编译期检查）。</summary>
    public readonly struct PopupArgs : IWindowArgs
    {
        public readonly string Title;
        public readonly string Message;
    public readonly string ConfirmText;
    public readonly string CancelText;
    public readonly Action ConfirmAction;
    public readonly Action CancelAction;

    public PopupArgs(string title, string message, string confirmText = null, string cancelText = null, Action confirmAction = null, Action cancelAction = null)
        {
            Title = title;
            Message = message;
        ConfirmText = confirmText;
        CancelText = cancelText;
        ConfirmAction = confirmAction;
        CancelAction = cancelAction;
        }

        public override string ToString() => Title + " | " + Message;
    }

    [UIWindow("PopupUIPlane", Layer = PanelType.TipsUI, Modal = true)]
    public class PopupUIPlane : UIController, IWindowArgsReceiver<PopupArgs>
    {
        /// <summary>打开时收到的参数（无参打开时为 default）。</summary>
        public PopupArgs Args { get; private set; }
        string openWindow = nameof(PopupUIPlane);
        string closeWindow = nameof(PopupUIPlane);

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

        public override void OnOpen()
        {
            base.OnOpen();
            // 参数不再从 object[] 里按位置取：由 OnWindowArgs(PopupArgs) 强类型注入。
            // 这里只做"上一次打开残留"的清理与按钮接线。
            confirmAction = null;
            cancelAction = null;
            WireButtons();
        }

        /// <summary>强类型参数入口（由 UIRouter / OpenWindowAsync&lt;TWindow, TArgs&gt; 调用）。</summary>
        public void OnWindowArgs(PopupArgs args)
        {
            Args = args;
            if (!string.IsNullOrEmpty(args.Title) && titleStr != null) titleStr.text = args.Title;
            if (!string.IsNullOrEmpty(args.Message) && containerStr != null) containerStr.text = args.Message;
            if (!string.IsNullOrEmpty(args.ConfirmText) && confirmStr != null) confirmStr.text = args.ConfirmText;
            if (!string.IsNullOrEmpty(args.CancelText) && cancelStr != null) cancelStr.text = args.CancelText;

            confirmAction = args.ConfirmAction;
            cancelAction = args.CancelAction;
            if (args.ConfirmAction != null && confirmBtn != null) confirmBtn.gameObject.SetActive(true);
            if (args.CancelAction != null && cancelBtn != null) cancelBtn.gameObject.SetActive(true);

            Debug.Log("[PopupUIPlane] 收到参数: " + args);
        }

        /// <summary>按钮接线（幂等：每次打开先 RemoveAllListeners 再挂）。</summary>
        private void WireButtons()
        {
            if (cancelBtn != null)
            {
                cancelBtn.onClick.RemoveAllListeners();
                cancelBtn.onClick.AddListener(() =>
                {
                    cancelAction?.Invoke();
                    CloseWindow();
                });
            }

            if (confirmBtn != null)
            {
                confirmBtn.onClick.RemoveAllListeners();
                confirmBtn.onClick.AddListener(() =>
                {
                    confirmAction?.Invoke();
                    CloseWindow();
                });
            }

            if (closeBtn != null)
            {
                closeBtn.onClick.RemoveAllListeners();
                closeBtn.onClick.AddListener(() =>
                {
                    CloseWindow();
                });
            }
        }

        public override void OnClose()
        {
            // 清理按钮监听器，防止多次开关窗口时累积
            if (cancelBtn != null) cancelBtn.onClick.RemoveAllListeners();
            if (confirmBtn != null) confirmBtn.onClick.RemoveAllListeners();
            if (closeBtn != null) closeBtn.onClick.RemoveAllListeners();

            // 清理回调引用，防止重新打开（未传 Action）时触发过期回调
            cancelAction = null;
            confirmAction = null;

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
            UIRouter.PushAsync(openWindow).Forget();
        }

        //关闭窗口
        public void CloseWindow()
        {
            UISystem.Instance.CloseWindow(closeWindow);
        }
    }
}
