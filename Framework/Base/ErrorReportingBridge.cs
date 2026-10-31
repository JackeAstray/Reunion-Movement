using System;

namespace Gear.Framework.Runtime.Base
{
    /// <summary>
    /// 全局错误捕获的运行时桥。Modules 的 ErrorReporter 在进入 Play 时注册初始化方法，
    /// Bootstrap 在启动早期调用 <see cref="TryInitialize"/>；没有注册者时静默跳过。
    /// </summary>
    public static class ErrorReportingBridge
    {
        /// <summary>初始化委托；由 ErrorReporter 注册。</summary>
        public static Action Initialize { get; set; }

        /// <summary>上传日志委托；由 ErrorReporter 注册。</summary>
        public static Action<Action<bool>> UploadLog { get; set; }

        /// <summary>尝试上传日志；未注册时返回 false。</summary>
        public static bool TryUploadLog(Action<bool> onComplete)
        {
            if (UploadLog == null) return false;
            UploadLog(onComplete);
            return true;
        }

        /// <summary>若已注册则初始化一次（幂等由实现方保证）。</summary>
        public static void TryInitialize()
        {
            Initialize?.Invoke();
        }

        /// <summary>清空注册（测试用）。</summary>
        public static void Reset()
        {
            Initialize = null;
            UploadLog = null;
        }
    }
}
