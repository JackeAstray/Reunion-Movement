using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine.Networking;

namespace ReunionMovement.Common.Util.Download
{
    /// <summary>
    /// 下载执行器基类。
    /// [Preserve]：DownloadExecutorFactory 是<b>反射</b>枚举本程序集内本类型的子类来建立"类名 → 类型"表的，
    /// 而 IL2CPP 的托管代码裁剪看不到这种反射用法 ⇒ 若没有 link.xml / [Preserve]，执行器类型可能在
    /// 目标平台（尤其是本项目主目标的 WebGL）被裁掉，届时工厂只会记一条"未找到执行器"并返回 null，
    /// 表现为"平台特异的静默功能缺失"。此处以特性声明保留；若将来新增执行器且发现仍被裁剪，
    /// 请在具体执行器类型上再显式标注一次。
    /// </summary>
    [UnityEngine.Scripting.Preserve]
    public abstract class IDownloadExecutor
    {
        /// <summary>
        /// 初始块大小
        /// </summary>
        public int InitialChunkSize = 200000;

        /// <summary>
        /// 已完成分块下载
        /// </summary>
        public bool CompletedMultipartDownload = false;
        public bool DidHeadReq = false;

        /// <summary>
        /// 是否完成下载
        /// </summary>
        public bool completed => Progress == 1.0f;

        /// <summary>
        /// 下载进度
        /// </summary>
        public abstract float Progress { get; }

        /// <summary>
        /// 下载的uri
        /// </summary>
        public abstract string Uri { get; set; }

        /// <summary>
        /// 下载的文件大小（long 支持 >2GB 文件，避免 int 溢出为负）
        /// </summary>
        public abstract long BytesDownloaded { get; }

        /// <summary>
        /// 请求头
        /// </summary>
        public Dictionary<string, string> RequestHeaders { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// 下载的路径
        /// </summary>
        public abstract string DownloadPath { get; set; }

        public abstract bool DownloadToRoot { get; set; }

        public abstract bool IsMD5Name { get; set; }

        /// <summary>
        /// 下载的结果路径
        /// </summary>
        public string DownloadResultPath
        {
            get
            {
                if (string.IsNullOrEmpty(Uri) || string.IsNullOrEmpty(DownloadPath))
                {
                    return null;
                }

                string filename = IsMD5Name ? PathUtil.GetFileNameByUrl(Uri) : HTTPHelper.GetFilenameFromUriNaively(Uri);

                var relativePath = DownloadToRoot
                    ? filename
                    : HTTPHelper.GetRelativePathFromUri(Uri);

                return Path.Combine(DownloadPath, relativePath)
                    .Replace("/", Path.DirectorySeparatorChar.ToString());
            }
        }

        /// <summary>
        /// 分块下载
        /// </summary>
        public abstract bool MultipartDownload { get; set; }

        /// <summary>
        /// 是否支持分块下载
        /// </summary>
        public bool TryMultipartDownload { get; set; } = true;

        /// <summary>
        /// 是否暂停下载
        /// </summary>
        public abstract bool Paused { get; }

        /// <summary>
        /// 暂停下载：中止在途请求但保留已完成部分（供 Resume 断点续传），不触发错误/取消事件。
        /// 返回是否成功进入暂停。
        /// </summary>
        public abstract bool Pause();

        /// <summary>
        /// 恢复下载：退出暂停状态；具体重发由下载管理器重新调用 Download() 并接线完成回调。
        /// 返回是否需要重新发起下载。
        /// </summary>
        public abstract bool Resume();

        /// <summary>
        /// 是否在下载失败时放弃
        /// </summary>
        public abstract bool AbandonOnFailure { get; set; }

        /// <summary>
        /// 下载的错误
        /// </summary>
        public abstract bool DidError { get; set; }

        /// <summary>
        /// 下载的开始时间
        /// </summary>
        public abstract int StartTime { get; }

        /// <summary>
        /// 是否正在下载
        /// </summary>
        public bool Downloading => StartTime != 0;

        /// <summary>
        /// 是否已被取消（幂等取消标志，供下载管理器判定在途任务是否已中止）
        /// </summary>
        public virtual bool IsCanceled => false;

        /// <summary>
        /// 下载的结束时间
        /// </summary>
        public abstract int EndTime { get; }

        /// <summary>
        /// 下载的超时时间
        /// </summary>
        public abstract int Timeout { get; set; }

        /// <summary>
        /// 下载的时间（毫秒）。
        /// 注意：当前实现读取的是 32 位的 <c>Environment.TickCount</c>（约 24.9 天回绕），且 <see cref="StartTime"/>/<see cref="EndTime"/>
        /// 为 <c>int</c>，因此设备长时间不重启后该值会被下面的"负值夹取为 0"保护成 0（表现为计时/速率读数归零，而非崩溃）。
        /// 若要真正消除回绕，需要把这两个属性改为 <c>long</c> 并改用 <c>Environment.TickCount64</c>（会波及全部执行器实现）。
        /// </summary>
        public long ElapsedTime
        {
            get
            {
                if (StartTime == 0)
                {
                    return 0;
                }

                if (EndTime == 0)
                {
                    // 无符号差值：32 位 Environment.TickCount 约 24.9 天回绕一次，回绕后
                    // "TickCount - StartTime" 会变成负数，旧写法把它夹成 0 ⇒ 计时/速率读数归零。
                    // 按 uint 解释即可得到真实的毫秒差（单次下载远短于 24.9 天，前提恒成立）。
                    // 注意：StartTime 由本类内部在开始时写入，不存在"未来值"这一输入，
                    // 故此处无需再保留"负值夹 0"的保护。
                    long diff = unchecked((uint)(Environment.TickCount - StartTime));
                    return diff;
                }

                long endDiff = unchecked((uint)(EndTime - StartTime));
                return endDiff;
            }
        }

        /// <summary>
        /// 下载的字节数
        /// </summary>
        public float MegabytesDownloadedPerSecond
        {
            get
            {
                var seconds = ElapsedTime / 1000f;
                return seconds > 0 ? (BytesDownloaded / 1000f) / seconds : 0f;
            }
        }

        /// <summary>
        /// 取消下载
        /// </summary>
        /// <returns></returns>
        public abstract bool Cancel();

        /// <summary>
        /// 根据现有的“URI”属性启动下载
        /// </summary>
        /// <returns></returns>
        public abstract UnityWebRequestAsyncOperation Download();
    }
}
