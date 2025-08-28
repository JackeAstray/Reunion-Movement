using ReunionMovement.Common;
using ReunionMovement.UI.ButtonClick;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DoubleLongClickButton : MonoBehaviour
{
    public DoubleClickButton DoubleClickButton = null;
    public LongClickButton LongClickButton = null;

    public void Start()
    {
        DoubleClickButton.onDoubleClick.AddListener(() =>
        {
            Log.Debug("双击按钮");
        });
        LongClickButton.onLongClick.AddListener(() =>
        {
            Log.Debug("长按按钮");
        });
    }
}
