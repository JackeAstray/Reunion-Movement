using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;

namespace ReunionMovement.Core.Terminal
{
    /// <summary>
    /// 终端项
    /// </summary>
    public class TerminalItem : MonoBehaviour
    {
        public TextMeshProUGUI text;

        public void SetText(string str)
        {
            text.text = str;
        }
    }
}
