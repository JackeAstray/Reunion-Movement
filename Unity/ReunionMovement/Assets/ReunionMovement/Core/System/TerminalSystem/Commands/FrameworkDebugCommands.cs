#if UNITY_EDITOR || DEVELOPMENT_BUILD
using Cysharp.Threading.Tasks;
using R3;
using ReunionMovement.Common;
using ReunionMovement.Common.Util;
using ReunionMovement.Core.Resources;

namespace ReunionMovement.Core.Terminal
{
    /// <summary>
    /// 框架调试终端命令 —— 通过 [RegisterCommand] 源码生成器注册（仅编辑器/开发构建编译）。
    /// 覆盖本会话新增框架能力：存档（SaveSystem）/ 崩溃上报（ErrorReporter）/
    /// 性能监控（PerformanceMonitor）/ Addressables 热更（AddressableUpdateFlow）。
    /// 终端用法：按 ~ 打开终端后输入命令，如 "PerfStat" / "AddrUpdate"。
    /// </summary>
    public static class FrameworkDebugCommands
    {
        /// <summary>存档测试数据载体（SaveSystem JSON 序列化）</summary>
        [System.Serializable]
        private class CmdSaveData
        {
            public string value;
            public string version = SaveSystem.DefaultVersion;
        }

        /// <summary>测试存档写入：SaveTest &lt;name&gt; &lt;value&gt;</summary>
        [RegisterCommand(Help = "SaveTest 2 String（SaveSystem 写入测试存档）", MinArgCount = 2, MaxArgCount = 2)]
        internal static void SaveTest(CommandArg[] args)
        {
            SaveSystem.Save("cmd_" + args[0].String, new CmdSaveData { value = args[1].String });
            Log.Debug("[Cmd] SaveTest 已写入存档 cmd_{0} = {1}", args[0].String, args[1].String);
        }

        /// <summary>测试存档读取：LoadTest &lt;name&gt;</summary>
        [RegisterCommand(Help = "LoadTest 1 String（SaveSystem 读取测试存档）", MinArgCount = 1, MaxArgCount = 1)]
        internal static void LoadTest(CommandArg[] args)
        {
            if (SaveSystem.TryLoad("cmd_" + args[0].String, out CmdSaveData data))
            {
                Log.Debug("[Cmd] LoadTest cmd_{0} = {1}", args[0].String, data.value);
            }
            else
            {
                Log.Warning("[Cmd] LoadTest 未找到存档 cmd_{0}", args[0].String);
            }
        }

        /// <summary>打印性能监控统计：PerfStat</summary>
        [RegisterCommand(Help = "PerfStat（打印当前 FPS/历史最低 FPS/托管内存）")]
        internal static void PerfStat(CommandArg[] args)
        {
            var monitor = PerformanceMonitor.Instance;
            Log.Debug("[Cmd] PerfStat FPS={0:F1} 最低={1:F1} 托管内存={2}MB",
                monitor.CurrentFps, monitor.MinFpsRecord, monitor.CurrentMemoryMB);
        }

        /// <summary>上报本地错误日志：ReportErrors</summary>
        [RegisterCommand(Help = "ReportErrors（上传错误日志到 ErrorReporter.UploadUrl）")]
        internal static void ReportErrors(CommandArg[] args)
        {
            Log.Debug("[Cmd] ReportErrors 开始上传错误日志...");
            ErrorReporter.UploadErrorLog(ok => Log.Debug("[Cmd] ReportErrors 上传结果: {0}", ok));
        }

        /// <summary>运行 Addressables 热更检查与下载：AddrUpdate</summary>
        [RegisterCommand(Help = "AddrUpdate（检查并下载 Addressables 远程更新）")]
        internal static void AddrUpdate(CommandArg[] args)
        {
            Log.Debug("[Cmd] AddrUpdate 开始热更检查...");
            var flow = new AddressableUpdateFlow();
            flow.State.Subscribe(s => Log.Debug("[Cmd] AddrUpdate 状态: {0}", s));
            flow.RunAsync().Forget();
        }

        /// <summary>清理 Addressables 本地 Bundle 缓存：AddrCleanCache</summary>
        [RegisterCommand(Help = "AddrCleanCache（清理 Addressables 本地 Bundle 缓存）")]
        internal static void AddrCleanCache(CommandArg[] args)
        {
            Log.Debug("[Cmd] AddrCleanCache 开始清理...");
            AddressableSystem.Instance.CleanBundleCache().Forget();
        }
    }
}
#endif
