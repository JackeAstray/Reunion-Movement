using ReunionMovement.Common;
using Cysharp.Threading.Tasks;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
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

        //按下时间（Time.unscaledTime，-1 表示未按下；不用墙钟，避免系统时间跳变误判）
        private float pressStartTime = -1f;

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

        // 新增：是否启用输入与键表（支持键盘 Space/Enter 与手柄 Gamepad.buttonSouth 为默认）
        [SerializeField]
        private bool enableInput = true;
        [SerializeField]
        private bool enableKeyboard = true;
        [SerializeField]
        private bool enableGamepad = true;

        [SerializeField]
        private UnityEngine.InputSystem.Key[] keyboardTriggerKeys = new UnityEngine.InputSystem.Key[] { UnityEngine.InputSystem.Key.Space, UnityEngine.InputSystem.Key.Enter };

        [SerializeField]
        private ButtonInputHelper.GamepadButtonType[] gamepadTriggerButtons = new ButtonInputHelper.GamepadButtonType[] { ButtonInputHelper.GamepadButtonType.South };

        // 输入按下标识（支持键盘 Space/Enter 与手柄 Gamepad.buttonSouth）
        private bool inputPressed = false;

        /// <summary>
        /// 长按
        /// </summary>
        private void TriggerLongClick()
        {
            Log.Debug("[LongClickButton] 长按事件触发。");
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

        private bool KeyboardPressedThisFrame()
        {
            return ButtonInputHelper.KeyboardPressedThisFrame(enableInput, enableKeyboard, keyboardTriggerKeys);
        }

        private bool KeyboardReleasedThisFrame()
        {
            return ButtonInputHelper.KeyboardReleasedThisFrame(enableInput, enableKeyboard, keyboardTriggerKeys);
        }

        private bool GamepadPressedThisFrame()
        {
            return ButtonInputHelper.GamepadPressedThisFrame(enableInput, enableGamepad, gamepadTriggerButtons);
        }

        private bool GamepadReleasedThisFrame()
        {
            return ButtonInputHelper.GamepadReleasedThisFrame(enableInput, enableGamepad, gamepadTriggerButtons);
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

                // 使用配置的键表进行检测
                if (KeyboardPressedThisFrame()) pressedThisFrame = true;
                if (KeyboardReleasedThisFrame()) releasedThisFrame = true;

                if (!pressedThisFrame && GamepadPressedThisFrame()) pressedThisFrame = true;
                if (!releasedThisFrame && GamepadReleasedThisFrame()) releasedThisFrame = true;

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
            if (pressStartTime < 0f)
            {
                pressStartTime = Time.unscaledTime;
                longPressCts = new CancellationTokenSource();
                progressCts = new CancellationTokenSource();
                StartLongPressingAsync(longPressCts.Token).Forget();
                StartProgressBarAsync(progressCts.Token).Forget();
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

            // 按压可能已被取消（OnPointerExit / 失去选中）：取消状态下抬起不再处理长按判定，
            // 也不触发 onButtonUp（移出按钮后的抬起不应算完整点击）
            if (pressStartTime >= 0f)
            {
                // 使用 unscaledTime 而非墙钟：与 onLongPressing 协程/进度条的 unscaled 基准保持一致；
                // 系统时间被 NTP 校准/手动调整时会跳变导致长按误判，且暂停（timeScale=0）不应累积按压时长
                float pressDuration = Time.unscaledTime - pressStartTime;
                if (pressDuration > longPressDuration)
                {
                    TriggerLongClick();
                }
                else
                {
                    ResetPressTime();
                }

                // 触发按钮抬起事件
                onButtonUp?.Invoke();
            }
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
            pressStartTime = -1f;
        }

        /// <summary>
        /// 长按协程
        /// </summary>
        private async UniTaskVoid StartLongPressingAsync(CancellationToken token)
        {
            // 统一 unscaled 时间基准：进度条用 unscaledDeltaTime，若此处受 timeScale 影响，
            // timeScale=0 时进度条照常涨满但 onLongPressing 永不触发（两处状态互相矛盾）
            bool canceled = await UniTask.Delay((int)(longPressDuration * 1000f), ignoreTimeScale: true, cancellationToken: token).SuppressCancellationThrow();
            if (!canceled) onLongPressing?.Invoke();
        }

        /// <summary>
        /// 进度条动画协程
        /// </summary>
        private async UniTaskVoid StartProgressBarAsync(CancellationToken token)
        {
            try
            {
                if (progressBar == null) return;
                progressBar.gameObject.SetActive(true);
                progressBar.fillAmount = 0f;
                float t = 0f;
                while (t < longPressDuration && !token.IsCancellationRequested)
                {
                    t += Time.unscaledDeltaTime;
                    progressBar.fillAmount = Mathf.Clamp01(t / longPressDuration);
                    await UniTask.Yield(PlayerLoopTiming.Update);
                }
                if (!token.IsCancellationRequested)
                    progressBar.fillAmount = 1f;
            }
            catch (System.Exception ex)
            {
                Log.Error($"[LongClickButton] 进度条动画异常: {ex.Message}");
            }
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

        protected override void OnDestroy()
        {
            // 组件销毁时取消长按/进度协程，避免 UniTask 继续运行访问已销毁的 progressBar，
            // 每帧抛 MissingReferenceException 直到计时结束
            CancelPress();
            ResetPressTime();
            base.OnDestroy();
        }
    }
}