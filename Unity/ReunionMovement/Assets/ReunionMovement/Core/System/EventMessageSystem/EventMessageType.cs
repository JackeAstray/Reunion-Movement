using ReunionMovement.Core.Base;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReunionMovement.Core.EventMessage
{
    public enum EventMessageType
    {
        //开始游戏
        StartGame,
        //点击按钮
        ButtonClick,
        //点击方块
        ClickBlock,
        //退出游戏
        Quit,
        //发送消息
        SendMessage,
        //进入下一个场景
        GoToNextScene,
        // 提示
        Tip,
        // 警告提示
        WaringTip,
        // 错误提示
        ErrorTip,
        // 通知
        Notice,
    }
}
