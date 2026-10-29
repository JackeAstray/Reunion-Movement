using DG.Tweening;
using DG.Tweening.Core;
using DG.Tweening.Plugins.Options;
using UnityEngine;

using ReunionMovement.Common;
namespace ReunionMovement.Utilities.UI.ImageExtensions
{
    /// <summary>
    /// <see cref="ImageEx"/> 的 DOTween 快捷扩展。
    ///
    /// <para>
    /// 为什么要自己写一份：框架里只用到 <c>DOFade</c> 和 <c>DOKill</c> 两个扩展，其中
    /// <c>DOFade(this Image …)</c> 来自 DOTween 的 <c>Plugins/DOTween/Modules/DOTweenModuleUI.cs</c>，
    /// 而那个目录是否自成程序集取决于 <c>DOTween.Modules.asmdef</c> —— DOTween 的 Setup 面板
    /// 会按它的「Create ASMDEF」开关**增删**这个文件。一旦被删，模块代码会落到 Assembly-CSharp，
    /// 而 asmdef 程序集（如 <c>ReunionMovement.GenerateScript.UI</c>）看不到 Assembly-CSharp，
    /// 调用处就会报 <c>CS1929: 'ImageEx' does not contain a definition for 'DOFade'</c>。
    /// </para>
    ///
    /// <para>
    /// 这里在框架自己的程序集里提供一个更具体的重载（<c>this ImageEx</c>），
    /// 无论 DOTween 模块是否被拆成独立程序集都能解析到；两者同时存在时也更精确，不会歧义。
    /// <c>DOKill</c> 来自 DOTween 核心的 <c>ShortcutExtensions</c>，不依赖模块程序集，无需重复提供。
    /// </para>
    /// </summary>
    public static class ImageExTweenExtensions
    {
        /// <summary>
        /// 淡入淡出：把 <see cref="ImageEx"/> 的颜色 alpha 补间到 <paramref name="endValue"/>。
        /// 语义与 DOTween 的 <c>Image.DOFade</c> 一致，只是不依赖 DOTween 的模块程序集。
        /// </summary>
        /// <param name="target">目标 ImageEx</param>
        /// <param name="endValue">目标 alpha（0–1）</param>
        /// <param name="duration">时长（秒）</param>
        public static TweenerCore<Color, Color, ColorOptions> DOFade(this ImageEx target, float endValue, float duration)
        {
            // 与 DOTween 自带 Image.DOFade 的实现保持完全一致（ToAlpha + SetTarget）
            TweenerCore<Color, Color, ColorOptions> tween = DOTween.ToAlpha(
                () => target.color, x => target.color = x, endValue, duration);
            tween.SetTarget(target);
            return tween;
        }
    }
}
