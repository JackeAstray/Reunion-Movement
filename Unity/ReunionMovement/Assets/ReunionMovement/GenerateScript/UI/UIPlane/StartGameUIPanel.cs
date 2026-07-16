using ReunionMovement.Common;
using ReunionMovement.Core.UIToolkit;
using ReunionMovement.Core.Resources;
using ReunionMovement.Core.Sound;
using Cysharp.Threading.Tasks;
using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace ReunionMovement.Core.UI
{
    /// <summary>
    /// 启动界面（UI Toolkit 版）—— 完全复刻 StartGameUIPlane (uGUI) 的动画效果。
    /// 
    /// 动画流程（与原版一模一样）：
    ///   1. logo1 淡入 0.45s
    ///   2. logo2 淡入 0.25s
    ///   3. 播放音效 300015
    ///   4. logo1 TransitionRate 0→1 持续 1.0s（线性）
    ///   5. logo2 TransitionRate 0→1 持续 0.9s（线性，同步开始）
    /// 
    /// 使用方式：
    ///   UIToolkitSystem.Instance.OpenPanel&lt;StartGameUIPanel&gt;("StartGame");
    /// </summary>
    public class StartGameUIPanel : UIToolkitPanel
    {
        #region UI 元素

        private VisualElement logo1;
        private VisualElement logo2;

        // TransitionRate 模拟：每个 logo 上的扫光遮罩层
        private VisualElement shine1;
        private VisualElement shine2;

        #endregion

        #region 图片路径（对应原版 Prefab 上拖拽赋值的 logo1/logo2）

        /// <summary>logo1 图片路径（相对于 Resources 目录，不含扩展名）</summary>
        private const string LOGO1_PATH = "UI/UIToolkit/Textures/logo1";

        /// <summary>logo2 图片路径</summary>
        private const string LOGO2_PATH = "UI/UIToolkit/Textures/logo2";

        #endregion

        #region 动画状态

        private bool animationPlaying;

        // 与 ImageEx.TransitionRate 对应的 0→1 值
        private float transitionRate1 = 0f;
        private float transitionRate2 = 0f;

        #endregion

        // ============================================================
        //  生命周期
        // ============================================================

        protected override void OnBind()
        {
            logo1  = Q<VisualElement>("logo1");
            logo2  = Q<VisualElement>("logo2");
            shine1 = Q<VisualElement>("shine1");
            shine2 = Q<VisualElement>("shine2");

            // 加载图片
            LoadLogoTexture(logo1, LOGO1_PATH);
            LoadLogoTexture(logo2, LOGO2_PATH);
        }

        public override async void OnOpen(object data = null)
        {
            base.OnOpen(data);

            if (animationPlaying) return;
            animationPlaying = true;

            await PlayLogoAnimation();
        }

        public override void OnClose()
        {
            animationPlaying = false;
            base.OnClose();
        }

        // ============================================================
        //  动画逻辑（与原版 StartGameUIPlane.OnInit 完全对应）
        // ============================================================
        private async UniTask PlayLogoAnimation()
        {
            // ============ 对应：logo1.DOFade(1, 0.45f) ============
            await FadeInAsync(logo1, 0.45f);
            if (!animationPlaying) return;

            // ============ 对应：logo2.DOFade(1, 0.25f) ============
            await FadeInAsync(logo2, 0.25f);
            if (!animationPlaying) return;

            // ============ 对应：SoundSystem.Instance.PlaySfx(300015) ============
            _ = SoundSystem.Instance.PlaySfx(300015);

            // ============ 对应：DOTween.To(() => logo1.TransitionRate, x => logo1.TransitionRate = x, 1f, 1f).SetEase(Ease.Linear) ============
            // ============ 同时：DOTween.To(() => logo2.TransitionRate, x => logo2.TransitionRate = x, 1f, 0.9f).SetEase(Ease.Linear) ============
            await UniTask.WhenAll(
                AnimateTransitionRateAsync(v => { transitionRate1 = v; UpdateShineVisual(shine1, logo1, v); }, 1f, 1.0f),
                AnimateTransitionRateAsync(v => { transitionRate2 = v; UpdateShineVisual(shine2, logo2, v); }, 1f, 0.9f)
            );

            animationPlaying = false;
        }

        // ============================================================
        //  动画工具方法（等价于 DOTween API）
        // ============================================================

        /// <summary>
        /// 等价于 ImageEx.DOFade(1, duration)
        /// </summary>
        private async UniTask FadeInAsync(VisualElement element, float duration)
        {
            if (element == null) return;

            float elapsed = 0f;
            while (elapsed < duration && animationPlaying)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                // DOTween 默认 OutQuad 缓动
                float eased = 1f - (1f - t) * (1f - t);
                element.style.opacity = eased;
                await UniTask.Yield(PlayerLoopTiming.Update);
            }
            element.style.opacity = 1f;
        }

        /// <summary>
        /// 等价于 DOTween.To(() => TransitionRate, x => TransitionRate = x, 1f, duration).SetEase(Ease.Linear)
        /// </summary>
        private async UniTask AnimateTransitionRateAsync(
            Action<float> setter, float target, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration && animationPlaying)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                // 原版 SetEase(Ease.Linear)
                setter(t * target);
                await UniTask.Yield(PlayerLoopTiming.Update);
            }
            setter(target);
        }

        // ============================================================
        //  TransitionRate 视觉模拟
        // ============================================================
        /// <summary>
        /// 用扫光遮罩模拟 ImageEx 的 TransitionRate Shader 效果。
        /// rate=0 → 遮罩全黑（logo 不可见/暗淡）
        /// rate=1 → 遮罩全透明（logo 完全显现）
        /// 
        /// 视觉效果：一道白色高光从左向右扫过 Logo，同时 Logo 从暗变亮。
        /// </summary>
        private void UpdateShineVisual(VisualElement shine, VisualElement logo, float rate)
        {
            if (shine == null || logo == null) return;

            // rate 驱动 logo 从暗变亮（模拟 shader transition 中的颜色变化）
            float brightness = Mathf.Lerp(0.3f, 1f, rate);
            logo.style.unityBackgroundImageTintColor = new StyleColor(
                new Color(brightness, brightness, brightness, 1f));

            // 扫光条从左到右移动
            float shineLeft = Mathf.Lerp(-20f, 120f, rate);
            shine.style.left = new StyleLength(Length.Percent(shineLeft));
            shine.style.opacity = rate < 0.05f ? 0f : (rate > 0.95f ? 0f : 0.6f);
        }

        // ============================================================
        //  图片加载
        // ============================================================
        private static void LoadLogoTexture(VisualElement element, string path)
        {
            if (element == null) return;

            var tex = ResourcesSystem.Instance.Load<Texture2D>(path, isCache: true);
            if (tex != null)
            {
                element.style.backgroundImage = new StyleBackground(tex);
            }
            else
            {
                Log.Warning("[StartGameUIPanel] 未找到图片: Resources/{0}", path);
            }
        }
    }
}
