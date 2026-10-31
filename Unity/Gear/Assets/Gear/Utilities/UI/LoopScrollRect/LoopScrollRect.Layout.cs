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
        private float CalculateContentSize(int count)
        {
            float size = Mathf.Max(0f, count * Step - spacing);
            if (enablePaging && !enableLooping && count > 0)
            {
                int lastPage = (count - 1) / Mathf.Max(1, itemsPerPage);
                size = Mathf.Max(size, lastPage * Mathf.Max(1, itemsPerPage) * Step + ViewSize);
            }
            return size;
        }
        /// <summary>
        /// 滚动时回调，计算新的 firstIndex 并刷新可见项。
        /// </summary>
        /// <param name="v2"></param>
        void OnScroll(Vector2 v2)
        {
            // 如果正在程序化重心化，忽略本次回调，避免重入/抖动
            if (isRecentering) return;

            // 计算应该显示的 firstIndex（基于 content.anchoredPosition）
            if (!initialized || content == null || viewport == null) return;

            int newFirst = 0;
            // 运行时钳制：spacing 可能被外部赋负值使步长 <=0，FloorToInt 与循环重心化会除零/溢出
            float step = Mathf.Max(0.001f, itemSize + spacing);
            if (direction == Direction.Vertical)
            {
                // content.anchoredPosition.y 为滚动偏移（默认顶部为0向下是正）
                float y = content.anchoredPosition.y;
                newFirst = Mathf.FloorToInt(y / step);
            }
            else
            {
                float x = -content.anchoredPosition.x; // 横向滚动时 anchoredPosition.x 方向与偏移反向
                newFirst = Mathf.FloorToInt(x / step);
            }

            int effectiveTotal = enableLooping && totalCount > 0 ? totalCount * LOOP_CYCLES : totalCount;
            newFirst = Mathf.Clamp(newFirst, 0, Math.Max(0, effectiveTotal - 1));
            if (newFirst != currentFirstIndex)
            {
                currentFirstIndex = newFirst;
                RefreshVisible();

                // 如果启用循环，检测是否接近边缘（进入第一份或第三份），若是则把 content 重新重心化到中间一份。
                // 关键守卫：数据量不足以填满视口（visibleCount >= effectiveTotal）时 clamp 上限为 0，
                // newFirst 恒 0 且恒 < firstBoundary → 每次滚动都命中重心化，content 位置无限漂移；
                // 此场景下循环毫无意义（一屏内无边缘），直接禁用重心化。
                if (enableLooping && totalCount > 0 && visibleCount < effectiveTotal)
                {
                    int firstBoundary = totalCount;
                    int secondBoundary = totalCount * 2;
                    if (currentFirstIndex < firstBoundary)
                    {
                        // 向右移动一个 cycle 以回到中间
                        float shift = totalCount * step;

                        isRecentering = true;
                        try
                        {
                            StopScrollCoroutineIfAny();
                            if (scrollRect != null) scrollRect.velocity = Vector2.zero;

                            if (direction == Direction.Vertical)
                            {
                                content.anchoredPosition = new Vector2(content.anchoredPosition.x, content.anchoredPosition.y + shift);
                            }
                            else
                            {
                                content.anchoredPosition = new Vector2(content.anchoredPosition.x - shift, content.anchoredPosition.y);
                            }

                            currentFirstIndex += totalCount;
                            RefreshVisible();
                        }
                        finally
                        {
                            // 与 JumpToIndex / Build / SmoothScrollToAsync 保持一致：RefreshVisible 会调用
                            // 用户的 BindItem，若其抛异常则必须复位 isRecentering，否则 OnScroll 会永久早退、
                            // 整个列表从此无法滚动（此前这两处是裸赋值，是文件内唯一漏掉该模式的点）
                            isRecentering = false;
                        }
                    }
                    else if (currentFirstIndex >= secondBoundary)
                    {
                        // 向左移动一个 cycle
                        float shift = totalCount * step;

                        isRecentering = true;
                        try
                        {
                            StopScrollCoroutineIfAny();
                            if (scrollRect != null) scrollRect.velocity = Vector2.zero;

                            if (direction == Direction.Vertical)
                            {
                                content.anchoredPosition = new Vector2(content.anchoredPosition.x, content.anchoredPosition.y - shift);
                            }
                            else
                            {
                                content.anchoredPosition = new Vector2(content.anchoredPosition.x + shift, content.anchoredPosition.y);
                            }

                            currentFirstIndex -= totalCount;
                            RefreshVisible();
                        }
                        finally
                        {
                            isRecentering = false;
                        }
                    }
                }

                // 当不是由分页主动设置时，也可以计算当前页（用于初始化或直接滚动后的回调）。
                // 拖拽中（未松手吸附）不通知：整数除法会产生中间页号导致指示器抖动，吸附完成时统一通知
                if (enablePaging && itemsPerPage > 0 && !enableLooping && !isDragging && scrollCts == null)
                {
                    int page = currentFirstIndex / itemsPerPage;
                    if (page != currentPage)
                    {
                        currentPage = page;
                        onPageChanged?.Invoke(currentPage);
                    }
                }
            }

            // 额外：在拖拽过程中实时显示拉动指示器（生成在 viewport 下，避免被 content 内布局影响）
            // 与 TryTriggerPullOnRelease 一致：Build 不访问 viewport，故 pooledItems 非空并不能保证
            // viewport 已配置，裸用 viewport.rect 会在拖拽中抛 NRE。无 viewport 时 viewSize/maxOffset
            // 本无定义，故跳过整套实时可视化（若希望改用 content 兜底计算几何量，即为方案 B）
            if (!enableLooping && isDragging && viewport != null && content != null)
            {
                float viewSize = (direction == Direction.Vertical) ? viewport.rect.height : viewport.rect.width;
                float contentSize = (direction == Direction.Vertical) ? content.rect.height : content.rect.width;
                float maxOffset = Mathf.Max(0f, contentSize - viewSize);

                float offset = (direction == Direction.Vertical) ? content.anchoredPosition.y : -content.anchoredPosition.x;

                // 起始端可视化（下拉/左拉）
                if (offset < 0f && enablePullStart)
                {
                    ShowPullStartIndicator();
                    // 仅在未进入刷新动作时更新进度
                    if (!isActionInProgressStart && pullStartIndicatorComp != null)
                    {
                        float progress = Mathf.Clamp01(-offset / Mathf.Max(1f, pullThreshold));
                        pullStartIndicatorComp.OnPulling(progress);
                    }
                }
                else
                {
                    // 如果没有触发动作且回到范围内，则隐藏实时指示器
                    if (!isActionInProgressStart)
                        HidePullStartIndicator();
                }

                // 末端可视化（上拉/右拉）
                if (offset > maxOffset && enablePullEnd)
                {
                    ShowPullEndIndicator();
                    if (!isActionInProgressEnd && pullEndIndicatorComp != null)
                    {
                        float progress = Mathf.Clamp01((offset - maxOffset) / Mathf.Max(1f, pullThreshold));
                        pullEndIndicatorComp.OnPulling(progress);
                    }
                }
                else
                {
                    if (!isActionInProgressEnd)
                        HidePullEndIndicator();
                }
            }
        }

        /// <summary>
        /// 强制刷新（重新计算 firstIndex 并刷新可见项）。
        /// </summary>
        void ForceRefresh()
        {
            // 强制首次计算 firstIndex 并刷新
            if (!initialized) return;
            // isRecentering 期间 OnScroll 会在首行早退；若先把索引置为 -1 再调用，索引就会停留在 -1
            // （RefreshVisible 里 virtualIndex >= 0 对 i=0 不成立 → 首项被隐藏、列表错位）。
            // 当前所有调用点均为同步、不可能命中，故此为纵深防御：今日永不触发 ⇒ 行为不变
            if (isRecentering) return;
            currentFirstIndex = -1;
            OnScroll(Vector2.zero);
        }

        /// <summary>
        /// 刷新当前可见项（绑定数据并定位）。
        /// </summary>
        void RefreshVisible()
        {
            // 与 OnScroll 的 step 钳制保持一致（运行时 spacing 可能被改负）
            float step = Mathf.Max(0.001f, itemSize + spacing);

            for (int i = 0; i < pooledItems.Count; i++)
            {
                int virtualIndex = currentFirstIndex + i;
                var item = pooledItems[i];
                var loopItemComp = i < pooledItemComps.Count ? pooledItemComps[i] : null;
                if (virtualIndex >= 0 && totalCount > 0 && (enableLooping || virtualIndex < totalCount))
                {
                    int dataIndex = enableLooping ? virtualIndex % totalCount : virtualIndex;
                    item.gameObject.SetActive(true);

                    // 相同槽位仍绑定相同数据时跳过重复绑定。
                    if (i >= pooledDataIndices.Count || pooledDataIndices[i] != dataIndex)
                    {
                        dataSource?.BindItem(item, dataIndex);
                        if (i < pooledDataIndices.Count)
                        {
                            pooledDataIndices[i] = dataIndex;
                        }
                    }

                    // 绑定点击/选中回调（如果 Item 上有 LoopItemBase）
                    if (loopItemComp != null)
                    {
                        loopItemComp.onClick = OnItemClicked;
                        // 由 LoopScrollRect 直接维护数据索引，不再依赖数据源 BindItem 写入，
                        // 避免自定义数据源提前 return 时 index 残留旧值导致点击/选中错位
                        loopItemComp.index = dataIndex;
                        loopItemComp.SetSelected(dataIndex == selectedDataIndex);
                    }

                    // 定位 item 使用虚拟索引，以支持循环中间重心化
                    if (direction == Direction.Vertical)
                    {
                        float posY = -virtualIndex * step;
                        // 非主轴 x 定位为 0，保证对齐
                        item.anchoredPosition = new Vector2(0f, posY);
                    }
                    else
                    {
                        float posX = virtualIndex * step;
                        // 非主轴 y 定位为 0，保证对齐
                        item.anchoredPosition = new Vector2(posX, 0f);
                    }
                }
                else
                {
                    // 若不可见，确保移除回调并隐藏
                    if (loopItemComp != null)
                    {
                        loopItemComp.onClick = null;
                        loopItemComp.SetSelected(false);
                    }
                    if (i < pooledDataIndices.Count)
                    {
                        pooledDataIndices[i] = -1;
                    }
                    item.gameObject.SetActive(false);
                }
            }

            // 更新指示器位置（若存在）
            UpdatePullIndicatorPositions();
        }

        void OnItemClicked(int dataIndex)
        {
            if (!isActiveAndEnabled || dataIndex < 0 || dataIndex >= totalCount) return;
            bool changed = selectedDataIndex != dataIndex;
            // 选择数据索引（不自动滚动）
            selectedDataIndex = dataIndex;
            // 更新可见项的选中状态（复用已缓存的组件引用，避免 GetComponent 循环）
            for (int i = 0; i < pooledItemComps.Count; i++)
            {
                var li = pooledItemComps[i];
                if (li != null)
                {
                    li.SetSelected(li.index == selectedDataIndex);
                }
            }

            onItemSelected?.Invoke(dataIndex);
            if (changed) SelectionChanged?.Invoke(dataIndex);
            ItemClicked?.Invoke(dataIndex);
        }

        /// <summary>
        /// 外部接口：以数据索引选择一个项（可选是否使其可见）
        /// </summary>
        public void SelectDataIndex(int dataIndex, bool ensureVisible = false, bool animated = false, bool notify = true)
        {
            if (totalCount == 0) return;
            dataIndex = Mathf.Clamp(dataIndex, 0, totalCount - 1);
            bool changed = selectedDataIndex != dataIndex;
            selectedDataIndex = dataIndex;

            // 更新可见项状态（复用已缓存的组件引用，避免 GetComponent 循环）
            for (int i = 0; i < pooledItemComps.Count; i++)
            {
                var li = pooledItemComps[i];
                if (li != null)
                {
                    li.SetSelected(li.index == selectedDataIndex);
                }
            }

            if (notify)
            {
                onItemSelected?.Invoke(dataIndex);
                if (changed) SelectionChanged?.Invoke(dataIndex);
            }

            if (ensureVisible)
            {
                // 将目标索引移动为第一个可见项（JumpToIndex 使用数据索引时会放到中间 cycle）
                JumpToIndex(dataIndex, animated, pageSnapDuration);
            }
        }

        public void ClearSelection(bool notify = false)
        {
            bool changed = selectedDataIndex != -1;
            selectedDataIndex = -1;
            for (int i = 0; i < pooledItemComps.Count; i++)
            {
                var li = pooledItemComps[i];
                if (li != null) li.SetSelected(false);
            }
            if (notify && changed) SelectionChanged?.Invoke(-1);
        }

        /// <summary>取当前选中的数据索引；无选中时返回 -1（ClearSelection/未选状态下即为此值）</summary>
        public int GetSelectedIndex() => selectedDataIndex;

        // IBeginDragHandler / IEndDragHandler 用于检测用户拖拽释放以判断是否触发拉动动作
        public void OnBeginDrag(PointerEventData eventData)
        {
            if (!initialized || !isActiveAndEnabled || eventData.button != PointerEventData.InputButton.Left) return;
            isDragging = true;
            // 用户开始拖拽：中断进行中的平滑滚动 tween，避免两者抢写 content.anchoredPosition
            StopScrollCoroutineIfAny();
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!initialized || !isActiveAndEnabled || eventData.button != PointerEventData.InputButton.Left) return;
            isDragging = false;
            TryTriggerPullOnRelease();

            // 如果松手时并未触发刷新/加载动作，则需要隐藏实时指示器
            if (!isActionInProgressStart)
            {
                HidePullStartIndicator();
            }
            if (!isActionInProgressEnd)
            {
                HidePullEndIndicator();
            }

            // Page snapping：在释放时按页吸附（如果启用）
            if (enablePaging && snapToPageOnRelease && totalCount > 0 && pooledItems.Count > 0 && !enableLooping)
            {
                float viewSize = (direction == Direction.Vertical) ? viewport.rect.height : viewport.rect.width;
                float contentSize = (direction == Direction.Vertical) ? content.rect.height : content.rect.width;
                float maxOffset = Mathf.Max(0f, contentSize - viewSize);

                float offset = (direction == Direction.Vertical) ? content.anchoredPosition.y : -content.anchoredPosition.x;

                // 如果用户触发了拉动刷新/加载（越界超过阈值），不进行分页吸附（让外部处理）
                float threshold = Mathf.Max(1f, pullThreshold);
                if (offset < -threshold || offset > maxOffset + threshold)
                {
                    return;
                }

                // 以 itemsPerPage 和 itemSize 计算页大小（像素）
                int safeItemsPerPage = Mathf.Max(1, itemsPerPage);
                float pageSizePixels = Mathf.Max(0.001f, safeItemsPerPage * (itemSize + spacing));

                // 计算最接近的页
                int pageIndex = Mathf.RoundToInt(offset / pageSizePixels);
                int maxPageIndex = Mathf.Max(0, Mathf.CeilToInt((float)totalCount / safeItemsPerPage) - 1);
                pageIndex = Mathf.Clamp(pageIndex, 0, maxPageIndex);

                int targetFirst = pageIndex * safeItemsPerPage;
                JumpToIndex(targetFirst, true, pageSnapDuration);
            }
        }

        /// <summary>
        /// 计算当前 content 偏移并基于阈值判定是否触发拉动回调（在释放时调用）
        /// 使用统一的 offset 计算： offset = content 轴向偏移（起点为 0），其中竖直使用 content.anchoredPosition.y，横向使用 -content.anchoredPosition.x（与 Refresh 逻辑一致）
        /// 当 offset < -threshold 表示在起始端超出阈值（下拉或左拉）
        /// 当 offset > maxOffset + threshold 表示在末端超出阈值（上拉或右拉）
        /// </summary>
        void TryTriggerPullOnRelease()
        {
            // 注意：不再因 totalCount==0 提前返回——空列表下拉加载首屏数据是核心使用场景
            // （content 小于 viewport 时 Elastic 模式仍可拉出负偏移）

            // 在循环模式下禁用拉动刷新/加载以避免与循环重心化逻辑冲突
            if (enableLooping) return;

            // 与 ReboundToEdge 保持一致：未配置 viewport/content 时直接返回。
            // 本方法由 OnEndDrag 无条件调用（不经过 pooledItems.Count > 0 那道守卫），
            // 缺此判断时下面的 viewport.rect 会抛 NullReferenceException
            if (viewport == null || content == null) return;

            float viewSize = (direction == Direction.Vertical) ? viewport.rect.height : viewport.rect.width;
            float contentSize = (direction == Direction.Vertical) ? content.rect.height : content.rect.width;
            float maxOffset = Mathf.Max(0f, contentSize - viewSize);

            float offset = (direction == Direction.Vertical) ? content.anchoredPosition.y : -content.anchoredPosition.x;
            float threshold = Mathf.Max(1f, pullThreshold);

            // 起始端（顶部/左侧）超出
            if (offset < -threshold && enablePullStart && !isActionInProgressStart)
            {
                isActionInProgressStart = true;
                // 显示起始端指示器（如果设置了预制体）
                ShowPullStartIndicator();
                // 切换指示器到刷新状态
                pullStartIndicatorComp?.OnRefreshing();
                onPullStart?.Invoke();
            }
            // 末端（底部/右侧）超出
            else if (offset > maxOffset + threshold && enablePullEnd && !isActionInProgressEnd)
            {
                isActionInProgressEnd = true;
                // 显示末端指示器（如果设置了预制体）
                ShowPullEndIndicator();
                pullEndIndicatorComp?.OnRefreshing();
                onPullEnd?.Invoke();
            }
        }
    }
}
