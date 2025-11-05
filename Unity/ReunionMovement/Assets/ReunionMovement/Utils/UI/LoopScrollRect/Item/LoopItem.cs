using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ReunionMovement.Common.Util
{
    public class LoopItem : LoopItemBase
    {
        public TextMeshProUGUI itemName;

        // Selection visuals
        public Color normalColor = Color.white;
        public Color selectedColor = Color.yellow;
        bool isSelected = false;

        /// <summary>
        /// 绑定数据到项
        /// </summary>
        /// <param name="index"></param>
        /// <param name="name"></param>
        public override void Set(int index, string name)
        {
            this.index = index;
            if (itemName != null)
            {
                itemName.text = name;
            }
            gameObject.name = name;
            // 确保视觉效果反映当前的选择状态
            UpdateVisual();
        }

        /// <summary>
        /// 设置选中状态
        /// </summary>
        /// <param name="selected"></param>
        public override void SetSelected(bool selected)
        {
            isSelected = selected;
            UpdateVisual();
        }

        /// <summary>
        /// 更新视觉效果
        /// </summary>
        void UpdateVisual()
        {
            if (itemName != null)
            {
                itemName.color = isSelected ? selectedColor : normalColor;
            }
        }
    }
}