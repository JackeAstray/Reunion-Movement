using ReunionMovement.Common.Util;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ReunionMovement.Example
{
    /// <summary>
    /// 示例实现：如果指示器包含一个 Text（或 TextMeshPro），显示状态文字与进度。
    /// 仅作示例，可按需替换为更复杂的动画或图形实现。
    /// </summary>
    public class ExamplePullIndicator : PullIndicatorBase
    {
        public TextMeshProUGUI statusText;

        public override void OnPulling(float progress)
        {
            if (statusText != null)
            {
                statusText.text = progress >= 1f ? "Release to refresh" : $"Pulling {Mathf.RoundToInt(progress * 100)}%";
            }
        }

        /// <summary>
        /// 用户松手且达到阈值后进入刷新/加载状态，指示器应切换到加载动画或类似表现。
        /// </summary>
        public override void OnRefreshing()
        {
            if (statusText != null)
            {
                statusText.text = "Refreshing...";
            }
        }

        /// <summary>
        /// 外部通知刷新/加载完成，可播放完成动画或清理状态。
        /// </summary>
        public override void OnComplete()
        {
            if (statusText != null)
            {
                statusText.text = "Complete";
            }
        }
    }
}