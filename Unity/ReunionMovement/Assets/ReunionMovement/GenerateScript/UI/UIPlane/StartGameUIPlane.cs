//此脚本是由工具自动生成，请勿手动创建

using DG.Tweening;
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

        public Image logo1;
        public Image logo2;

        //public void Start()
        //{
        //    OnInit();
        //}

        public override void OnInit()
        {
            base.OnInit();

            logo1.DOFade(1, 1f).OnComplete(() =>
            {
                logo2.DOFade(1, 1f).OnComplete(() =>
                {
                    logo1.DOFade(0, 1f);
                    logo2.DOFade(0, 1f).OnComplete(() =>
                    {

                    });
                });
            });
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
