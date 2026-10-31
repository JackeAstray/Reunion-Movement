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
    public partial class LoopScrollRect : MonoBehaviour, IBeginDragHandler, IEndDragHandler
    {
        public enum Direction { Vertical, Horizontal }

        [Header("References")]
        public ScrollRect scrollRect;
        public RectTransform itemPrefab;

        [Header("设置")]
        public Direction direction = Direction.Vertical;
        // 数据总条数
        public int totalCount = 0;
        // 项间距（不得使 itemSize + spacing <= 0，否则滚动计算除零）
        [Range(0f, 500f)]
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
        // 判定阈值（像素，须 > 0，否则进度计算除零）
        [Range(1f, 1000f)]
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

        // 可选：通过预制体显示的拉动指示器（在 content 下生成，拉动开始时显示，完成后删除）
        public RectTransform pullStartIndicatorPrefab;
        public RectTransform pullEndIndicatorPrefab;

        // 运行时实例
        RectTransform pullStartIndicatorInstance = null;
        RectTransform pullEndIndicatorInstance = null;
        // 指示器脚本引用
        PullIndicatorBase pullStartIndicatorComp = null;
        PullIndicatorBase pullEndIndicatorComp = null;

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
        // 与 pooledItems 平行的缓存（避免滚动热路径 GetComponent 与重复 BindItem）
        List<LoopItemBase> pooledItemComps = new List<LoopItemBase>();
        List<int> pooledDataIndices = new List<int>();
        // 当前第一个可见项对应的虚拟索引（可跨 cycle）
        int currentFirstIndex = -1;

        // Paging 状态
        int currentPage = -1;

        // 选中项（以数据索引为准，-1 表示无选中）
        int selectedDataIndex = -1;
        // 选中回调（参数：数据索引）
        public UnityEvent<int> onItemSelected;

        // 拖拽与拉动状态
        bool isDragging = false;
        // 正在进行的刷新/加载（防止重复触发），start/end 分开
        bool isActionInProgressStart = false;
        bool isActionInProgressEnd = false;

        // 用于平滑滚动的取消令牌（跳转时可选，UniTask 零 GC）
        CancellationTokenSource scrollCts = null;

        // 当启用循环时使用的 cycle 倍数（固定为 3, 中间为初始显示区）
        const int LOOP_CYCLES = 3;

        // 标记在程序化调整 content 时忽略 OnScroll 回调，防止重入/抖动
        bool isRecentering = false;
        private bool initialized;
        private Vector2 lastViewportSize;
        private EventTrigger dragTrigger;
        private EventTrigger.Entry beginDragEntry;
        private EventTrigger.Entry endDragEntry;
        public event Action<int> ItemClicked;
        public event Action<int> SelectionChanged;
        private float Step => Mathf.Max(0.001f, itemSize + spacing);
        private float ViewSize => direction == Direction.Vertical ? viewport.rect.height : viewport.rect.width;
        private float Offset => direction == Direction.Vertical ? content.anchoredPosition.y : -content.anchoredPosition.x;
        private float MaxOffset => Mathf.Max(0f,
            (direction == Direction.Vertical ? content.rect.height : content.rect.width) - ViewSize);

        private void SetOffset(float offset)
        {
            var position = content.anchoredPosition;
            if (direction == Direction.Vertical) position.y = offset;
            else position.x = -offset;
            content.anchoredPosition = position;
        }

        private bool TryInitializeReferences()
        {
            if (scrollRect == null) scrollRect = GetComponentInChildren<ScrollRect>();
            if (scrollRect == null || scrollRect.content == null || itemPrefab == null)
            {
                Debug.LogError("LoopScrollRect requires ScrollRect, Content and Item Prefab.", this);
                return false;
            }
            content = scrollRect.content;
            viewport = scrollRect.viewport != null ? scrollRect.viewport : scrollRect.GetComponent<RectTransform>();
            return viewport != null;
        }

        void Awake()
        {
            if (scrollRect == null)
            {
                scrollRect = GetComponentInChildren<ScrollRect>();
            }
            if (scrollRect == null)
            {
                Debug.LogError("LoopScrollRect: 需要 ScrollRect 引用，组件已禁用。", this);
                enabled = false;
                return;
            }
            content = scrollRect.content;
            viewport = scrollRect.viewport != null ? scrollRect.viewport : scrollRect.GetComponent<RectTransform>();

            if (itemPrefab == null)
            {
                Debug.LogError("LoopScrollRect: 需要 Item 预制体，组件已禁用。", this);
                enabled = false;
                return;
            }
            // 注意：不直接对 itemPrefab 资产 SetActive(false)（会修改内存中的预制体状态，
            // 编辑器下可能标脏资产并影响其他实例化源）；实例化后由 GetItem 显式控制激活。
        }

        void OnEnable()
        {
            // Awake 校验失败时会禁用本组件并 return，此时 scrollRect 为 null；
            // 若之后被重新启用，直接解引用会抛 NRE。与 OnDestroy 的判空保持一致
            if (scrollRect == null) return;
            scrollRect.onValueChanged.RemoveListener(OnScroll);
            scrollRect.onValueChanged.AddListener(OnScroll);
            RegisterDragEvents();
            if (initialized) ForceRefresh();
        }

        private void RegisterDragEvents()
        {
            if (scrollRect == null || scrollRect.gameObject == gameObject || beginDragEntry != null) return;
            dragTrigger = scrollRect.GetComponent<EventTrigger>();
            if (dragTrigger == null) dragTrigger = scrollRect.gameObject.AddComponent<EventTrigger>();
            beginDragEntry = new EventTrigger.Entry { eventID = EventTriggerType.BeginDrag };
            beginDragEntry.callback.AddListener(data => OnBeginDrag((PointerEventData)data));
            endDragEntry = new EventTrigger.Entry { eventID = EventTriggerType.EndDrag };
            endDragEntry.callback.AddListener(data => OnEndDrag((PointerEventData)data));
            dragTrigger.triggers.Add(beginDragEntry);
            dragTrigger.triggers.Add(endDragEntry);
        }

        private void LateUpdate()
        {
            if (!initialized || viewport == null || content == null) return;
            if ((viewport.rect.size - lastViewportSize).sqrMagnitude > 0.01f)
            {
                Build();
                ForceRefresh();
            }
        }

        void OnDisable()
        {
            StopScrollCoroutineIfAny();
            if (scrollRect != null) scrollRect.StopMovement();
            if (dragTrigger != null)
            {
                dragTrigger.triggers.Remove(beginDragEntry);
                dragTrigger.triggers.Remove(endDragEntry);
            }
            beginDragEntry = null;
            endDragEntry = null;
            // 用 if 而非提前 return：下面的拖拽标记复位与指示器隐藏仍须执行。
            // 判空与 OnDestroy 的写法保持一致（Awake 校验失败时 scrollRect 可能为 null）
            if (scrollRect != null) scrollRect.onValueChanged.RemoveListener(OnScroll);
            // 组件在拖拽中途被禁用（窗口关闭等）时 OnEndDrag 不会到达：
            // 复位拖拽标记，避免重新启用后 OnScroll 每帧误跑指示器逻辑
            isDragging = false;

            // 隐藏已显示的 pull 指示器实例（拖拽中禁用会残留 active 指示器，直到下次滚动才被隐藏）
            if (pullStartIndicatorInstance != null)
            {
                pullStartIndicatorInstance.gameObject.SetActive(false);
            }
            if (pullEndIndicatorInstance != null)
            {
                pullEndIndicatorInstance.gameObject.SetActive(false);
            }
        }

        void OnDestroy()
        {
            // 销毁时停止平滑滚动协程：否则 UniTaskVoid 协程会继续每帧访问
            // 已销毁的 content.anchoredPosition，导致 MissingReferenceException 持续刷屏。
            StopScrollCoroutineIfAny();
            foreach (var item in pooledItems)
            {
                if (item == null) continue;
                var component = item.GetComponent<LoopItemBase>();
                if (component != null) component.onClick = null;
                item.gameObject.SetActive(false);
                Destroy(item.gameObject);
            }
            pooledItems.Clear();
            pooledItemComps.Clear();
            pooledDataIndices.Clear();
            if (scrollRect != null)
            {
                scrollRect.onValueChanged.RemoveListener(OnScroll);
            }
            // 销毁复用的指示器实例（挂在 viewport 下，不随本组件销毁）
            if (pullStartIndicatorInstance != null)
            {
                Destroy(pullStartIndicatorInstance.gameObject);
                pullStartIndicatorInstance = null;
                pullStartIndicatorComp = null;
            }
            if (pullEndIndicatorInstance != null)
            {
                Destroy(pullEndIndicatorInstance.gameObject);
                pullEndIndicatorInstance = null;
                pullEndIndicatorComp = null;
            }
        }

        /// <summary>
        /// 初始化并开始（会读取 dataSource 的数量并构建池）。
        /// 可在运行时多次调用以刷新数据源。
        /// </summary>
        public void Initialize(IDataSource source)
        {
            if (!TryInitializeReferences()) return;
            if (isActiveAndEnabled)
            {
                scrollRect.onValueChanged.RemoveListener(OnScroll);
                scrollRect.onValueChanged.AddListener(OnScroll);
                RegisterDragEvents();
            }
            dataSource = source;
            totalCount = dataSource?.GetItemCount() ?? totalCount;
            selectedDataIndex = -1;
            isActionInProgressStart = false;
            isActionInProgressEnd = false;
            HidePullStartIndicator();
            HidePullEndIndicator();
            Build();
            initialized = true;
            ForceRefresh();
        }

        /// <summary>
        /// 更新内容尺寸并调整池容量，复用已有条目。
        /// </summary>
        void Build()
        {
            // 数据重建期间取消在途平滑滚动：否则其每帧写 content.anchoredPosition，
            // 结束后用旧 finalFirstIndex 对新池执行 RefreshVisible，造成跳动/错位
            StopScrollCoroutineIfAny();

            float previousOffset = content != null ? Offset : 0f;
            currentFirstIndex = -1;
            currentPage = -1;
            totalCount = Mathf.Max(0, totalCount);
            if (selectedDataIndex >= totalCount) selectedDataIndex = -1;
            itemsPerPage = Mathf.Max(1, itemsPerPage);
            extraBuffer = Mathf.Max(1, extraBuffer);
            lastViewportSize = viewport.rect.size;

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
            // 运行时钳制：spacing 可能被外部赋负值，使 itemSize+spacing<=0 导致除零/错位
            spacing = Mathf.Max(0f, spacing);
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
            for (int index = pooledItems.Count - 1; index >= visibleCount; index--)
            {
                if (pooledItemComps[index] != null) pooledItemComps[index].onClick = null;
                if (pooledItems[index] != null)
                {
                    pooledItems[index].gameObject.SetActive(false);
                    Destroy(pooledItems[index].gameObject);
                }
                pooledItems.RemoveAt(index);
                pooledItemComps.RemoveAt(index);
                pooledDataIndices.RemoveAt(index);
            }

            // 设置 content 大小以允许滚动（使用 SetSizeWithCurrentAnchors 更可靠）
            if (direction == Direction.Vertical)
            {
                float contentHeight = CalculateContentSize(effectiveTotal);
                content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, contentHeight);
            }
            else
            {
                float contentWidth = CalculateContentSize(effectiveTotal);
                content.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, contentWidth);
            }

            // 生成池内初始可见项（reuse prefab）
            for (int i = 0; i < visibleCount; i++)
            {
                RectTransform rt;
                if (i < pooledItems.Count && pooledItems[i] != null)
                {
                    rt = pooledItems[i];
                    pooledDataIndices[i] = -1;
                }
                else
                {
                    var go = Instantiate(itemPrefab.gameObject, content);
                    rt = go.GetComponent<RectTransform>();
                    if (i < pooledItems.Count)
                    {
                        pooledItems[i] = rt;
                        pooledItemComps[i] = rt.GetComponent<LoopItemBase>();
                        pooledDataIndices[i] = -1;
                    }
                    else
                    {
                        pooledItems.Add(rt);
                        pooledItemComps.Add(rt.GetComponent<LoopItemBase>());
                        pooledDataIndices.Add(-1);
                    }
                }
                rt.gameObject.SetActive(false);
                rt.localScale = Vector3.one;

                rt.pivot = new Vector2(0, 1);

                if (direction == Direction.Vertical)
                {
                    // 纵向滚动：项在横向拉伸，自动跟随 content 宽度
                    rt.anchorMin = new Vector2(0, 1);
                    rt.anchorMax = new Vector2(1, 1);
                    rt.sizeDelta = new Vector2(0, itemSize); // 宽度自适应 (0 offset)，高度为 itemSize
                    rt.anchoredPosition = new Vector2(0f, -i * (itemSize + spacing));
                }
                else
                {
                    // 横向滚动：项在纵向拉伸，自动跟随 content 高度
                    rt.anchorMin = new Vector2(0, 0);
                    rt.anchorMax = new Vector2(0, 1);
                    rt.sizeDelta = new Vector2(itemSize, 0); // 宽度为 itemSize，高度自适应 (0 offset)
                    rt.anchoredPosition = new Vector2(i * (itemSize + spacing), 0f);
                }

            }

            // 如果启用了循环并且有数据，初始定位到中间 cycle 的起点，避免立刻到达边缘
            if (enableLooping && totalCount > 0)
            {
                int startFirst = initialized
                    ? totalCount + Mathf.FloorToInt(Mathf.Repeat(previousOffset, totalCount * Step) / Step)
                    : totalCount;
                currentFirstIndex = startFirst;
                Vector2 pos = content.anchoredPosition;

                // 在程序化设置位置时忽略 OnScroll 回调以避免重入
                isRecentering = true;
                try
                {
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
                }
                finally
                {
                    isRecentering = false;
                }
            }
            else
            {
                SetOffset(Mathf.Clamp(Offset, 0f, MaxOffset));
                scrollRect.StopMovement();
            }
        }

    }
}