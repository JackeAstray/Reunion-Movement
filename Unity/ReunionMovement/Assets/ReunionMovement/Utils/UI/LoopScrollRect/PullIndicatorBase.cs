using UnityEngine;

namespace ReunionMovement.Common.Util
{
    /// <summary>
    /// 拉动指示器基类，负责显示“拉动中 / 刷新中 / 完成”等状态。
    /// 将此脚本挂在 pullStartIndicatorPrefab / pullEndIndicatorPrefab 的根节点上。
    /// 子类重写具体视觉逻辑（例如显示进度条、切换文字或播放动画）。
    /// </summary>
    public abstract class PullIndicatorBase : MonoBehaviour
    {
        /// <summary>
        /// 正在拖拽过程中，progress 范围为 0..1（当 progress >= 1 表示已达到触发阈值）。
        /// </summary>
        public virtual void OnPulling(float progress) { }

        /// <summary>
        /// 用户松手且达到阈值后进入刷新/加载状态，指示器应切换到加载动画或类似表现。
        /// </summary>
        public virtual void OnRefreshing() { }

        /// <summary>
        /// 外部通知刷新/加载完成，可播放完成动画或清理状态。
        /// </summary>
        public virtual void OnComplete() { }
    }
}