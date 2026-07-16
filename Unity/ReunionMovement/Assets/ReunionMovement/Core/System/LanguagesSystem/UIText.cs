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

        void Start()
        {
            // 如果游戏引擎已经运行完毕，直接执行；否则等待初始化完成的广播
            if (GameEngine.Current != null && GameEngine.Current.State == EngineState.Running)
            {
                OnGameInitFinished();
            }
            else
            {
                GameEngine.OnInitialized += OnGameInitFinished;
            }
        }

        /// <summary>
        /// 游戏初始化完成后的回调方法，注册 R3 语言订阅并更新文本
        /// </summary>
        private void OnGameInitFinished()
        {
            tmpTextComponent = GetComponent<TMP_Text>();
            textComponent = GetComponent<Text>();
            if (tmpTextComponent == null && textComponent == null)
            {
                Log.Error("UIText组件需要绑定TMP_Text或Text组件");
                return;
            }

            // 使用 R3 订阅语言切换 —— 自动处理订阅生命周期
            languageSubscription = LanguagesSystem.Instance.CurrentLanguage
                .Subscribe(_ => GetTextLanguage());

            // 首次更新文本
            GetTextLanguage();
        }

        private void OnDestroy()
        {
            // 取消订阅静态事件，防止悬空引用
            GameEngine.OnInitialized -= OnGameInitFinished;

            // 释放 R3 订阅
            languageSubscription?.Dispose();
            languageSubscription = null;
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
