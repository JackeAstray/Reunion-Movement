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

        // 初始状态与 GameOption 同步，避免图标与真实全屏状态不一致
        private bool isFullscreen = GameOption.CurrentOption.fullscreen;
        public Sprite fullscreenImg_01;
        public Sprite fullscreenImg_02;
        public Image fullscreenImg;
        public Button fullscreen;

        // public void Start()
        // {
        //     OnInit();
        // }

        public override void OnInit()
        {
            if (_initialized) return;
            _initialized = true;

            base.OnInit();

            if (Application.platform == RuntimePlatform.WebGLPlayer)
            {
                // 全屏按钮：初始图标与 GameOption 同步；浏览器侧状态变化（含 Esc/页面按钮退出）
                // 经 StartGame.OnFullscreenChanged 广播实时回刷，避免图标与真实状态不一致
                StartGame.OnFullscreenChanged += OnFullscreenChanged;
                RefreshFullscreenIcon();

                if (fullscreen != null)
                {
                    fullscreen.onClick.RemoveAllListeners();
                    fullscreen.onClick.AddListener(OnFullscreenButtonClick);
                }
                else
                {
                    Log.Warning("StartGameUIPlane: fullscreen 按钮未赋值，跳过全屏设置绑定");
                }
            }
            else
            {
                // 非 WebGL 平台隐藏全屏按钮
                if (fullscreen != null)
                {
                    fullscreen.gameObject.SetActive(false);
                }
            }


            // 生成代码空保护：logo 未赋值时给出明确告警而非 NRE（仅跳过 Logo 动画，不影响全屏按钮）
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

            // 窗口重新打开时按最新状态刷新（覆盖窗口关闭期间浏览器侧被 Esc 退出等变化）
            RefreshFullscreenIcon();
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
            // 退订静态事件：防止窗口销毁后仍被回调（静态事件持实例引用，不退订会阻止回收）
            StartGame.OnFullscreenChanged -= OnFullscreenChanged;

            // 清理 DOTween 动画，防止对象销毁后访问（含无目标 tween）
            logo1?.DOKill();
            logo2?.DOKill();
            logo1TransitionTween?.Kill();
            logo2TransitionTween?.Kill();
        }

        /// <summary>
        /// 全屏按钮点击：切换全屏并刷新图标。
        /// 必须由按钮点击触发 —— 浏览器要求全屏在用户手势内发起，代码直接调用会被拒绝。
        /// </summary>
        private void OnFullscreenButtonClick()
        {
            StartGame.SetFullscreen();
            RefreshFullscreenIcon();
        }

        /// <summary>全屏状态变化（StartGame 广播：主动切换 + 浏览器侧 Esc/页面按钮退出）</summary>
        private void OnFullscreenChanged(bool fullscreen)
        {
            RefreshFullscreenIcon();
        }

        /// <summary>按 GameOption 的全屏状态刷新全屏图标（WebGL 的 GameOption 已由浏览器回调同步）</summary>
        private void RefreshFullscreenIcon()
        {
            isFullscreen = GameOption.CurrentOption.fullscreen;
            if (fullscreenImg != null)
            {
                fullscreenImg.sprite = isFullscreen ? fullscreenImg_01 : fullscreenImg_02;
            }
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
