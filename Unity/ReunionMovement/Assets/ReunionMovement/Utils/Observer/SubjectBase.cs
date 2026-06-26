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
        /// 通知所有观察者（使用 ToArray 防御性拷贝，防止回调中修改 observers 导致 InvalidOperationException）
        /// </summary>
        /// <param name="args"></param>
        public void NotifyAll(params object[] args)
        {
            var snapshot = observers.ToArray();
            foreach (ObserverBase observer in snapshot)
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
            if (observer != null)
            {
                observer.subject = null;
            }
            observers.Remove(observer);
        }

        /// <summary>
        /// 清除所有观察者（virtual 允许派生类安全扩展清理逻辑，避免 new 关键字隐藏）
        /// </summary>
        public virtual void Clear()
        {
            observers.Clear();
        }
    }
}