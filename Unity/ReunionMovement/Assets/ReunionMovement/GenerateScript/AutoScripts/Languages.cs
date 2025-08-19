//此脚本为工具生成，请勿手动创建 2025-08-10 22:51:12.720 <ExcelTo>
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting;

namespace ReunionMovement
{
    [Serializable]
    public class Languages
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
                "[Id={1},Number={2},ZH_CN={3},EN_US={4},RU_RU={5},JA_JP={6}]",
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
        public Languages ToEntity()
        {
            return new Languages
            {
                Id = this.Id,
                Number = this.Number,
                ZH_CN = this.ZH_CN,
                EN_US = this.EN_US,
                RU_RU = this.RU_RU,
                JA_JP = this.JA_JP,

            };
        }

        /// <summary>
        /// 解析实体为实例
        /// </summary>
        public static Languages FromEntity(Languages entity)
        {
            return new Languages
            {
                Id = entity.Id,
                Number = entity.Number,
                ZH_CN = entity.ZH_CN,
                EN_US = entity.EN_US,
                RU_RU = entity.RU_RU,
                JA_JP = entity.JA_JP,

            };
        }
    }
}
