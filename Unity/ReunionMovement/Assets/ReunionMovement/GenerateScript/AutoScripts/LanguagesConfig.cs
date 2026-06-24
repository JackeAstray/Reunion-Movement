//此脚本为工具生成，请勿手动创建 2026-06-21 17:04:11.180 <ExcelTo>
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting;

namespace ReunionMovement
{
    [Serializable]
    public class LanguagesConfig
    {
        
        public int Id;    //索引
        public int Number;    //编号
        public string ZH_CN;    //中文
        public string EN_US;    //英文
        public string RU_RU;    //俄文
        public string JA_JP;    //日文

        public override string ToString()
        {
            return string.Format(
                "[Id={0},Number={1},ZH_CN={2},EN_US={3},RU_RU={4},JA_JP={5}]",
                this.Id,
                this.Number,
                this.ZH_CN,
                this.EN_US,
                this.RU_RU,
                this.JA_JP
            );
        }

        /// <summary>
        /// 将实例转换为实体
        /// </summary>
        public LanguagesConfig ToEntity()
        {
            return new LanguagesConfig
            {
                Id     = this.Id,
                Number = this.Number,
                ZH_CN  = this.ZH_CN,
                EN_US  = this.EN_US,
                RU_RU  = this.RU_RU,
                JA_JP  = this.JA_JP,

            };
        }

        /// <summary>
        /// 解析实体为实例
        /// </summary>
        public static LanguagesConfig FromEntity(LanguagesConfig entity)
        {
            return new LanguagesConfig
            {
                Id     = entity.Id,
                Number = entity.Number,
                ZH_CN  = entity.ZH_CN,
                EN_US  = entity.EN_US,
                RU_RU  = entity.RU_RU,
                JA_JP  = entity.JA_JP,

            };
        }
    }
}
