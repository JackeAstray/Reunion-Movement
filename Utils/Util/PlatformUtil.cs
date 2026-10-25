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

        /// <summary>指定平台是否为 WebGL（浏览器）。</summary>
        public static bool IsWebGLPlatformOn(RuntimePlatform platform)
        {
            return platform == RuntimePlatform.WebGLPlayer;
        }

        /// <summary>当前平台是否为 WebGL（浏览器）。</summary>
        public static bool IsWebGLPlatform => IsWebGLPlatformOn(Application.platform);

        /// <summary>
        /// 指定平台的“显示配置”是否由平台/宿主预设决定 —— 此时框架不得用存档里的分辨率与质量档覆盖它。
        ///
        /// 移动端：设备分辨率由系统固定、<see cref="Screen.resolutions"/> 为空；质量档由项目的
        /// m_PerPlatformDefaultQuality 指定（Android/iPhone 为 Mobile 档）。
        /// WebGL：canvas 尺寸由宿主页面控制，Unity 文档明确 <see cref="Screen.SetResolution"/> 在 WebGL 无效；
        /// 质量档同样由 m_PerPlatformDefaultQuality 指定（WebGL 为 Mobile 档）。
        ///
        /// 两类平台都返回 true：用存档里的桌面默认值（1920x1080、以及被当裸索引 clamp 后恒为 PC 的
        /// graphicsQuality）去覆盖它们，只会得到拉伸画面，或与项目 per-platform 配置相反的画质。
        /// </summary>
        public static bool IsDisplayPresetByPlatformOn(RuntimePlatform platform)
        {
            return IsMobilePlatformOn(platform) || IsWebGLPlatformOn(platform);
        }

        /// <summary>
        /// 当前平台的显示配置是否由平台/宿主预设决定（见 <see cref="IsDisplayPresetByPlatformOn"/>）。
        /// </summary>
        public static bool IsDisplayPresetByPlatform => IsMobilePlatform || IsWebGLPlatform;

        /// <summary>
        /// 指定平台的显示分辨率是否可由 <see cref="Screen.SetResolution"/> 控制（= 非移动端且非 WebGL）。
        ///
        /// 移动端返回 false：设备显示分辨率由系统固定、<see cref="Screen.resolutions"/> 为空，
        /// <see cref="Screen.SetResolution"/> 不会切换显示模式，只会把 Unity 的内部渲染目标改成传入尺寸
        /// 再拉伸铺满整屏 —— 与设备宽高比不一致时画面被拉伸/压扁（AR 应用会直接破坏相机画面与识别坐标），
        /// 同时 Screen.width/height 会变成这个假分辨率，破坏依赖它做方向/布局判断的逻辑。
        /// WebGL 同样返回 false：canvas 尺寸由宿主页面/浏览器决定，该 API 在 WebGL 无效
        /// （调用它只会谎报成功）。
        /// </summary>
        public static bool IsResolutionControllableOn(RuntimePlatform platform)
        {
            return !IsDisplayPresetByPlatformOn(platform);
        }

        /// <summary>
        /// 当前平台的分辨率是否可由 <see cref="Screen.SetResolution"/> 控制。
        /// 移动端与 WebGL 必须交给系统/宿主页面决定分辨率（见 <see cref="IsResolutionControllableOn"/>）。
        /// </summary>
        public static bool IsResolutionControllable => IsResolutionControllableOn(Application.platform);
    }
}
