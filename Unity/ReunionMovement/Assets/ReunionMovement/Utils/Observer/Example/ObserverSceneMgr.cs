using ReunionMovement.Common.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace ReunionMovement.Example
{
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
