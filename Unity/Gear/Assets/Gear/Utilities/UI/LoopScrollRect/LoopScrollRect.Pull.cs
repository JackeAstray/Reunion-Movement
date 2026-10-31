using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.EventSystems;

using Gear.Common;
namespace Gear.Utilities
{
    public partial class LoopScrollRect
    {
        /// <summary>
        /// 外部在刷新/加载完成后调用以允许下一次触发。
        /// - CompletePullStart(): 完成起始端（下拉/左拉）动作
        /// - CompletePullEnd(): 完成末端（上拉/右拉）动作
        /// 调用后可以选择刷新数据源（如 totalCount 改变后调用 Initialize/ForceRefresh）
        /// </summary>
        public void CompletePullStart()
        {
            isActionInProgressStart = false;
            // 通知指示器完成动画
            pullStartIndicatorComp?.OnComplete();
            HidePullStartIndicator();
            // 自动回弹：content 停在拉出位时平滑回位到起始边界（外部无需手动复位位置）
            ReboundToEdge(true);
        }

        /// <summary>
        /// 刷新/加载完成后回弹到内容边界（起始端回 0 / 末端回 maxOffset），带平滑动画。
        /// 仅在越界（拉出位）时执行，正常位置不动作。
        /// </summary>
        private void ReboundToEdge(bool toStart)
        {
            if (!isActiveAndEnabled || content == null || viewport == null) return;
            float viewSize = (direction == Direction.Vertical) ? viewport.rect.height : viewport.rect.width;
            float contentSize = (direction == Direction.Vertical) ? content.rect.height : content.rect.width;
            float maxOffset = Mathf.Max(0f, contentSize - viewSize);

            float offset = (direction == Direction.Vertical) ? content.anchoredPosition.y : -content.anchoredPosition.x;
            if (toStart && offset >= 0f) return;            // 已回位
            if (!toStart && offset <= maxOffset) return;    // 已回位

            // 目标位置：边界 + 保留当前 firstIndex（回弹不改变数据位置）
            Vector2 targetPos = content.anchoredPosition;
            if (direction == Direction.Vertical)
            {
                targetPos.y = toStart ? 0f : maxOffset;
            }
            else
            {
                targetPos.x = toStart ? 0f : -maxOffset;
            }

            StopScrollCoroutineIfAny();
            SmoothScrollToAsync(targetPos, currentFirstIndex, pageSnapDuration).Forget();
        }

        public void CompletePullEnd()
        {
            isActionInProgressEnd = false;
            pullEndIndicatorComp?.OnComplete();
            HidePullEndIndicator();
            // 自动回弹：content 停在拉出位时平滑回位到末端边界
            ReboundToEdge(false);
        }

        // 显示/隐藏指示器的辅助方法
        void ShowPullStartIndicator()
        {
            if (pullStartIndicatorPrefab == null) return;
            if (pullStartIndicatorInstance == null)
            {
                // 将指示器生成在 viewport 下，避免 content 的子项重建或布局影响它的位置
                RectTransform parent = viewport != null ? viewport : content;
                pullStartIndicatorInstance = Instantiate(pullStartIndicatorPrefab, parent);
                // 获取脚本引用（如果存在）
                pullStartIndicatorComp = pullStartIndicatorInstance.GetComponent<PullIndicatorBase>();
            }
            else
            {
                // 复用已创建的实例（Hide 仅隐藏不销毁），避免反复 Instantiate/Destroy 的 GC 压力
                pullStartIndicatorInstance.gameObject.SetActive(true);
            }
            pullStartIndicatorInstance.SetAsLastSibling();
            // 保证对齐
            // 将起始指示器固定在 viewport 的上边缘
            pullStartIndicatorInstance.pivot = new Vector2(0.5f, 1f);
            pullStartIndicatorInstance.anchorMin = new Vector2(0.5f, 1f);
            pullStartIndicatorInstance.anchorMax = new Vector2(0.5f, 1f);
            // 设置尺寸和位置
            if (direction == Direction.Vertical)
            {
                // 与第 813 行保持同一降级惯例：viewport 为空时退回 content 取几何量。
                // 此前这两处裸用 viewport.rect，与 813 行的降级自相矛盾（viewport 为空即 NRE）。
                // content 非空由调用方保证（TryTriggerPullOnRelease 与 OnScroll 都已判过 content）
                RectTransform geo = viewport != null ? viewport : content;
                float width = geo.rect.width;
                if (width <= 0) width = pullStartIndicatorPrefab.rect.width;
                pullStartIndicatorInstance.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
                // 高度使用预制体高度或 itemSize 的一部分
                float h = pullStartIndicatorPrefab.rect.height > 0 ? pullStartIndicatorPrefab.rect.height : itemSize;
                pullStartIndicatorInstance.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, h);
                // 固定在上边缘
                pullStartIndicatorInstance.anchoredPosition = new Vector2(0f, 0f);
            }
            else
            {
                // Horizontal: 固定在左侧中间
                RectTransform geo = viewport != null ? viewport : content;
                float height = geo.rect.height;
                if (height <= 0) height = pullStartIndicatorPrefab.rect.height;
                pullStartIndicatorInstance.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
                float w = pullStartIndicatorPrefab.rect.width > 0 ? pullStartIndicatorPrefab.rect.width : itemSize;
                pullStartIndicatorInstance.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, w);
                // 左侧中间
                pullStartIndicatorInstance.pivot = new Vector2(0f, 0.5f);
                pullStartIndicatorInstance.anchorMin = new Vector2(0f, 0.5f);
                pullStartIndicatorInstance.anchorMax = new Vector2(0f, 0.5f);
                pullStartIndicatorInstance.anchoredPosition = new Vector2(0f, 0f);
            }
            // 不依赖 pooledItems 的位置来定位；它固定在 viewport 边缘
        }

        void ShowPullEndIndicator()
        {
            if (pullEndIndicatorPrefab == null) return;
            if (pullEndIndicatorInstance == null)
            {
                RectTransform parent = viewport != null ? viewport : content;
                pullEndIndicatorInstance = Instantiate(pullEndIndicatorPrefab, parent);
                // 获取脚本引用
                pullEndIndicatorComp = pullEndIndicatorInstance.GetComponent<PullIndicatorBase>();
            }
            else
            {
                // 复用已创建的实例（Hide 仅隐藏不销毁），避免反复 Instantiate/Destroy 的 GC 压力
                pullEndIndicatorInstance.gameObject.SetActive(true);
            }
            pullEndIndicatorInstance.SetAsLastSibling();
            // 将末端指示器固定在 viewport 的下边缘
            pullEndIndicatorInstance.pivot = new Vector2(0.5f, 0f);
            pullEndIndicatorInstance.anchorMin = new Vector2(0.5f, 0f);
            pullEndIndicatorInstance.anchorMax = new Vector2(0.5f, 0f);
            if (direction == Direction.Vertical)
            {
                float width = (viewport != null ? viewport : content).rect.width;   // 与 868 行的降级一致
                if (width <= 0) width = pullEndIndicatorPrefab.rect.width;
                pullEndIndicatorInstance.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
                float h = pullEndIndicatorPrefab.rect.height > 0 ? pullEndIndicatorPrefab.rect.height : itemSize;
                pullEndIndicatorInstance.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, h);
                pullEndIndicatorInstance.anchoredPosition = new Vector2(0f, 0f);
            }
            else
            {
                // Horizontal: 固定在右侧中间
                float height = (viewport != null ? viewport : content).rect.height;  // 与 868 行的降级一致
                if (height <= 0) height = pullEndIndicatorPrefab.rect.height;
                pullEndIndicatorInstance.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
                float w = pullEndIndicatorPrefab.rect.width > 0 ? pullEndIndicatorPrefab.rect.width : itemSize;
                pullEndIndicatorInstance.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, w);
                // 右侧中间
                pullEndIndicatorInstance.pivot = new Vector2(1f, 0.5f);
                pullEndIndicatorInstance.anchorMin = new Vector2(1f, 0.5f);
                pullEndIndicatorInstance.anchorMax = new Vector2(1f, 0.5f);
                pullEndIndicatorInstance.anchoredPosition = new Vector2(0f, 0f);
            }
            // 固定在 viewport 下边缘
        }

        /// <summary>
        /// 隐藏起始端指示器实例（保留复用，避免反复 Instantiate/Destroy）。
        /// </summary>
        void HidePullStartIndicator()
        {
            if (pullStartIndicatorInstance != null)
            {
                pullStartIndicatorInstance.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// 隐藏末端指示器实例（保留复用，避免反复 Instantiate/Destroy）。
        /// </summary>
        void HidePullEndIndicator()
        {
            if (pullEndIndicatorInstance != null)
            {
                pullEndIndicatorInstance.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// 指示器固定在 viewport 边缘，不随 pooledItems 调整位置
        /// </summary>
        void UpdatePullIndicatorPositions()
        {
            // 对于现在的实现，指示器固定锚点在 viewport 的上下边缘，通常不需要在此更新位置。
            // 保留空实现以便未来扩展。
        }
    }
}
