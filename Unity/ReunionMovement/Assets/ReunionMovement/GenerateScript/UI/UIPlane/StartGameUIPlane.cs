//此脚本是由工具自动生成，请勿手动创建

using DG.Tweening;
using ReunionMovement.Common;
using ReunionMovement.Core.Sound;
using ReunionMovement.UI.ImageExtensions;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ReunionMovement.Core.UI
{
    public class StartGameUIPlane : UIController
    {
        string openWindow = UINames.StartGame;
        string closeWindow = UINames.StartGame;

        public ImageEx logo1;
        public ImageEx logo2;

        // 无目标 DOTween（DOTween.To(getter, setter, ...)）不受 logo.DOKill() 控制，必须持有引用手动 Kill
        private Tween logo1TransitionTween;
        private Tween logo2TransitionTween;

        private bool _initialized = false;

        // public void Start()
        // {
        //     OnInit();
        // }

        public override void OnInit()
        {
            if (_initialized) return;
            _initialized = true;

            base.OnInit();

            // 生成代码空保护：logo 未赋值时给出明确告警而非 NRE
            if (logo1 == null || logo2 == null)
            {
                Log.Error("StartGameUIPlane: logo1/logo2 未赋值，跳过 Logo 动画");
                return;
            }

            // 先杀残留动画，避免重复打开/重复 OnInit 时动画叠加
            logo1.DOKill();
            logo2.DOKill();
            logo1TransitionTween?.Kill();
            logo2TransitionTween?.Kill();

            logo1.DOFade(1, 0.45f).OnComplete(() =>
            {
                logo2.DOFade(1, 0.25f).OnComplete(() =>
                {
                    logo1.TransitionRate = 0f;
                    logo2.TransitionRate = 0f;

                    _ = SoundSystem.Instance.PlaySfx(300015);

                    logo1TransitionTween?.Kill();
                    logo2TransitionTween?.Kill();
                    logo1TransitionTween = DOTween.To(() => logo1.TransitionRate, x => logo1.TransitionRate = x, 1f, 1f).SetEase(Ease.Linear);
                    logo2TransitionTween = DOTween.To(() => logo2.TransitionRate, x => logo2.TransitionRate = x, 1f, 0.9f).SetEase(Ease.Linear);
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

        private void OnDestroy()
        {
            // 清理 DOTween 动画，防止对象销毁后访问（含无目标 tween）
            logo1?.DOKill();
            logo2?.DOKill();
            logo1TransitionTween?.Kill();
            logo2TransitionTween?.Kill();
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
