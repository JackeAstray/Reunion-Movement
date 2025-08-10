using Newtonsoft.Json.Bson;
using ReunionMovement.Common;
using ReunionMovement.Common.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace ReunionMovement.Core.Languages
{
    public class UIText : ObserverBase
    {
        // 语言文本ID
        [SerializeField] private int id;

        private TMP_Text tmpTextComponent;
        private Text textComponent;

        void Start()
        {
            tmpTextComponent = GetComponent<TMP_Text>();
            textComponent = GetComponent<Text>();
            if (tmpTextComponent == null && textComponent == null)
            {
                Debug.LogError("UIText组件需要绑定TMP_Text或Text组件");
                return;
            }
            LanguagesSystem.Instance.RegisterObserver(this);
            UpdateData();
        }

        private void OnDestroy()
        {
            if (LanguagesSystem.Instance != null)
            {
                LanguagesSystem.Instance.RemoveObserver(this);
            }
        }

        /// <summary>
        /// 更新数据
        /// </summary>
        /// <param name="args"></param>
        public override void UpdateData(params object[] args)
        {
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

            string value = LanguagesSystem.Instance.GetTextById(id);

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
                Log.Debug("GetTextLanguage() " + id + "是空的");
            }
        }
    }
}
