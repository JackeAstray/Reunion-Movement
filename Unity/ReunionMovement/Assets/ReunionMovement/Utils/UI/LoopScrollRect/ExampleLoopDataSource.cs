using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;

namespace ReunionMovement.Common.Util
{
    public class ExampleLoopDataSource : MonoBehaviour, LoopScrollRect.IDataSource
    {
        public LoopScrollRect loop;
        public int itemTotal = 100;

        // 实际数据存储
        List<string> items = new List<string>();

        void Awake()
        {
            // 构造初始数据
            for (int i = 0; i < itemTotal; i++)
            {
                items.Add($"Item {i}");
            }
        }

        void Start()
        {
            loop.Initialize(this);
            loop.onPullStart.AddListener(Refresh);
            loop.onPullEnd.AddListener(Load);
        }

        public void OnDestroy()
        {
            loop.onPullStart.RemoveListener(Refresh);
            loop.onPullEnd.RemoveListener(Load);
        }

        /// <summary>
        /// 返回真实数据数量
        /// </summary>
        /// <returns></returns>
        public int GetItemCount() => items.Count;

        /// <summary>
        /// 使用 items 列表的内容绑定到 Item 上
        /// </summary>
        /// <param name="item"></param>
        /// <param name="dataIndex"></param>
        public void BindItem(RectTransform item, int dataIndex)
        {
            if (dataIndex < 0 || dataIndex >= items.Count) return;

            LoopItem loopItem = item.GetComponent<LoopItem>();
            if (loopItem)
            {
                loopItem.Set(dataIndex, items[dataIndex]);
            }
        }

        /// <summary>
        /// 刷新
        /// </summary>
        public void Refresh()
        {
            Log.Info("Refresh");
            loop.CompletePullStart();
        }

        /// <summary>
        /// 加载
        /// </summary>
        public void Load()
        {
            Log.Info("Load");
            loop.CompletePullEnd();
        }

        /// <summary>
        /// 跳转到指定索引
        /// </summary>
        /// <param name="index"></param>
        public void JumpToIndex(int index)
        {
            loop.JumpToIndex(index);
        }

        /// <summary>
        /// 在指定索引处插入数据
        /// </summary>
        /// <param name="index"></param>
        /// <param name="text"></param>
        public void AddItemAt(int index, string text)
        {
            if (index < 0) index = 0;
            if (index > items.Count) index = items.Count;
            items.Insert(index, text);
            // 通知 LoopScrollRect 更新（必须在主线程）
            loop.NotifyDataSetChanged();
        }

        /// <summary>
        /// 移除指定索引的数据
        /// </summary>
        /// <param name="index"></param>
        public void RemoveItemAt(int index)
        {
            if (index < 0 || index >= items.Count)
            {
                return;
            }
            items.RemoveAt(index);
            loop.NotifyDataSetChanged();
        }

        /// <summary>
        /// 添加数据到末尾
        /// </summary>
        /// <param name="text"></param>
        public void AddItem(string text)
        {
            items.Add(text);
            loop.NotifyDataSetChanged();
        }
    }
}