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
        /// 根据 data index 跳转到对应位置。
        /// index: 目标数据索引（以数据项为基准的第一个可见项索引）
        /// animated: 是否使用平滑滚动（协程），false 则立即跳转并刷新可见项。
        /// duration: 平滑滚动时的时长（秒）。
        /// 说明：跳转会将第一个可见项设置为 index（若 index 太大，会被限制为可用的最大 firstIndex）。
        /// </summary>
        public void JumpToIndex(int index, bool animated = false, float duration = 0.25f)
        {
            if (!initialized || !isActiveAndEnabled || totalCount == 0 || pooledItems.Count == 0) return;
            index = Mathf.Clamp(index, 0, totalCount - 1);

            int effectiveTotal = enableLooping && totalCount > 0 ? totalCount * LOOP_CYCLES : totalCount;

            // 当启用分页时，最大 firstIndex 应基于每页显示的项数（itemsPerPage），
            // 而不是 pooledItems/visibleCount。否则当 pooledItems > itemsPerPage 时，
            // 最后一页可能被错误地 clamp 掉，导致无法到达最后一页。
            int maxFirst;
            if (enablePaging && itemsPerPage > 0 && !enableLooping)
            {
                int safeItemsPerPage = Mathf.Max(1, itemsPerPage);
                maxFirst = ((totalCount - 1) / safeItemsPerPage) * safeItemsPerPage;
            }
            else
            {
                maxFirst = Math.Max(0, effectiveTotal - 1);
            }

            int targetFirst = Mathf.Clamp(index, 0, maxFirst);

            // 如果启用了循环并且传入的是数据索引（0..totalCount-1），我们应该把目标放到中间 cycle
            if (enableLooping)
            {
                // 把目标 index 放到中间 cycle
                int middleBase = totalCount;
                targetFirst = Mathf.Clamp(middleBase + index, 0, maxFirst);
            }

            float targetOffset = Mathf.Clamp(targetFirst * Step, 0f, MaxOffset);
            targetFirst = Mathf.FloorToInt(targetOffset / Step);
            Vector2 targetPos = content.anchoredPosition;
            if (direction == Direction.Vertical)
            {
                targetPos.y = targetOffset;
            }
            else
            {
                targetPos.x = -targetOffset;
            }

            // 立即生效
            if (!animated || duration <= 0f)
            {
                StopScrollCoroutineIfAny();
                // 在程序化设置位置时忽略 OnScroll 回调
                isRecentering = true;
                try
                {
                    content.anchoredPosition = targetPos;
                    // 停止惯性
                    if (scrollRect != null)
                    {
                        scrollRect.velocity = Vector2.zero;
                    }
                    currentFirstIndex = targetFirst;
                    RefreshVisible();
                }
                finally
                {
                    isRecentering = false;
                }

                // 更新 page 并通知（循环模式下不启用 page 回调）
                if (enablePaging && itemsPerPage > 0 && !enableLooping)
                {
                    int page = currentFirstIndex / Mathf.Max(1, itemsPerPage);
                    if (page != currentPage)
                    {
                        currentPage = page;
                        onPageChanged?.Invoke(currentPage);
                    }
                }
            }
            else
            {
                // 平滑滚动
                StopScrollCoroutineIfAny();
                SmoothScrollToAsync(targetPos, targetFirst, duration).Forget();
            }
        }

        /// <summary>
        /// 跳到下一页
        /// </summary>
        public void NextPage(bool animated = true)
        {
            if (!enablePaging || totalCount == 0) return;
            int safeItemsPerPage = Mathf.Max(1, itemsPerPage);
            if (enableLooping)
            {
                // 循环模式：currentPage 不更新（页面概念模糊），按页索引计算会恒跳同一页。
                // 改为基于当前可见数据位置推进一页（JumpToIndex 会把数据索引映射到 middle cycle）
                int currentDataIndex = ((currentFirstIndex % totalCount) + totalCount) % totalCount;
                int targetDataIndex = (currentDataIndex + safeItemsPerPage) % totalCount;
                JumpToIndex(targetDataIndex, animated, pageSnapDuration);
                return;
            }
            int maxPageIndex = Mathf.Max(0, Mathf.CeilToInt((float)totalCount / safeItemsPerPage) - 1);
            int nextPage = Mathf.Clamp(currentPage + 1, 0, maxPageIndex);
            int targetFirst = nextPage * safeItemsPerPage;
            JumpToIndex(targetFirst, animated, pageSnapDuration);
        }

        /// <summary>
        /// 跳到上一页
        /// </summary>
        public void PrevPage(bool animated = true)
        {
            if (!enablePaging || totalCount == 0) return;
            int safeItemsPerPage = Mathf.Max(1, itemsPerPage);
            if (enableLooping)
            {
                // 循环模式：同 NextPage，按当前可见数据位置回退一页（数据索引域）
                int currentDataIndex = ((currentFirstIndex % totalCount) + totalCount) % totalCount;
                int targetDataIndex = (currentDataIndex - safeItemsPerPage % totalCount + totalCount) % totalCount;
                JumpToIndex(targetDataIndex, animated, pageSnapDuration);
                return;
            }
            int prevPage = Mathf.Clamp(currentPage - 1, 0, Mathf.Max(0, Mathf.CeilToInt((float)totalCount / safeItemsPerPage) - 1));
            int targetFirst = prevPage * safeItemsPerPage;
            JumpToIndex(targetFirst, animated, pageSnapDuration);
        }

        /// <summary>
        /// 停止当前的滚动协程（若有）。
        /// </summary>
        void StopScrollCoroutineIfAny()
        {
            scrollCts?.Cancel();
            scrollCts?.Dispose();
            scrollCts = null;
        }

        /// <summary>
        /// 平滑滚动（UniTask 零 GC，替代协程）
        /// </summary>
        private async UniTaskVoid SmoothScrollToAsync(Vector2 targetAnchoredPos, int finalFirstIndex, float duration)
        {
            var cts = new CancellationTokenSource();
            scrollCts = cts;
            var ct = cts.Token;
            try
            {
                Vector2 start = content.anchoredPosition;
                float elapsed = 0f;

                // 停止惯性
                if (scrollRect != null)
                {
                    scrollRect.velocity = Vector2.zero;
                }

                while (elapsed < duration && !ct.IsCancellationRequested)
                {
                    // unscaledDeltaTime：暂停（timeScale=0）时翻页动画不应冻结
                    elapsed += Time.unscaledDeltaTime;
                    float t = Mathf.Clamp01(elapsed / duration);
                    float eased = Mathf.SmoothStep(0f, 1f, t);
                    content.anchoredPosition = Vector2.Lerp(start, targetAnchoredPos, eased);
                    await UniTask.Yield(PlayerLoopTiming.Update);
                }

                if (ct.IsCancellationRequested) return;

                // 最终确定位置
                // 在程序化设置位置时忽略 OnScroll 回调（try/finally 防用户 BindItem 异常卡死标志）
                isRecentering = true;
                try
                {
                    content.anchoredPosition = targetAnchoredPos;
                    currentFirstIndex = Mathf.Clamp(Mathf.FloorToInt(Offset / Step), 0,
                        Mathf.Max(0, (enableLooping ? totalCount * LOOP_CYCLES : totalCount) - 1));
                    RefreshVisible();
                    // 更新指示器位置（若存在）
                    UpdatePullIndicatorPositions();
                }
                finally
                {
                    isRecentering = false;
                }

                // 更新 page 并通知
                if (enablePaging && itemsPerPage > 0 && !enableLooping)
                {
                    int page = currentFirstIndex / Mathf.Max(1, itemsPerPage);
                    if (page != currentPage)
                    {
                        currentPage = page;
                        onPageChanged?.Invoke(currentPage);
                    }
                }
            }
            finally
            {
                // 正常完成也释放本协程创建的 CTS（此前只在取消时由 StopScrollCoroutineIfAny 释放）。
                // 若已被新的 StopScrollCoroutineIfAny/新协程接管，则不再重复 Dispose。
                if (scrollCts == cts)
                {
                    scrollCts.Dispose();
                    scrollCts = null;
                }
            }
        }

        /// <summary>
        /// 通知数据集已改变（如 totalCount 变化后调用以刷新）。
        /// </summary>
        public void NotifyDataSetChanged()
        {
            if (!TryInitializeReferences()) return;
            // 重新从 dataSource 更新 totalCount，并重建池和刷新视图
            totalCount = dataSource?.GetItemCount() ?? totalCount;
            Build();
            initialized = true;
            ForceRefresh();
        }

        public void RefreshItem(int dataIndex)
        {
            if (!initialized || dataIndex < 0 || dataIndex >= totalCount) return;
            for (int index = 0; index < pooledDataIndices.Count; index++)
            {
                if (pooledDataIndices[index] == dataIndex) pooledDataIndices[index] = -1;
            }
            RefreshVisible();
        }

        public void RefreshItems()
        {
            if (!initialized) return;
            for (int index = 0; index < pooledDataIndices.Count; index++) pooledDataIndices[index] = -1;
            RefreshVisible();
        }
    }
}
