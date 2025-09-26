using TMPro;
using UnityEngine;

namespace ReunionMovement.Common.Util
{
    public class LoopItem : MonoBehaviour
    {
        public int index = -1;
        public TextMeshProUGUI itemName;

        public void Set(int index, string name)
        {
            this.index = index;
            itemName.text = name;
            gameObject.name = name;
        }
    }
}