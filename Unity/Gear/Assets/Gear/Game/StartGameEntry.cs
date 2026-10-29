using Gear.Framework.Runtime;
using Gear.Framework.Runtime.Base;

// 示例工程入口：只存在于源码仓库，不进 UPM 包 / .unitypackage。
// 接入自己的游戏时，在你自己的程序集里换一行即可：
//   [assembly: GearGameEntry(typeof(MyGame.MyGameEntry))]
[assembly: GearGameEntry(typeof(StartGame))]
