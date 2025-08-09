//此脚本为工具生成，请勿手动创建 2025-08-09 17:27:36.665 <ExcelTo>
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting;

namespace ReunionMovement
{
    [Serializable]
    public class SoundConfig
    {
        
        public int Id;    //索引
        public int Number;    //编号
        public string Path;    //路径
        public string Name;    //名称
        public int Type;    //类型
        public string Detailed;    //介绍

        public override string ToString()
        {
            return string.Format(
                "[Id={1},Number={2},Path={3},Name={4},Type={5},Detailed={6}]",
                this.Id,
                this.Number,
                this.Path,
                this.Name,
                this.Type,
                this.Detailed
            );
        }

        /// <summary>
        /// 将实例转换为实体
        /// </summary>
        public SoundConfig ToEntity()
        {
            return new SoundConfig
            {
                Id       = this.Id,
                Number   = this.Number,
                Path     = this.Path,
                Name     = this.Name,
                Type     = this.Type,
                Detailed = this.Detailed,

            };
        }

        /// <summary>
        /// 解析实体为实例
        /// </summary>
        public static SoundConfig FromEntity(SoundConfig entity)
        {
            return new SoundConfig
            {
                Id       = entity.Id,
                Number   = entity.Number,
                Path     = entity.Path,
                Name     = entity.Name,
                Type     = entity.Type,
                Detailed = entity.Detailed,

            };
        }
    }
}
