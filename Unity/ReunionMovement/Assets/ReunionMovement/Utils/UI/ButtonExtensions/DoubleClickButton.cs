using ReunionMovement.Common;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ReunionMovement.UI.ButtonClick
{
    //双击按钮
    public class DoubleClickButton : Button
    {
        [SerializeField]
        private ButtonClickEvent doubleClick = new ButtonClickEvent();

        public ButtonClickEvent onDoubleClick
        {
            get { return doubleClick; }
            set { doubleClick = value; }
        }

        private DateTime firstTime;
        private DateTime secondTime;

        /// <summary>
        /// 双击
        /// </summary>
        private void Press()
        {
            Log.Debug($"[DoubleClickButton] 双击事件触发。");
            if (onDoubleClick != null)
            {
                onDoubleClick.Invoke();
            }
            resetTime();
        }

        /// <summary>
        /// 按下
        /// </summary>
        /// <param name="eventData"></param>
        public override void OnPointerDown(PointerEventData eventData)
        {
            base.OnPointerDown(eventData);
            if (firstTime.Equals(default(DateTime)))
            {
                firstTime = DateTime.Now;
            }
            else
            {
                secondTime = DateTime.Now;
            }
        }

        /// <summary>
        /// 抬起
        /// </summary>
        /// <param name="eventData"></param>
        public override void OnPointerUp(PointerEventData eventData)
        {
            base.OnPointerUp(eventData);
            if (!firstTime.Equals(default(DateTime)) && !secondTime.Equals(default(DateTime)))
            {
                var intervalTime = secondTime - firstTime;
                float milliSeconds = intervalTime.Seconds * 1000 + intervalTime.Milliseconds;
                Log.Debug($"[DoubleClickButton] 两次点击间隔：{milliSeconds} 毫秒");
                if (milliSeconds < 400)
                {
                    Press();
                }
                else
                {
                    resetTime();
                }
            }
        }

        /// <summary>
        /// 离开
        /// </summary>
        /// <param name="eventData"></param>
        public override void OnPointerExit(PointerEventData eventData)
        {
            base.OnPointerExit(eventData);
            resetTime();
        }

        /// <summary>
        /// 重置时间
        /// </summary>
        private void resetTime()
        {
            firstTime = default(DateTime);
            secondTime = default(DateTime);
        }
    }
}