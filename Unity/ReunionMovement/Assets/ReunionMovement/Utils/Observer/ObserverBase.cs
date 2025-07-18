using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ReunionMovement.Common.Util
{
    /// <summary>
    /// 观察者
    /// </summary>
    public abstract class ObserverBase : MonoBehaviour
    {
        protected SubjectBase subject;

        /// <summary>
        /// 更新数据
        /// </summary>
        /// <param name="args"></param>
        public abstract void UpdateData(params object[] args);
    }
}