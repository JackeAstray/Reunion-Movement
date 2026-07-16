using ReunionMovement.Common;
using ReunionMovement.Core.Base;
using ReunionMovement.Core.Resources;
using ReunionMovement.Core.Sound;
using Cysharp.Threading.Tasks;
using R3;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace ReunionMovement.Core.Languages
{
    /// <summary>
    /// 语言系统 —— 使用 R3 ReactiveProperty 管理语言切换通知
    /// </summary>
    public class LanguagesSystem : ICustomSystem
    {
        #region 单例与初始化
        private static readonly Lazy<LanguagesSystem> instance = new(() => new LanguagesSystem());
        public static LanguagesSystem Instance => instance.Value;

        public bool isInited { get; private set; }

        private double initProgress = 0;
        public double InitProgress { get { return initProgress; } }
        #endregion

        /// <summary>R3 响应式语言属性 —— 值变化时自动通知所有订阅者</summary>
        public ReactiveProperty<Multilingual> CurrentLanguage { get; private set; }
            = new ReactiveProperty<Multilingual>(Multilingual.ZH_CN);

        /// <summary>兼容旧代码的非响应式访问器</summary>
        public Multilingual multilingual
        {
            get => CurrentLanguage.Value;
            set => CurrentLanguage.Value = value;
        }

        private LanguagesContainer languagesContainer;
        private Dictionary<int, LanguagesConfig> languagesDict;
        // 多语言枚举 → 文本字段选择器（避免 switch-case，支持扩展新语言）
        private static readonly Dictionary<Multilingual, Func<LanguagesConfig, string>> languageSelectors =
            new Dictionary<Multilingual, Func<LanguagesConfig, string>>
            {
                { Multilingual.ZH_CN, c => c.ZH_CN },
                { Multilingual.EN_US, c => c.EN_US },
                { Multilingual.RU_RU, c => c.RU_RU },
                { Multilingual.JA_JP, c => c.JA_JP },
            };

        public UniTask Init()
        {
            initProgress = 0;

            // 从ScriptableObjects中获取文本
            languagesContainer = ResourcesSystem.Instance.Load<LanguagesContainer>("ScriptableObjects/LanguagesContainer");
            if (languagesContainer == null || languagesContainer.configs == null)
            {
                Log.Error("LanguagesContainer或其configs为空, 语言系统初始化失败!");
            }
            else
            {
                // 构建字典以加速查找 O(1)
                languagesDict = new Dictionary<int, LanguagesConfig>(languagesContainer.configs.Count);
                foreach (var lang in languagesContainer.configs)
                {
                    languagesDict[lang.Number] = lang;
                }
            }

            initProgress = 100;
            isInited = true;
            Log.Debug("LanguagesSystem 初始化完成");
            return UniTask.CompletedTask;
        }

        public void Update(float logicTime, float realTime)
        {

        }

        public void Clear()
        {
            Log.Debug("LanguagesSystem 清除数据");
            // 释放 R3 ReactiveProperty，自动断开所有订阅
            CurrentLanguage?.Dispose();
            CurrentLanguage = null;
            // 重置初始化状态和相关数据
            isInited = false;
            initProgress = 0;
            languagesContainer = null;
            languagesDict = null;
        }

        /// <summary>
        /// 获取当前多语言设置
        /// </summary>
        /// <returns></returns>
        public Multilingual GetMultilingual()
        {
            return multilingual;
        }

        /// <summary>
        /// 设置多语言 —— 通过 ReactiveProperty 自动通知所有订阅者
        /// </summary>
        /// <param name="multilingual"></param>
        public void SetMultilingual(Multilingual multilingual)
        {
            CurrentLanguage.Value = multilingual;
        }

        /// <summary>
        /// 根据ID获取对应的文本
        /// </summary>
        /// <param name="number"></param>
        /// <returns></returns>
        public string GetTextById(int number)
        {
            if (languagesContainer != null && languagesContainer.configs != null && languagesDict != null)
            {
                if (languagesDict.TryGetValue(number, out var language))
                {
                    // 使用字典映射代替 switch-case，便于扩展新语言
                    if (languageSelectors.TryGetValue(multilingual, out var selector))
                    {
                        return selector(language);
                    }
                    // 默认返回中文
                    return language.ZH_CN;
                }
                else
                {
                    Log.Error("未找到ID为{0}的语言配置", number);
                }
            }
            else
            {
                Log.Error("LanguagesContainer或configs为空");
            }

            return string.Empty; // 如果未找到对应的文本，返回空字符串
        }
    }
}