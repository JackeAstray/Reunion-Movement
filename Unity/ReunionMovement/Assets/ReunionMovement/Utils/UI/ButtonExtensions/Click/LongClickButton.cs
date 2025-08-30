using ReunionMovement.Common;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

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

        // 输入按下标识（支持键盘 Space/Enter 与手柄 Gamepad.buttonSouth）
        private bool inputPressed = false;

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
        /// 按下（鼠标）
        /// </summary>
        /// <param name="eventData"></param>
        public override void OnPointerDown(PointerEventData eventData)
        {
            base.OnPointerDown(eventData);
            StartPressIfNeeded();
        }

        /// <summary>
        /// 抬起（鼠标）
        /// </summary>
        /// <param name="eventData"></param>
        public override void OnPointerUp(PointerEventData eventData)
        {
            base.OnPointerUp(eventData);
            EndPressAndHandle();
        }

        /// <summary>
        /// 离开（鼠标）
        /// </summary>
        /// <param name="eventData"></param>
        public override void OnPointerExit(PointerEventData eventData)
        {
            base.OnPointerExit(eventData);
            CancelPress();
            ResetPressTime();
        }

        /// <summary>
        /// Update 用于检测键盘与手柄长按（当该按钮为当前选中时）
        /// 使用 Unity 新 Input System
        /// </summary>
        private void Update()
        {
            if (EventSystem.current == null) return;

            bool isSelected = EventSystem.current.currentSelectedGameObject == gameObject;

            if (isSelected)
            {
                bool pressedThisFrame = false;
                bool releasedThisFrame = false;

                // 键盘检测（Space / Enter）
                if (Keyboard.current != null)
                {
                    var space = Keyboard.current.spaceKey;
                    var enter = Keyboard.current.enterKey;
                    if (space.wasPressedThisFrame || enter.wasPressedThisFrame) pressedThisFrame = true;
                    if (space.wasReleasedThisFrame || enter.wasReleasedThisFrame) releasedThisFrame = true;
                }

                // 手柄检测（Gamepad 主键 buttonSouth，通常为 A）
                if (!pressedThisFrame && Gamepad.current != null)
                {
                    if (Gamepad.current.buttonSouth.wasPressedThisFrame) pressedThisFrame = true;
                }
                if (!releasedThisFrame && Gamepad.current != null)
                {
                    if (Gamepad.current.buttonSouth.wasReleasedThisFrame) releasedThisFrame = true;
                }

                // 按下开始（仅在之前未按下时触发）
                if (pressedThisFrame && !inputPressed)
                {
                    inputPressed = true;
                    StartPressIfNeeded();
                }

                // 抬起结束（仅在之前已按下时触发）
                if (releasedThisFrame && inputPressed)
                {
                    inputPressed = false;
                    EndPressAndHandle();
                }
            }
            else
            {
                // 如果失去选中且仍处于按下状态，则取消
                if (inputPressed)
                {
                    inputPressed = false;
                    CancelPress();
                    ResetPressTime();
                }
            }
        }

        /// <summary>
        /// 开始按下行为（用于鼠标和键盘/手柄）
        /// </summary>
        private void StartPressIfNeeded()
        {
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
        /// 结束按下并根据时长处理（用于鼠标和键盘/手柄抬起）
        /// </summary>
        private void EndPressAndHandle()
        {
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
        /// 取消按下（用于鼠标移出或键盘/手柄取消场景）
        /// </summary>
        private void CancelPress()
        {
            longPressCts?.Cancel();
            longPressCts = null;
            progressCts?.Cancel();
            progressCts = null;
            ResetProgressBar();
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

        protected override void OnDisable()
        {
            base.OnDisable();
            // 失活时取消所有正在进行的按下/进度逻辑，避免残留状态
            inputPressed = false;
            CancelPress();
            ResetPressTime();
            ResetProgressBar();
        }
    }
}