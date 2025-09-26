using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

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
    /// </summary>
    public class LoopScrollRect : MonoBehaviour
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
        // 多创建几个作缓冲
        public int extraBuffer = 2;

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

            // 如果 content 有布局组件并且你希望手动控制大小，可能需要强制重建布局后再次设置尺寸：
            // LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            // （或者禁用 HorizontalLayoutGroup/ContentSizeFitter，或让布局组件负责尺寸）

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

                // 可根据需要显式设置尺寸，确保与 itemSize 对齐（当 prefab 使用 LayoutElement 时，这一步视情况而定）
                if (direction == Direction.Vertical)
                {
                    rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, itemSize);
                }
                else
                {
                    rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, itemSize);
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
    }
}