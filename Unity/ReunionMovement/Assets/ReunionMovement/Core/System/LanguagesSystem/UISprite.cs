using ReunionMovement.Common;
using R3;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ReunionMovement.Core.Languages
{
    /// <summary>
    /// UI 多语言图片组件 —— 按当前语言切换 Sprite（配 Image 组件）。
    /// 与 UIText 相同的事件驱动模式：订阅语言系统 OnReady（稳定源）与 CurrentLanguage，
    /// 覆盖“引擎运行中语言系统 Clear→Init，旧订阅失效”的重订阅场景。
    /// 语言缺失时回退中文，再回退保持当前图片不变。
    /// </summary>
    public class UISprite : MonoBehaviour
    {
        [Serializable]
        public class LanguageSpriteEntry
        {
            [Tooltip("语言")]
            public Multilingual language;
            [Tooltip("该语言对应的图片")]
            public Sprite sprite;
        }

        [SerializeField]
        [Tooltip("各语言的图片映射（缺失语言回退中文）")]
        private List<LanguageSpriteEntry> sprites = new List<LanguageSpriteEntry>();

        private Image image;
        private IDisposable languageSubscription;
        private IDisposable readySubscription;

        void Start()
        {
            image = GetComponent<Image>();
            if (image == null)
            {
                Log.Error("UISprite 组件需要绑定 Image 组件", this);
                return;
            }

            // 语言系统就绪订阅（OnReady 是稳定实例 Subject，Clear 不销毁）：
            // 覆盖“引擎运行中语言系统 Clear→Init，旧 CurrentLanguage 订阅已失效”的场景
            readySubscription = LanguagesSystem.Instance.OnReady.Subscribe(_ => OnLanguageReady());

            if (LanguagesSystem.Instance.CurrentLanguage != null)
            {
                BindLanguage(LanguagesSystem.Instance.CurrentLanguage);
            }
        }

        /// <summary>语言系统重新就绪：重绑最新 CurrentLanguage 并刷新图片</summary>
        private void OnLanguageReady()
        {
            var currentLanguage = LanguagesSystem.Instance.CurrentLanguage;
            if (currentLanguage == null) return;
            BindLanguage(currentLanguage);
        }

        /// <summary>绑定语言订阅并立即刷新（重复调用安全：旧订阅先 Dispose）</summary>
        private void BindLanguage(ReactiveProperty<Multilingual> currentLanguage)
        {
            languageSubscription?.Dispose();
            languageSubscription = currentLanguage.Subscribe(_ => ApplyLanguage());
            ApplyLanguage();
        }

        /// <summary>按当前语言查找并应用图片</summary>
        private void ApplyLanguage()
        {
            if (image == null) return;
            var currentLanguage = LanguagesSystem.Instance.CurrentLanguage;
            if (currentLanguage == null) return;

            var target = FindSprite(currentLanguage.Value);
            if (target != null)
            {
                image.sprite = target;
            }
        }

        /// <summary>查找指定语言图片（缺失回退中文，再回退 null 保持当前不变）</summary>
        private Sprite FindSprite(Multilingual language)
        {
            for (int i = 0; i < sprites.Count; i++)
            {
                if (sprites[i].language == language && sprites[i].sprite != null) return sprites[i].sprite;
            }
            // 语言回退：缺失时回退中文
            for (int i = 0; i < sprites.Count; i++)
            {
                if (sprites[i].language == Multilingual.ZH_CN && sprites[i].sprite != null) return sprites[i].sprite;
            }
            return null;
        }

        private void OnDestroy()
        {
            languageSubscription?.Dispose();
            languageSubscription = null;
            readySubscription?.Dispose();
            readySubscription = null;
        }
    }
}
