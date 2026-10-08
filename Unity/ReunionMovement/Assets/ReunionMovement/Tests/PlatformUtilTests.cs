using NUnit.Framework;
using ReunionMovement.Common.Util;
using UnityEngine;

namespace ReunionMovement.Tests
{
    /// <summary>
    /// 平台能力判定 EditMode 测试。
    ///
    /// 回归保护：移动端（Android/iOS）必须判定为“移动端”且“分辨率不可控”。
    /// 否则 GameOption.ApplyDisplayOptions / ResolutionMgr 会照桌面存档调用
    /// Screen.SetResolution(1920, 1080, ...)，把 Unity 内部渲染目标改成 1920x1080
    /// 再拉伸铺满整屏（如 1116x2480），表现为画面拉伸/压扁，且 Screen.width/height
    /// 变成假分辨率，破坏依赖它做方向/布局判断的逻辑。
    /// </summary>
    public class PlatformUtilTests
    {
        [Test]
        public void Mobile_Platforms_Are_Mobile_And_Not_Resolution_Controllable()
        {
            Assert.IsTrue(PlatformUtil.IsMobilePlatformOn(RuntimePlatform.Android), "Android 应判定为移动端");
            Assert.IsTrue(PlatformUtil.IsMobilePlatformOn(RuntimePlatform.IPhonePlayer), "iOS 应判定为移动端");
            Assert.IsFalse(PlatformUtil.IsResolutionControllableOn(RuntimePlatform.Android), "Android 分辨率由系统控制");
            Assert.IsFalse(PlatformUtil.IsResolutionControllableOn(RuntimePlatform.IPhonePlayer), "iOS 分辨率由系统控制");
        }

        [Test]
        public void WebGL_Is_Not_Resolution_Controllable()
        {
            Assert.IsTrue(PlatformUtil.IsWebGLPlatformOn(RuntimePlatform.WebGLPlayer), "WebGL 应判定为 WebGL");
            Assert.IsFalse(PlatformUtil.IsMobilePlatformOn(RuntimePlatform.WebGLPlayer), "WebGL 不是移动端");
            // Unity 文档：Screen.SetResolution 在 WebGL 无效（canvas 尺寸由宿主页面控制），
            // 返回 true 会谎报成功并让 ResolutionMgr/SetScreen 静默失效
            Assert.IsFalse(PlatformUtil.IsResolutionControllableOn(RuntimePlatform.WebGLPlayer), "WebGL 分辨率由宿主页面控制");
            Assert.IsTrue(PlatformUtil.IsDisplayPresetByPlatformOn(RuntimePlatform.WebGLPlayer), "WebGL 的显示配置由平台预设决定");
        }

        [Test]
        public void Desktop_And_Editor_Platforms_Are_Resolution_Controllable()
        {
            Assert.IsFalse(PlatformUtil.IsMobilePlatformOn(RuntimePlatform.WindowsPlayer));
            Assert.IsFalse(PlatformUtil.IsMobilePlatformOn(RuntimePlatform.WindowsEditor));
            Assert.IsFalse(PlatformUtil.IsDisplayPresetByPlatformOn(RuntimePlatform.WindowsPlayer));
            Assert.IsFalse(PlatformUtil.IsDisplayPresetByPlatformOn(RuntimePlatform.WindowsEditor));
            Assert.IsFalse(PlatformUtil.IsDisplayPresetByPlatformOn(RuntimePlatform.OSXPlayer));
            Assert.IsFalse(PlatformUtil.IsDisplayPresetByPlatformOn(RuntimePlatform.LinuxPlayer));
            Assert.IsTrue(PlatformUtil.IsResolutionControllableOn(RuntimePlatform.WindowsPlayer));
            Assert.IsTrue(PlatformUtil.IsResolutionControllableOn(RuntimePlatform.WindowsEditor));
            Assert.IsTrue(PlatformUtil.IsResolutionControllableOn(RuntimePlatform.OSXPlayer));
            Assert.IsTrue(PlatformUtil.IsResolutionControllableOn(RuntimePlatform.LinuxPlayer));
        }

        [Test]
        public void Current_Platform_Matches_Explicit_Query()
        {
            Assert.AreEqual(
                PlatformUtil.IsMobilePlatformOn(Application.platform),
                PlatformUtil.IsMobilePlatform);
            Assert.AreEqual(
                PlatformUtil.IsWebGLPlatformOn(Application.platform),
                PlatformUtil.IsWebGLPlatform);
            Assert.AreEqual(
                PlatformUtil.IsDisplayPresetByPlatformOn(Application.platform),
                PlatformUtil.IsDisplayPresetByPlatform);
            Assert.AreEqual(
                PlatformUtil.IsResolutionControllableOn(Application.platform),
                PlatformUtil.IsResolutionControllable);
        }
    }
}
