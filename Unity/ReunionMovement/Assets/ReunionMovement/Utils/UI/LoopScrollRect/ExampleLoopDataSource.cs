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
            LoopItem loopItem = item.GetComponent<LoopItem>();
            if (loopItem)
            {
                loopItem.Set(dataIndex, $"Item {dataIndex}");
            }
        }
    }
}
