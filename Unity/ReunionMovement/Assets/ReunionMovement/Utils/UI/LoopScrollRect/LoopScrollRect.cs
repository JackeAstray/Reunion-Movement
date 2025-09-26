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

        [Header("下拉/上拉 / 左拉/右拉 设置")]
        // 启用各方向的拉动触发
        public bool enablePullStart = true; // 对竖直为下拉刷新、横向为左拉刷新（视 content 起始端）
        public bool enablePullEnd = true;   // 对竖直为上拉加载、横向为右拉加载（视 content 末端）
        // 判定阈值（像素）
        public float pullThreshold = 50f;
        // 回调事件
        public UnityEvent onPullStart; // 下拉刷新 / 左拉刷新
        public UnityEvent onPullEnd;   // 上拉加载 / 右拉加载

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
        // 当前第一个可见项对应的数据索引
        int currentFirstIndex = -1;

        // 拖拽与拉动状态
        bool isDragging = false;
        // 正在进行的刷新/加载（防止重复触发），start/end 分开
        bool isActionInProgressStart = false;
        bool isActionInProgressEnd = false;

        // 用于平滑滚动的协程引用（跳转时可选）
        Coroutine scrollCoroutine = null;

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

            visibleCount = Mathf.CeilToInt(viewSize / (itemSize + spacing)) + extraBuffer;
            visibleCount = Mathf.Min(visibleCount, Mathf.Max(0, totalCount));

            // 设置 content 大小以允许滚动（使用 SetSizeWithCurrentAnchors 更可靠）
            if (direction == Direction.Vertical)
            {
                float contentHeight = totalCount * (itemSize + spacing) - spacing;
                content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, contentHeight);
            }
            else
            {
                float contentWidth = totalCount * (itemSize + spacing) - spacing;
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
        }
        /// <summary>
        /// 滚动时回调，计算新的 firstIndex 并刷新可见项。
        /// </summary>
        /// <param name="v2"></param>
        void OnScroll(Vector2 v2)
        {
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

            newFirst = Mathf.Clamp(newFirst, 0, Math.Max(0, totalCount - visibleCount));
            if (newFirst != currentFirstIndex)
            {
                currentFirstIndex = newFirst;
                RefreshVisible();
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
                int dataIndex = currentFirstIndex + i;
                var item = pooledItems[i];
                if (dataIndex >= 0 && dataIndex < totalCount)
                {
                    item.gameObject.SetActive(true);
                    // 绑定数据
                    dataSource?.BindItem(item, dataIndex);
                    // 定位 item
                    if (direction == Direction.Vertical)
                    {
                        float posY = -dataIndex * (itemSize + spacing);
                        item.anchoredPosition = new Vector2(item.anchoredPosition.x, posY);
                    }
                    else
                    {
                        float posX = dataIndex * (itemSize + spacing);
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

            int maxFirst = Math.Max(0, totalCount - visibleCount);
            int targetFirst = Mathf.Clamp(index, 0, maxFirst);

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
                content.anchoredPosition = targetPos;
                // 停止惯性
                if (scrollRect != null)
                {
                    scrollRect.velocity = Vector2.zero;
                }
                currentFirstIndex = targetFirst;
                RefreshVisible();
            }
            else
            {
                // 平滑滚动
                StopScrollCoroutineIfAny();
                scrollCoroutine = StartCoroutine(SmoothScrollTo(targetPos, targetFirst, duration));
            }
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
            content.anchoredPosition = targetAnchoredPos;
            currentFirstIndex = finalFirstIndex;
            RefreshVisible();
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