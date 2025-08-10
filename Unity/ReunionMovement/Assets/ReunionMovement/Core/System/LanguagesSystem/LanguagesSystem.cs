using ReunionMovement.Common;
using ReunionMovement.Common.Util;
using ReunionMovement.Core.Base;
using ReunionMovement.Core.Resources;
using ReunionMovement.Core.Sound;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace ReunionMovement.Core.Languages
{
    public class LanguagesSystem : SubjectBase, ICustommSystem
    {
        #region 单例与初始化
        private static readonly Lazy<LanguagesSystem> instance = new(() => new LanguagesSystem());
        public static LanguagesSystem Instance => instance.Value;
        public bool IsInited { get; private set; }
        private double initProgress = 0;
        public double InitProgress { get { return initProgress; } }
        #endregion

        public Multilingual multilingual = Multilingual.ZH;
        private LanguagesContainer languagesContainer;

        public async Task Init()
        {
            initProgress = 0;

            // 从ScriptableObjects中获取文本
            languagesContainer = ResourcesSystem.Instance.Load<LanguagesContainer>("LanguagesContainer");
            if (languagesContainer == null || languagesContainer.configs == null)
            {
                Log.Error("LanguagesContainer或其configs为空, 语言系统初始化失败!");
            }

            initProgress = 100;
            IsInited = true;
            Log.Debug("LanguagesSystem 初始化完成");
        }

        public void Update(float logicTime, float realTime)
        {

        }

        public void Clear()
        {
            Log.Debug("LanguagesSystem 清除数据");
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
        /// 设置多语言
        /// </summary>
        /// <param name="multilingual"></param>
        public void SetMultilingual(Multilingual multilingual)
        {
            this.multilingual = multilingual;
            // 通知所有观察者
            SetState(); 
        }

        /// <summary>
        /// 根据ID获取对应的文本
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public string GetTextById(int id)
        {
            if (languagesContainer != null && languagesContainer.configs != null)
            {
                ReunionMovement.Languages language = languagesContainer.configs.Find(l => l.Id == id);
                if (language != null)
                {
                    // 根据当前多语言设置返回对应的文本
                    switch (multilingual)
                    {
                        case Multilingual.ZH:
                            return language.ZH;
                        case Multilingual.EN:
                            return language.EN;
                        case Multilingual.RU:
                            return language.RU;
                        case Multilingual.JP:
                            return language.JP;
                        default:
                            return language.ZH; // 默认返回中文
                    }
                }
                else
                {
                    Log.Error($"未找到ID为{id}的语言配置");
                }
            }
            else
            {
                Log.Error("LanguagesContainer或configs为空");
            }

            return string.Empty; // 如果未找到对应的文本，返回空字符串
        }

        /// <summary>
        /// 注册观察者
        /// </summary>
        /// <param name="observer"></param>
        public void RegisterObserver(ObserverBase observer)
        {
            base.Attach(observer);
        }

        /// <summary>
        /// 移除观察者
        /// </summary>
        /// <param name="observer"></param>
        public void RemoveObserver(ObserverBase observer)
        {
            base.Remove(observer);
        }

        /// <summary>
        /// 清除所有观察者
        /// </summary>
        public void ClearObservers()
        {
            base.Clear();
        }
    }
}