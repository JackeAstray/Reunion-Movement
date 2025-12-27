//此脚本是由工具自动生成，请勿手动创建

using DG.Tweening;
using ReunionMovement.Common;
using ReunionMovement.Core.Sound;
using ReunionMovement.UI.ImageExtensions;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using static UnityEngine.GraphicsBuffer;

namespace ReunionMovement.Core.UI
{
    public class StartGameUIPlane : UIController
    {
        string openWindow;
        string closeWindow;

        public ImageEx logo1;
        public ImageEx logo2;

        public void Start()
        {
            OnInit();
        }

        public override void OnInit()
        {
            base.OnInit();

            logo1.DOFade(1, 0.45f).OnComplete(() =>
            {
                logo2.DOFade(1, 0.25f).OnComplete(() =>
                {
                    logo1.TransitionRate = 0f;
                    logo2.TransitionRate = 0f;

                    SoundSystem.Instance.PlaySfx(300015);

                    DOTween.To(() => logo1.TransitionRate, x => logo1.TransitionRate = x, 1f, 1f).SetEase(Ease.Linear);
                    DOTween.To(() => logo2.TransitionRate, x => logo2.TransitionRate = x, 1f, 0.9f).SetEase(Ease.Linear);
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
