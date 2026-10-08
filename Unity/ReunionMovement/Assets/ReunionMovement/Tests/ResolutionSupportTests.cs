using NUnit.Framework;
using ReunionMovement.Common.Util;
using UnityEngine;

namespace ReunionMovement.Tests
{
    /// <summary>
    /// 分辨率可控性判定 EditMode 测试。
    ///
    /// 回归保护：移动端（Android/iOS）必须判定为“分辨率不可控”。
    /// 否则 GameOption.ApplyDisplayOptions / ResolutionMgr 会照桌面存档调用
    /// Screen.SetResolution(1920, 1080, ...)，把 Unity 内部渲染目标改成 1920x1080
    /// 再拉伸铺满整屏（如 1116x2480），表现为画面拉伸/压扁，且 Screen.width/height
    /// 变成假分辨率，破坏依赖它做方向/布局判断的逻辑。
    /// </summary>
    public class ResolutionSupportTests
    {
        [Test]
        public void Mobile_Platforms_Are_Not_Controllable()
        {
            Assert.IsFalse(ResolutionMgr.IsResolutionControllableOn(RuntimePlatform.Android), "Android 分辨率由系统控制");
            Assert.IsFalse(ResolutionMgr.IsResolutionControllableOn(RuntimePlatform.IPhonePlayer), "iOS 分辨率由系统控制");
        }

        [Test]
        public void Desktop_And_Editor_Platforms_Are_Controllable()
        {
            Assert.IsTrue(ResolutionMgr.IsResolutionControllableOn(RuntimePlatform.WindowsPlayer));
            Assert.IsTrue(ResolutionMgr.IsResolutionControllableOn(RuntimePlatform.WindowsEditor));
            Assert.IsTrue(ResolutionMgr.IsResolutionControllableOn(RuntimePlatform.OSXPlayer));
            Assert.IsTrue(ResolutionMgr.IsResolutionControllableOn(RuntimePlatform.LinuxPlayer));
        }

        [Test]
        public void Current_Platform_Matches_Explicit_Query()
        {
            Assert.AreEqual(
                ResolutionMgr.IsResolutionControllableOn(Application.platform),
                ResolutionMgr.IsResolutionControllable);
        }
    }
}
