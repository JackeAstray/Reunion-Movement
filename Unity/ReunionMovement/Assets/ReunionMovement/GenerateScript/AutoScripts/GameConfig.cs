//此脚本为工具生成，请勿手动创建 2025-08-09 17:27:36.639 <ExcelTo>
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting;

namespace ReunionMovement
{
    [Serializable]
    public class GameConfig
    {
        
        public int Id;    //索引
        public int Number;    //编号
        public int LanguageID;    //语言ID
        public string Title;    //文本介绍
        public object Value;    //值

        public override string ToString()
        {
            return string.Format(
                "[Id={1},Number={2},LanguageID={3},Title={4},Value={5}]",
                this.Id,
                this.Number,
                this.LanguageID,
                this.Title,
                this.Value
            );
        }

        /// <summary>
        /// 将实例转换为实体
        /// </summary>
        public GameConfig ToEntity()
        {
            return new GameConfig
            {
                Id         = this.Id,
                Number     = this.Number,
                LanguageID = this.LanguageID,
                Title      = this.Title,
                Value      = this.Value,

            };
        }

        /// <summary>
        /// 解析实体为实例
        /// </summary>
        public static GameConfig FromEntity(GameConfig entity)
        {
            return new GameConfig
            {
                Id         = entity.Id,
                Number     = entity.Number,
                LanguageID = entity.LanguageID,
                Title      = entity.Title,
                Value      = entity.Value,

            };
        }
    }
}
