using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.EventSystems;

namespace ReunionMovement.Common.Util
{
    /// <summary>
    /// 简单的 Loop ScrollRect（固定项大小）。
    /// 要求：
    /// - itemPrefab 为 RectTransform（最好包含 LayoutElement 或固定大小）。
    /// - content 的 pivot/anchor 推荐：竖直 -> 上左 (pivot.y = 1), 水平 -> 上左 (pivot.x = 0)。
    /// 使用方式：
    /// - 在场景中把此组件挂到包含 ScrollRect 的对象（或任意对象），
    ///   指定 ScrollRect、itemPrefab，调用 Initialize(dataSource) 或在 Inspector 设置 totalCount 并在 Start 前调用 Initialize.
    /// 
    /// 新增：
    /// - 支持上拉加载 / 下拉刷新（竖直）以及 左拉刷新 / 右拉加载（横向）
    /// - 通过 UnityEvent 回调触发，暴露阈值与启用开关，并提供完成通知接口以重置状态
    /// - 新增 PageView 支持：按页对齐（可配置项数/每页），释放时自动吸附并触发页变更事件
    /// - 新增 循环模式（enableLooping），启用后会在内容中构建三倍循环并在必要时重心化以实现视觉上的无限循环
    /// </summary>
    public class LoopScrollRect : MonoBehaviour, IBeginDragHandler, IEndDragHandler
    {
        public enum Direction { Vertical, Horizontal }

        [Header("References")]
        public ScrollRect scrollRect;
        public RectTransform itemPrefab;

        [Header("设置")]
        public Direction direction = Direction.Vertical;
        // 数据总条数
        public int totalCount = 0;
        // 项间距
        public float spacing = 0f;
        // 是否自动计算 extraBuffer（开启时会覆盖 Inspector 中的值）
        public bool autoCalculateExtraBuffer = true;
        // 多创建几个作缓冲（当 autoCalculateExtraBuffer 为 true 时会被覆盖）
        public int extraBuffer = 2;

        [Header("循环设置")]
        // 开启无限循环（视觉上）
        public bool enableLooping = false;

        [Header("下拉/上拉 / 左拉/右拉 设置")]
        // 启用各方向的拉动触发
        public bool enablePullStart = true; // 对竖直为下拉刷新、横向为左拉刷新（视 content 起始端）
        public bool enablePullEnd = true;   // 对竖直为上拉加载、横向为右拉加载（视 content 末端）
        // 判定阈值（像素）
        public float pullThreshold = 50f;
        // 回调事件
        public UnityEvent onPullStart; // 下拉刷新 / 左拉刷新
        public UnityEvent onPullEnd;   // 上拉加载 / 右拉加载

        [Header("PageView 设置")]
        // 启用按页对齐（PageView）
        public bool enablePaging = false;
        // 每页显示多少个 item（>=1）
        public int itemsPerPage = 1;
        // 释放时是否吸附到页（true）或仅通过 JumpToIndex 控制翻页（false）
        public bool snapToPageOnRelease = true;
        // 吸附平滑时长
        public float pageSnapDuration = 0.25f;
        // 页变化事件（参数：当前页索引，从0开始）
        public UnityEvent<int> onPageChanged;

        // DataSource 用于外部绑定数据和数量
        public interface IDataSource
        {
            int GetItemCount();
            void BindItem(RectTransform item, int dataIndex);
        }

        IDataSource dataSource;

        RectTransform content;
        RectTransform viewport;
        // 单项尺寸（高度或宽度）
        float itemSize;
        int visibleCount;
        List<RectTransform> pooledItems = new List<RectTransform>();
        // 当前第一个可见项对应的虚拟索引（可跨 cycle）
        int currentFirstIndex = -1;

        // Paging 状态
        int currentPage = -1;

        // 拖拽与拉动状态
        bool isDragging = false;
        // 正在进行的刷新/加载（防止重复触发），start/end 分开
        bool isActionInProgressStart = false;
        bool isActionInProgressEnd = false;

        // 用于平滑滚动的协程引用（跳转时可选）
        Coroutine scrollCoroutine = null;

        // 当启用循环时使用的 cycle 倍数（固定为 3, 中间为初始显示区）
        const int LOOP_CYCLES = 3;

        // 标记在程序化调整 content 时忽略 OnScroll 回调，防止重入/抖动
        bool isRecentering = false;

        void Awake()
        {
            if (scrollRect == null)
            {
                scrollRect = GetComponentInChildren<ScrollRect>();
            }
            if (scrollRect == null)
            {
                throw new Exception("LoopScrollRect: 需要ScrollRect引用。");
            }
            content = scrollRect.content;
            viewport = scrollRect.viewport != null ? scrollRect.viewport : scrollRect.GetComponent<RectTransform>();

            if (itemPrefab == null)
            {
                throw new Exception("LoopScrollRect: 需要Item预制体。");
            }
            itemPrefab.gameObject.SetActive(false);
        }

        void OnEnable()
        {
            scrollRect.onValueChanged.AddListener(OnScroll);
        }

        void OnDisable()
        {
            scrollRect.onValueChanged.RemoveListener(OnScroll);
        }

        /// <summary>
        /// 初始化并开始（会读取 dataSource 的数量并构建池）。
        /// 可在运行时多次调用以刷新数据源。
        /// </summary>
        public void Initialize(IDataSource source)
        {
            dataSource = source;
            totalCount = dataSource?.GetItemCount() ?? totalCount;
            Build();
            ForceRefresh();
        }

        /// <summary>
        /// 构建池和 content（会清理已有池）。
        /// </summary>
        void Build()
        {
            // 清理已有池
            foreach (var it in pooledItems)
            {
                if (it != null)
                {
                    Destroy(it.gameObject);
                }
            }
            pooledItems.Clear();
            currentFirstIndex = -1;
            currentPage = -1;

            // 计算单项大小（使用 prefab 的 rect）
            if (direction == Direction.Vertical)
            {
                itemSize = itemPrefab.rect.height;
            }
            else
            {
                itemSize = itemPrefab.rect.width;
            }

            // 视口可见数（向上取整） + buffer
            if (itemSize <= 0) itemSize = 1;
            float viewSize = (direction == Direction.Vertical) ? viewport.rect.height : viewport.rect.width;

            // 自动计算 extraBuffer（基于可见项数的比例），可避免在快速滚动时频繁重用导致闪烁
            if (autoCalculateExtraBuffer)
            {
                int itemsInView = Mathf.CeilToInt(viewSize / (itemSize + spacing));
                // 取可见项数的一半作为缓冲（经验值），保证至少 1，限制一个合理上限以避免创建过多对象
                extraBuffer = Mathf.Clamp(Mathf.CeilToInt(itemsInView * 0.5f), 1, 10);
            }

            // 当启用循环时，我们在 content 中创建多份循环（LOOP_CYCLES），并从中间一份开始显示
            int effectiveTotal = enableLooping && totalCount > 0 ? totalCount * LOOP_CYCLES : totalCount;

            visibleCount = Mathf.CeilToInt(viewSize / (itemSize + spacing)) + extraBuffer;
            visibleCount = Mathf.Min(visibleCount, Mathf.Max(0, effectiveTotal));

            // 设置 content 大小以允许滚动（使用 SetSizeWithCurrentAnchors 更可靠）
            if (direction == Direction.Vertical)
            {
                float contentHeight = effectiveTotal * (itemSize + spacing) - spacing;
                content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, contentHeight);
            }
            else
            {
                float contentWidth = effectiveTotal * (itemSize + spacing) - spacing;
                content.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, contentWidth);
            }

            // 生成池内初始可见项（reuse prefab）
            for (int i = 0; i < visibleCount; i++)
            {
                var go = Instantiate(itemPrefab.gameObject, content);
                go.SetActive(true);
                var rt = go.GetComponent<RectTransform>();
                rt.localScale = Vector3.one;

                // 保证项使用左上对齐（与注释中推荐的 pivot/anchor 一致），避免被父布局拉伸。
                // 如果你的 prefab 已经正确设置，这两行可以去掉；但在遇到“宽度比预期大”问题时建议保持明确设置。
                rt.pivot = new Vector2(0, 1);
                rt.anchorMin = rt.anchorMax = new Vector2(0, 1);

                // 显式设置位置，防止初始位置为 (0,0) 导致错位
                if (direction == Direction.Vertical)
                {
                    rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, -i * (itemSize + spacing));
                }
                else
                {
                    rt.anchoredPosition = new Vector2(i * (itemSize + spacing), rt.anchoredPosition.y);
                }

                // 在生成池内初始可见项的循环中，根据 direction 决定主轴与非主轴尺寸：
                // - 主轴尺寸使用计算得到的 itemSize（保持固定项高/宽）
                // - 非主轴尺寸使用 content 的当前宽/高以填满 content（若 content 为 0 则回退到 prefab 原始尺寸）
                if (direction == Direction.Vertical)
                {
                    // 主轴：高度已设置为 itemSize；非主轴：使用 content 宽度（fallback 到 prefab 宽度）
                    rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, itemSize);
                    float width = content.rect.width;
                    if (width <= 0) width = itemPrefab.rect.width;
                    rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
                }
                else
                {
                    // 主轴：宽度已设置为 itemSize；非主轴：使用 content 高度（fallback 到 prefab 高度）
                    rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, itemSize);
                    float height = content.rect.height;
                    if (height <= 0) height = itemPrefab.rect.height;
                    rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
                }

                pooledItems.Add(rt);
            }

            // 如果启用了循环并且有数据，初始定位到中间 cycle 的起点，避免立刻到达边缘
            if (enableLooping && totalCount > 0)
            {
                int startFirst = totalCount; // middle cycle
                currentFirstIndex = startFirst;
                Vector2 pos = content.anchoredPosition;

                // 在程序化设置位置时忽略 OnScroll 回调以避免重入
                isRecentering = true;
                if (direction == Direction.Vertical)
                {
                    pos.y = startFirst * (itemSize + spacing);
                }
                else
                {
                    pos.x = -startFirst * (itemSize + spacing);
                }
                content.anchoredPosition = pos;
                if (scrollRect != null) scrollRect.velocity = Vector2.zero;
                RefreshVisible();
                isRecentering = false;
            }
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
            if (totalCount == 0 || pooledItems.Count == 0)
            {
                return;
            }

            int newFirst = 0;
            if (direction == Direction.Vertical)
            {
                // content.anchoredPosition.y 为滚动偏移（默认顶部为0向下是正）
                float y = content.anchoredPosition.y;
                newFirst = Mathf.FloorToInt(y / (itemSize + spacing));
            }
            else
            {
                float x = -content.anchoredPosition.x; // 横向滚动时 anchoredPosition.x 方向与偏移反向
                newFirst = Mathf.FloorToInt(x / (itemSize + spacing));
            }

            int effectiveTotal = enableLooping && totalCount > 0 ? totalCount * LOOP_CYCLES : totalCount;
            newFirst = Mathf.Clamp(newFirst, 0, Math.Max(0, effectiveTotal - visibleCount));
            if (newFirst != currentFirstIndex)
            {
                currentFirstIndex = newFirst;
                RefreshVisible();

                // 如果启用循环，检测是否接近边缘（进入第一份或第三份），若是则把 content 重新重心化到中间一份
                if (enableLooping && totalCount > 0)
                {
                    int firstBoundary = totalCount;
                    int secondBoundary = totalCount * 2;
                    if (currentFirstIndex < firstBoundary)
                    {
                        // 向右移动一个 cycle 以回到中间
                        float shift = totalCount * (itemSize + spacing);

                        isRecentering = true;
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

                        isRecentering = false;
                    }
                    else if (currentFirstIndex >= secondBoundary)
                    {
                        // 向左移动一个 cycle
                        float shift = totalCount * (itemSize + spacing);

                        isRecentering = true;
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

                        isRecentering = false;
                    }
                }

                // 当不是由分页主动设置时，也可以计算当前页（用于初始化或直接滚动后的回调）
                if (enablePaging && itemsPerPage > 0 && !enableLooping)
                {
                    int page = currentFirstIndex / itemsPerPage;
                    if (page != currentPage)
                    {
                        currentPage = page;
                        onPageChanged?.Invoke(currentPage);
                    }
                }
            }
        }

        /// <summary>
        /// 强制刷新（重新计算 firstIndex 并刷新可见项）。
        /// </summary>
        void ForceRefresh()
        {
            // 强制首次计算 firstIndex 并刷新
            if (totalCount == 0 || pooledItems.Count == 0)
            {
                return;
            }
            currentFirstIndex = -1;
            OnScroll(Vector2.zero);
        }

        /// <summary>
        /// 刷新当前可见项（绑定数据并定位）。
        /// </summary>
        void RefreshVisible()
        {
            for (int i = 0; i < pooledItems.Count; i++)
            {
                int virtualIndex = currentFirstIndex + i;
                var item = pooledItems[i];
                if (virtualIndex >= 0 && totalCount > 0)
                {
                    int dataIndex = ((virtualIndex % totalCount) + totalCount) % totalCount; // modulo
                    item.gameObject.SetActive(true);
                    // 绑定数据
                    dataSource?.BindItem(item, dataIndex);
                    // 定位 item 使用虚拟索引，以支持循环中间重心化
                    if (direction == Direction.Vertical)
                    {
                        float posY = -virtualIndex * (itemSize + spacing);
                        item.anchoredPosition = new Vector2(item.anchoredPosition.x, posY);
                    }
                    else
                    {
                        float posX = virtualIndex * (itemSize + spacing);
                        item.anchoredPosition = new Vector2(posX, item.anchoredPosition.y);
                    }
                }
                else
                {
                    item.gameObject.SetActive(false);
                }
            }
        }

        // IBeginDragHandler / IEndDragHandler 用于检测用户拖拽释放以判断是否触发拉动动作
        public void OnBeginDrag(PointerEventData eventData)
        {
            isDragging = true;
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            isDragging = false;
            TryTriggerPullOnRelease();

            // Page snapping：在释放时按页吸附（如果启用）
            if (enablePaging && snapToPageOnRelease && totalCount > 0 && pooledItems.Count > 0 && !enableLooping)
            {
                float viewSize = (direction == Direction.Vertical) ? viewport.rect.height : viewport.rect.width;
                float contentSize = (direction == Direction.Vertical) ? content.rect.height : content.rect.width;
                float maxOffset = Mathf.Max(0f, contentSize - viewSize);

                float offset = (direction == Direction.Vertical) ? content.anchoredPosition.y : -content.anchoredPosition.x;

                // 如果用户触发了拉动刷新/加载（越界超过阈值），不进行分页吸附（让外部处理）
                if (offset < -pullThreshold || offset > maxOffset + pullThreshold)
                {
                    return;
                }

                // 以 itemsPerPage 和 itemSize 计算页大小（像素）
                int safeItemsPerPage = Mathf.Max(1, itemsPerPage);
                float pageSizePixels = safeItemsPerPage * (itemSize + spacing);

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
            if (totalCount == 0) return;

            // 在循环模式下禁用拉动刷新/加载以避免与循环重心化逻辑冲突
            if (enableLooping) return;

            float viewSize = (direction == Direction.Vertical) ? viewport.rect.height : viewport.rect.width;
            float contentSize = (direction == Direction.Vertical) ? content.rect.height : content.rect.width;
            float maxOffset = Mathf.Max(0f, contentSize - viewSize);

            float offset = (direction == Direction.Vertical) ? content.anchoredPosition.y : -content.anchoredPosition.x;

            // 起始端（顶部/左侧）超出
            if (offset < -pullThreshold && enablePullStart && !isActionInProgressStart)
            {
                isActionInProgressStart = true;
                onPullStart?.Invoke();
            }
            // 末端（底部/右侧）超出
            else if (offset > maxOffset + pullThreshold && enablePullEnd && !isActionInProgressEnd)
            {
                isActionInProgressEnd = true;
                onPullEnd?.Invoke();
            }
        }

        /// <summary>
        /// 外部在刷新/加载完成后调用以允许下一次触发。
        /// - CompletePullStart(): 完成起始端（下拉/左拉）动作
        /// - CompletePullEnd(): 完成末端（上拉/右拉）动作
        /// 调用后可以选择刷新数据源（如 totalCount 改变后调用 Initialize/ForceRefresh）
        /// </summary>
        public void CompletePullStart()
        {
            isActionInProgressStart = false;
        }

        public void CompletePullEnd()
        {
            isActionInProgressEnd = false;
        }

        /// <summary>
        /// 根据 data index 跳转到对应位置。
        /// index: 目标数据索引（以数据项为基准的第一个可见项索引）
        /// animated: 是否使用平滑滚动（协程），false 则立即跳转并刷新可见项。
        /// duration: 平滑滚动时的时长（秒）。
        /// 说明：跳转会将第一个可见项设置为 index（若 index 太大，会被限制为可用的最大 firstIndex）。
        /// </summary>
        public void JumpToIndex(int index, bool animated = false, float duration = 0.25f)
        {
            if (totalCount == 0 || pooledItems.Count == 0) return;

            int effectiveTotal = enableLooping && totalCount > 0 ? totalCount * LOOP_CYCLES : totalCount;

            // 当启用分页时，最大 firstIndex 应基于每页显示的项数（itemsPerPage），
            // 而不是 pooledItems/visibleCount。否则当 pooledItems > itemsPerPage 时，
            // 最后一页可能被错误地 clamp 掉，导致无法到达最后一页。
            int maxFirst;
            if (enablePaging && itemsPerPage > 0 && !enableLooping)
            {
                int safeItemsPerPage = Mathf.Max(1, itemsPerPage);
                maxFirst = Math.Max(0, totalCount - safeItemsPerPage);
            }
            else
            {
                maxFirst = Math.Max(0, effectiveTotal - visibleCount);
            }

            int targetFirst = Mathf.Clamp(index, 0, maxFirst);

            // 如果启用了循环并且传入的是数据索引（0..totalCount-1），我们应该把目标放到中间 cycle
            if (enableLooping)
            {
                // 把目标 index 放到中间 cycle
                int middleBase = totalCount;
                targetFirst = Mathf.Clamp(middleBase + index, 0, maxFirst);
            }

            Vector2 targetPos = content.anchoredPosition;
            if (direction == Direction.Vertical)
            {
                targetPos.y = targetFirst * (itemSize + spacing);
            }
            else
            {
                targetPos.x = -targetFirst * (itemSize + spacing);
            }

            // 立即生效
            if (!animated || duration <= 0f)
            {
                StopScrollCoroutineIfAny();
                // 在程序化设置位置时忽略 OnScroll 回调
                isRecentering = true;
                content.anchoredPosition = targetPos;
                // 停止惯性
                if (scrollRect != null)
                {
                    scrollRect.velocity = Vector2.zero;
                }
                currentFirstIndex = targetFirst;
                RefreshVisible();
                isRecentering = false;

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
                scrollCoroutine = StartCoroutine(SmoothScrollTo(targetPos, targetFirst, duration));
            }
        }

        /// <summary>
        /// 跳到下一页
        /// </summary>
        public void NextPage(bool animated = true)
        {
            if (!enablePaging || totalCount == 0) return;
            int safeItemsPerPage = Mathf.Max(1, itemsPerPage);
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
            int prevPage = Mathf.Clamp(currentPage - 1, 0, Mathf.Max(0, Mathf.CeilToInt((float)totalCount / safeItemsPerPage) - 1));
            int targetFirst = prevPage * safeItemsPerPage;
            JumpToIndex(targetFirst, animated, pageSnapDuration);
        }

        /// <summary>
        /// 停止当前的滚动协程（若有）。
        /// </summary>
        void StopScrollCoroutineIfAny()
        {
            if (scrollCoroutine != null)
            {
                try
                {
                    StopCoroutine(scrollCoroutine);
                }
                catch { }
                scrollCoroutine = null;
            }
        }

        /// <summary>
        /// 平滑滚动协程。
        /// </summary>
        /// <param name="targetAnchoredPos"></param>
        /// <param name="finalFirstIndex"></param>
        /// <param name="duration"></param>
        /// <returns></returns>
        IEnumerator SmoothScrollTo(Vector2 targetAnchoredPos, int finalFirstIndex, float duration)
        {
            Vector2 start = content.anchoredPosition;
            float elapsed = 0f;

            // 停止惯性
            if (scrollRect != null)
            {
                scrollRect.velocity = Vector2.zero;
            }

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                // 平滑插值（可替换为其他 easing）
                float eased = Mathf.SmoothStep(0f, 1f, t);
                content.anchoredPosition = Vector2.Lerp(start, targetAnchoredPos, eased);
                yield return null;
            }

            // 最终确定位置
            // 在程序化设置位置时忽略 OnScroll 回调
            isRecentering = true;
            content.anchoredPosition = targetAnchoredPos;
            currentFirstIndex = finalFirstIndex;
            RefreshVisible();
            isRecentering = false;

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

            scrollCoroutine = null;
        }

        /// <summary>
        /// 通知数据集已改变（如 totalCount 变化后调用以刷新）。
        /// </summary>
        public void NotifyDataSetChanged()
        {
            // 重新从 dataSource 更新 totalCount，并重建池和刷新视图
            totalCount = dataSource?.GetItemCount() ?? totalCount;
            Build();
            ForceRefresh();
        }
    }
}