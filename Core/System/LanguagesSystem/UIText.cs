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
        private IDisposable readySubscription;
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

            // 语言系统就绪订阅（OnReady 是稳定实例 Subject，Clear 不销毁）：
            // 覆盖“引擎运行中语言系统 Clear→Init，旧 CurrentLanguage 订阅已失效”的场景，
            // 否则本组件标记 isInitFinished 后永不重订阅，文本永久停留在旧语言。
            readySubscription = LanguagesSystem.Instance.OnReady.Subscribe(_ => OnLanguageReady());

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

            tmpTextComponent = GetComponent<TMP_Text>();
            textComponent = GetComponent<Text>();
            if (tmpTextComponent == null && textComponent == null)
            {
                // 组件配置缺失：标记完成，避免每次广播重试刷屏
                isInitFinished = true;
                Log.Error("UIText组件需要绑定TMP_Text或Text组件");
                return;
            }

            // 语言系统 Clear() 后 CurrentLanguage 为 null（未重新 Init）：保持“未完成”状态，
            // 等待 OnLanguageReady（语言系统重新 Init 广播）或引擎再次初始化广播时重试。
            // 注意：isInitFinished 必须保持 false，否则重建后永不重订阅。
            var currentLanguage = LanguagesSystem.Instance.CurrentLanguage;
            if (currentLanguage == null)
            {
                Log.Warning("UIText: LanguagesSystem 已清理（CurrentLanguage 为 null），等待其重新初始化");
                return;
            }

            isInitFinished = true;
            BindLanguage(currentLanguage);

            // 初始化完成后释放 initSubscription（仅需一次）
            initSubscription?.Dispose();
            initSubscription = null;
        }

        /// <summary>
        /// 语言系统重新就绪：重绑最新 CurrentLanguage 并刷新文本。
        /// （覆盖 Clear→Init 场景：旧 ReactiveProperty 已在 Clear 时 Dispose，订阅自动失效）
        /// </summary>
        private void OnLanguageReady()
        {
            var currentLanguage = LanguagesSystem.Instance.CurrentLanguage;
            if (currentLanguage == null) return;
            isInitFinished = true;
            BindLanguage(currentLanguage);
        }

        /// <summary>绑定语言订阅并首次更新文本（重复调用安全：旧订阅先 Dispose）</summary>
        private void BindLanguage(ReactiveProperty<Multilingual> currentLanguage)
        {
            languageSubscription?.Dispose();
            languageSubscription = currentLanguage.Subscribe(_ => GetTextLanguage());
            GetTextLanguage();
        }

        private void OnDestroy()
        {
            // 释放 R3 订阅（无需手动 -=，IDisposable 自动管理）
            languageSubscription?.Dispose();
            languageSubscription = null;
            initSubscription?.Dispose();
            initSubscription = null;
            readySubscription?.Dispose();
            readySubscription = null;
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
