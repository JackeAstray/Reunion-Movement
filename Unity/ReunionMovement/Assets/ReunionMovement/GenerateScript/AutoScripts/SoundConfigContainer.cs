//此脚本为工具生成，请勿手动创建 2025-08-10 22:51:12.754 <ExcelTo>
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting;

namespace ReunionMovement
{
    [CreateAssetMenu(fileName = "SoundConfigContainer", menuName = "ScriptableObjects/SoundConfigContainer", order = 0)]
    public class SoundConfigContainer : ScriptableObject
    {
        public List<SoundConfig> configs;
    }
}
