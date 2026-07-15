using ReunionMovement.Common.Util;
using R3;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace ReunionMovement.Example
{
    /// <summary>
    /// 观察者场景管理器 —— 使用 R3 ReactiveProperty 进行数值变化通知
    /// </summary>
    public class ObserverSceneMgr : MonoBehaviour
    {
        SubjectExample subject;

        public ObserverExample observer1;
        public ObserverExample observer2;

        public void Start()
        {
            subject = new SubjectExample();

            observer1.Init(subject);
            observer2.Init(subject);
        }

        public void OnChangeValue()
        {
            subject.ChangeValue(RandomUtil.RandomRange(101));
        }
    }
}
