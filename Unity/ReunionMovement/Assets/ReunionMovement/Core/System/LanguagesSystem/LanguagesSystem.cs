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
    public class LanguagesSystem : ICustomSystem, ISystemDisposable
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

        /// <summary>兼容旧代码的非响应式访问器（Clear 后未重 Init 时为 null，安全降级为 ZH_CN）</summary>
        public Multilingual multilingual
        {
            get => CurrentLanguage != null ? CurrentLanguage.Value : Multilingual.ZH_CN;
            set
            {
                if (CurrentLanguage != null)
                {
                    CurrentLanguage.Value = value;
                }
                else
                {
                    Log.Warning("LanguagesSystem.multilingual: 系统未初始化（Clear 后未重新 Init），设置被忽略");
                }
            }
        }

        /// <summary>语言系统就绪通知（每次 Init 完成时广播；Clear 不销毁，作为 UIText 等组件的稳定重订阅源）</summary>
        private readonly Subject<Unit> onReadySubject = new Subject<Unit>();
        public Observable<Unit> OnReady => onReadySubject;

        private LanguagesContainer languagesContainer;
        private Dictionary<int, LanguagesConfig> languagesDict;
        /// <summary>已上报过的缺失 ID（首次 Log.Error，后续静默，防止缺项配表运行时刷屏）</summary>
        private readonly HashSet<int> missingKeysReported = new HashSet<int>();
        /// <summary>容器为空错误是否已上报（Init 失败后每次 GetTextById 都走此分支，防刷屏）</summary>
        private bool containerEmptyReported;
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

            // 重建 R3 ReactiveProperty（Clear() 已 Dispose 并置 null，重初始化时必须重建）
            CurrentLanguage ??= new ReactiveProperty<Multilingual>(Multilingual.ZH_CN);

            // 重建字典时清空缺失键缓存：配表更新后允许重新上报缺失
            missingKeysReported.Clear();
            containerEmptyReported = false;

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
                    // configs 中可能混入空项（ScriptableObject 列表未填满），跳过避免 NRE
                    if (lang == null)
                    {
                        Log.Warning("LanguagesSystem: configs 中存在空项，已跳过");
                        continue;
                    }
                    // 重复 Number 键静默覆盖会导致文本被悄悄替换、排查困难，此处显式告警
                    if (languagesDict.ContainsKey(lang.Number))
                    {
                        Log.Error("LanguagesSystem: 配表存在重复 Number={0}，后写配置已覆盖先写，请检查 LanguagesContainer", lang.Number);
                    }
                    languagesDict[lang.Number] = lang;
                }
            }

            initProgress = 100;
            isInited = true;
            // 广播就绪：UIText 等组件据此重绑最新 CurrentLanguage（覆盖 Clear→Init 重订阅场景）
            try
            {
                onReadySubject.OnNext(Unit.Default);
            }
            catch (Exception ex)
            {
                Log.Error("LanguagesSystem OnReady 订阅者异常（已隔离）: {0}", ex.Message);
            }
            Log.Debug("LanguagesSystem 初始化完成");
            return UniTask.CompletedTask;
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
            // Clear() 后（未重新 Init）CurrentLanguage 为 null，判空避免 NRE
            if (CurrentLanguage == null)
            {
                Log.Warning("LanguagesSystem.SetMultilingual: 系统未初始化（Clear 后未重新 Init），设置被忽略");
                return;
            }
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
                        string text = selector(language);
                        if (!string.IsNullOrEmpty(text)) return text;
                        // 语言回退：目标语言缺失时回退中文，避免 UIText 显示空白
                        if (!string.IsNullOrEmpty(language.ZH_CN)) return language.ZH_CN;
                        return string.Empty;
                    }
                    // 默认返回中文
                    return language.ZH_CN;
                }
                else
                {
                    // 首次缺失才报错，后续同 ID 静默返回，防止运行时反复刷屏
                    if (missingKeysReported.Add(number))
                    {
                        Log.Error("未找到ID为{0}的语言配置", number);
                    }
                }
            }
            else
            {
                // 首次才报错：Init 失败（容器缺失）后 languagesDict 为 null，
                // 每次 GetTextById 都会走此分支，高频调用下会刷屏
                if (!containerEmptyReported)
                {
                    containerEmptyReported = true;
                    Log.Error("LanguagesContainer或configs为空");
                }
            }

            return string.Empty; // 如果未找到对应的文本，返回空字符串
        }

        /// <summary>
        /// 根据ID获取文本并格式化插值（如“已击杀 {0} 个敌人”）。
        /// 文本缺失时返回空字符串（格式化不生效）。
        /// </summary>
        public string GetTextById(int number, params object[] args)
        {
            string text = GetTextById(number);
            if (string.IsNullOrEmpty(text)) return string.Empty;
            try
            {
                return args != null && args.Length > 0 ? string.Format(text, args) : text;
            }
            catch (FormatException ex)
            {
                // 配表占位符与参数不匹配：返回原文并告警（避免异常传播到 UI 刷新链）
                Log.Warning("LanguagesSystem.GetTextById({0}) 格式化失败: {1}", number, ex.Message);
                return text;
            }
        }
    }
}