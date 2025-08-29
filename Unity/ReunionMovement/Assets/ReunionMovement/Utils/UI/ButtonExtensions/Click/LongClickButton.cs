using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using ReunionMovement.Common;

namespace ReunionMovement.UI.ButtonClick
{
    /// <summary>
    /// 长按按钮
    /// </summary>
    public class LongClickButton : Button
    {
        [SerializeField]
        private ButtonClickEvent longClick = new ButtonClickEvent();

        public ButtonClickEvent onLongClick
        {
            get { return longClick; }
            set { longClick = value; }
        }

        // 新增的按钮抬起事件
        [SerializeField]
        private ButtonClickEvent buttonUp = new ButtonClickEvent();

        public ButtonClickEvent onButtonUp
        {
            get { return buttonUp; }
            set { buttonUp = value; }
        }

        // 新增的长按未抬起事件
        [SerializeField]
        private ButtonClickEvent longPressing = new ButtonClickEvent();

        public ButtonClickEvent onLongPressing
        {
            get { return longPressing; }
            set { longPressing = value; }
        }

        // 进度条（需在Inspector拖拽绑定，类型为Image，FillMethod建议Horizontal/Vertical/Radial）
        [SerializeField]
        public Image progressBar;

        //按下时间
        private DateTime pressStartTime;
        //长按取消令牌
        private CancellationTokenSource longPressCts;
        //进度条动画取消令牌
        private CancellationTokenSource progressCts;
        //长按判定时长
        [SerializeField]
        private float longPressDuration = 0.6f;
        public float LongPressDuration
        {
            get => longPressDuration;
            set => longPressDuration = value;
        }

        /// <summary>
        /// 长按
        /// </summary>
        private void TriggerLongClick()
        {
            Log.Debug($"[LongClickButton] 长按事件触发。");
            onLongClick?.Invoke();
            ResetPressTime();
        }

        /// <summary>
        /// 按下
        /// </summary>
        /// <param name="eventData"></param>
        public override void OnPointerDown(PointerEventData eventData)
        {
            base.OnPointerDown(eventData);
            if (pressStartTime == default)
            {
                pressStartTime = DateTime.Now;
                longPressCts = new CancellationTokenSource();
                progressCts = new CancellationTokenSource();
                StartLongPressingCoroutine(longPressCts.Token);
                StartProgressBarCoroutine(progressCts.Token);
            }
        }

        /// <summary>
        /// 抬起
        /// </summary>
        /// <param name="eventData"></param>
        public override void OnPointerUp(PointerEventData eventData)
        {
            base.OnPointerUp(eventData);
            longPressCts?.Cancel();
            longPressCts = null;
            progressCts?.Cancel();
            progressCts = null;
            ResetProgressBar();

            if (pressStartTime != default)
            {
                var pressDuration = DateTime.Now - pressStartTime;
                if (pressDuration.TotalMilliseconds > longPressDuration * 1000f)
                {
                    TriggerLongClick();
                }
                else
                {
                    ResetPressTime();
                }
            }

            // 触发按钮抬起事件
            onButtonUp?.Invoke();
        }

        /// <summary>
        /// 离开
        /// </summary>
        /// <param name="eventData"></param>
        public override void OnPointerExit(PointerEventData eventData)
        {
            base.OnPointerExit(eventData);
            longPressCts?.Cancel();
            longPressCts = null;
            progressCts?.Cancel();
            progressCts = null;
            ResetProgressBar();
            ResetPressTime();
        }

        /// <summary>
        /// 重置时间
        /// </summary>
        private void ResetPressTime()
        {
            pressStartTime = default;
        }

        /// <summary>
        /// 长按协程
        /// </summary>
        private async void StartLongPressingCoroutine(CancellationToken token)
        {
            try
            {
                await Task.Delay((int)(longPressDuration * 1000f), token);
                onLongPressing?.Invoke();
            }
            catch (TaskCanceledException)
            {
                Log.Debug("[LongClickButton] 长按协程被取消。");
            }
        }

        /// <summary>
        /// 进度条动画协程
        /// </summary>
        private async void StartProgressBarCoroutine(CancellationToken token)
        {
            if (progressBar == null) return;
            progressBar.gameObject.SetActive(true);
            progressBar.fillAmount = 0f;
            float t = 0f;
            while (t < longPressDuration)
            {
                if (token.IsCancellationRequested) break;
                t += Time.unscaledDeltaTime;
                progressBar.fillAmount = Mathf.Clamp01(t / longPressDuration);
                await Task.Yield();
            }
            if (!token.IsCancellationRequested)
                progressBar.fillAmount = 1f;
        }

        /// <summary>
        /// 重置进度条
        /// </summary>
        private void ResetProgressBar()
        {
            if (progressBar != null)
            {
                progressBar.fillAmount = 0f;
                progressBar.gameObject.SetActive(false);
            }
        }
    }
}