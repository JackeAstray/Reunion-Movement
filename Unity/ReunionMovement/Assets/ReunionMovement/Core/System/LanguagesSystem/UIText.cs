using ReunionMovement.Common;
using R3;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace ReunionMovement.Core.Languages
{
    /// <summary>
    /// UI 多语言文本组件 —— 通过 R3 订阅语言切换事件自动更新
    /// </summary>
    public class UIText : MonoBehaviour
    {
        // 语言文本ID
        [SerializeField] private int number;

        private TMP_Text tmpTextComponent;
        private Text textComponent;

        /// <summary>R3 订阅管理器 —— OnDestroy 时自动取消所有订阅</summary>
        private IDisposable languageSubscription;
        private IDisposable initSubscription;
        /// <summary>初始化完成回调是否已执行（防止订阅+直接调用双路径重复初始化）</summary>
        private bool isInitFinished;

        void Start()
        {
            // 先订阅初始化广播，再检查引擎是否已运行：
            // 若先检查后订阅，引擎在检查与订阅之间完成初始化（OnInitializedSubject 已广播、无重放），
            // 事件会被错过，文本永不更新。
            // 注意：引擎 Dispose 后 static Subject 会被置 null（重建前），
            // 此时直接订阅会 NRE，判空跳过（本组件随场景重建后重新 Start）。
            var initSubject = GameEngine.OnInitializedSubject;
            if (initSubject != null)
            {
                initSubscription = initSubject.Subscribe(_ => OnGameInitFinished());
            }

            if (GameEngine.Current != null && GameEngine.Current.State == EngineState.Running)
            {
                OnGameInitFinished();
            }
        }

        /// <summary>
        /// 游戏初始化完成后的回调方法，注册 R3 语言订阅并更新文本
        /// </summary>
        private void OnGameInitFinished()
        {
            // 幂等：订阅路径与 Running 直调路径可能同时命中，避免重复初始化/重复订阅
            if (isInitFinished) return;
            isInitFinished = true;

            tmpTextComponent = GetComponent<TMP_Text>();
            textComponent = GetComponent<Text>();
            if (tmpTextComponent == null && textComponent == null)
            {
                Log.Error("UIText组件需要绑定TMP_Text或Text组件");
                return;
            }

            // 语言系统 Clear() 后 CurrentLanguage 为 null（未重新 Init），判空避免 NRE
            var currentLanguage = LanguagesSystem.Instance.CurrentLanguage;
            if (currentLanguage == null)
            {
                Log.Warning("UIText: LanguagesSystem 已清理（CurrentLanguage 为 null），跳过语言订阅");
                return;
            }

            // 使用 R3 订阅语言切换 —— 自动处理订阅生命周期
            languageSubscription = currentLanguage
                .Subscribe(_ => GetTextLanguage());

            // 首次更新文本
            GetTextLanguage();

            // 初始化完成后释放 initSubscription（仅需一次）
            initSubscription?.Dispose();
            initSubscription = null;
        }

        private void OnDestroy()
        {
            // 释放 R3 订阅（无需手动 -=，IDisposable 自动管理）
            languageSubscription?.Dispose();
            languageSubscription = null;
            initSubscription?.Dispose();
            initSubscription = null;
        }

        /// <summary>
        /// 设置文本ID
        /// </summary>
        /// <param name="id"></param>
        public void SetNumber(int number)
        {
            this.number = number;
            GetTextLanguage();
        }

        /// <summary>
        /// 获取当前语言的文本
        /// </summary>
        public void GetTextLanguage()
        {
            if (LanguagesSystem.Instance == null)
            {
                return;
            }

            string value = LanguagesSystem.Instance.GetTextById(number);

            if (!string.IsNullOrEmpty(value))
            {
                // 设置文本组件的文本
                if (tmpTextComponent != null)
                {
                    tmpTextComponent.text = value;
                }

                if (textComponent != null)
                {
                    textComponent.text = value;
                }
            }
            else
            {
                Log.Debug("GetTextLanguage() " + number + "是空的");
            }
        }
    }
}
