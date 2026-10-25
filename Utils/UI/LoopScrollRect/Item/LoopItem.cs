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
            // 仅名字变化时重命名：高频滚动下每次绑定都写 gameObject.name 会产生字符串分配，
            // 重名还会触发 Unity 自动追加 "(1)" 后缀（运行期 Hierarchy 命名无收益）
            if (gameObject.name != name)
            {
                gameObject.name = name;
            }
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