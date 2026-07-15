using ReunionMovement.Common;
using R3;
using System;
using UnityEngine;

namespace ReunionMovement.Example
{
    /// <summary>
    /// 观察者示例 —— 使用 R3 Subscribe 替代 ObserverBase
    /// </summary>
    public class ObserverExample : MonoBehaviour
    {
        public int exampleValue;

        private IDisposable subscription;

        public void Init(SubjectExample subject)
        {
            // 使用 R3 订阅 ReactiveProperty 的变化
            subscription = subject.Value
                .Subscribe(value =>
                {
                    Log.Debug($"收到数值变化通知: {value}");
                    exampleValue = value;
                });
        }

        private void OnDestroy()
        {
            // 释放 R3 订阅，防止内存泄漏
            subscription?.Dispose();
            subscription = null;
        }
    }
}