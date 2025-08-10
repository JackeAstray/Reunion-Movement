using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ReunionMovement.Common.Util
{
    /// <summary>
    /// 被观察者
    /// </summary>
    public abstract class SubjectBase
    {
        private List<ObserverBase> observers = new List<ObserverBase>();

        /// <summary>
        /// 设置状态并通知所有观察者
        /// </summary>
        /// <param name="args"></param>
        public void SetState(params object[] args)
        {
            NotifyAll(args);
        }

        /// <summary>
        /// 添加观察者
        /// </summary>
        /// <param name="observer"></param>
        public void Attach(ObserverBase observer)
        {
            if (!observers.Contains(observer))
            {
                observers.Add(observer);
                // 设置观察者的subject
                observer.subject = this;
            }
        }

        /// <summary>
        /// 通知所有观察者
        /// </summary>
        /// <param name="args"></param>
        public void NotifyAll(params object[] args)
        {
            foreach (ObserverBase observer in observers)
            {
                if (observer != null)
                {
                    observer.UpdateData(args);
                }
            }
        }

        /// <summary>
        /// 移除观察者
        /// </summary>
        /// <param name="observer"></param>
        public void Remove(ObserverBase observer)
        {
            if (observers.Contains(observer))
            {
                observers.Remove(observer);
            }
        }

        /// <summary>
        /// 清除所有观察者
        /// </summary>
        public void Clear()
        {
            observers.Clear();
        }
    }
}