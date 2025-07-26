//此脚本是由工具自动生成，请勿手动创建

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ReunionMovement.Core.UI
{
    public class StartGameUIPlane : UIController
    {
        string openWindow;
        string closeWindow;

        public override void OnInit()
        {
            base.OnInit();
        }

        public override void OnOpen(params object[] args)
        {
            base.OnOpen(args);
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
