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

        void Start()
        {
            loop.Initialize(this);
        }

        public int GetItemCount() => itemTotal;

        public void BindItem(RectTransform item, int dataIndex)
        {
            // 假设 item 有一个 Text 子对象显示索引
            var txt = item.GetComponentInChildren<TextMeshProUGUI>();
            if (txt) txt.text = $"Item {dataIndex}";
            // 你也可以在这里设置图片、按钮回调等
        }
    }
}
