using UnityEngine;

namespace ReunionMovement.Common.Util
{
    /// <summary>
    /// 平台能力判定（纯函数，便于测试与按目标平台预判）。
    /// 集中一处，避免各模块各自硬编码 Application.platform 判断而出现分歧。
    /// </summary>
    public static class PlatformUtil
    {
        /// <summary>指定平台是否为移动端（Android / iOS）。</summary>
        public static bool IsMobilePlatformOn(RuntimePlatform platform)
        {
            return platform == RuntimePlatform.Android
                || platform == RuntimePlatform.IPhonePlayer;
        }

        /// <summary>当前平台是否为移动端（Android / iOS）。</summary>
        public static bool IsMobilePlatform => IsMobilePlatformOn(Application.platform);

        /// <summary>
        /// 指定平台的显示分辨率是否可由 <see cref="Screen.SetResolution"/> 控制（= 非移动端）。
        ///
        /// 移动端返回 false：设备显示分辨率由系统固定、<see cref="Screen.resolutions"/> 为空，
        /// <see cref="Screen.SetResolution"/> 不会切换显示模式，只会把 Unity 的内部渲染目标改成传入尺寸
        /// 再拉伸铺满整屏 —— 与设备宽高比不一致时画面被拉伸/压扁（AR 应用会直接破坏相机画面与识别坐标），
        /// 同时 Screen.width/height 会变成这个假分辨率，破坏依赖它做方向/布局判断的逻辑。
        /// </summary>
        public static bool IsResolutionControllableOn(RuntimePlatform platform)
        {
            return !IsMobilePlatformOn(platform);
        }

        /// <summary>
        /// 当前平台的分辨率是否可由 <see cref="Screen.SetResolution"/> 控制。
        /// 移动端必须交给系统决定分辨率（见 <see cref="IsResolutionControllableOn"/>）。
        /// </summary>
        public static bool IsResolutionControllable => IsResolutionControllableOn(Application.platform);
    }
}
