//此脚本为工具生成，请勿手动创建 2025-08-10 15:57:36.534 <ExcelTo>
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
        public string ZH;    //中文
        public string EN;    //英文
        public string RU;    //俄文
        public string JP;    //日文

        public override string ToString()
        {
            return string.Format(
                "[Id={1},Number={2},ZH={3},EN={4},RU={5},JP={6}]",
                this.Id,
                this.Number,
                this.ZH,
                this.EN,
                this.RU,
                this.JP
            );
        }

        /// <summary>
        /// 将实例转换为实体
        /// </summary>
        public Languages ToEntity()
        {
            return new Languages
            {
                Id     = this.Id,
                Number = this.Number,
                ZH     = this.ZH,
                EN     = this.EN,
                RU     = this.RU,
                JP     = this.JP,

            };
        }

        /// <summary>
        /// 解析实体为实例
        /// </summary>
        public static Languages FromEntity(Languages entity)
        {
            return new Languages
            {
                Id     = entity.Id,
                Number = entity.Number,
                ZH     = entity.ZH,
                EN     = entity.EN,
                RU     = entity.RU,
                JP     = entity.JP,

            };
        }
    }
}
