using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ReunionMovement.Common.Util
{
    /// <summary>
    /// 用于 LoopScrollRect 的项基类。
    /// 子类应重写 Set(...) 来绑定数据，可重写 SetSelected 来更改选中视觉。
    /// </summary>
    public abstract class LoopItemBase : MonoBehaviour, IPointerClickHandler
    {
        // 数据索引（由 LoopScrollRect 维护）
        public int index = -1;

        // 点击回调（由 LoopScrollRect 赋值）
        public Action<int> onClick;

        /// <summary>
        /// 将数据绑定到此项。子类应实现具体绑定逻辑。
        /// </summary>
        public abstract void Set(int index, string name);

        /// <summary>
        /// 设置视觉上的选中状态。子类可覆盖实现具体表现。
        /// </summary>
        public virtual void SetSelected(bool selected) { }

        /// <summary>
        /// 点击事件处理
        /// </summary>
        /// <param name="eventData"></param>
        public void OnPointerClick(PointerEventData eventData)
        {
            onClick?.Invoke(index);
        }
    }
}
