using UnityEngine;
using UnityEngine.EventSystems;

namespace Gear.UI.Select
{
    /// <summary>
    /// 把选项上的鼠标悬停/滚轮事件转发给轮播控制器。
    /// 由 CarouselSelectionController 在运行时自动挂到每个选项上；
    /// 点击仍走 Button.onClick，避免与 EventSystem 的提交/点击逻辑重复。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CarouselPointerBridge : MonoBehaviour, IPointerEnterHandler, IScrollHandler
    {
        public CarouselSelectionController Owner;
        public int Index;

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (Owner == null) return;
            Owner.OnPointerEnterOption(Index);
        }

        public void OnScroll(PointerEventData eventData)
        {
            if (Owner == null) return;
            Owner.OnPointerScroll(eventData.scrollDelta.y);
        }
    }
}
