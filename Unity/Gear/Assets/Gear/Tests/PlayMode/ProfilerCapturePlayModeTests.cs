using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Cysharp.Threading.Tasks;
using Gear.Framework.Runtime;
using Gear.Framework.Runtime.Base;
using NUnit.Framework;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.SceneManagement;

namespace Gear.Tests.PlayMode
{
    /// <summary>
    /// 把 <see cref="EngineProfiler"/> 埋的 marker 采成可读数据 —— 给"优化到底该改哪"提供依据，
    /// 而不是靠猜。
    ///
    /// <para>
    /// 用法（batchmode，无需打开编辑器）：
    /// <c>Tools/profile-capture.ps1 -UnityPath &lt;Unity.exe&gt;</c>
    /// 结果写到 <c>Logs/profiler/module-costs.json</c>，同时把摘要打进 Unity 日志。
    /// </para>
    ///
    /// <para>
    /// 为什么不复用 EditMode：EditMode 没有真正的逐帧驱动，<c>OnUpdate</c> 只能手动调，
    /// 测不到 PlayerLoop 里的真实开销（含 R3 广播、周期事件、物理步进）。
    /// </para>
    /// </summary>
    [Category("ProfilerCapture")]
    public class ProfilerCapturePlayModeTests
    {
        private const int WarmupFrames = 60;
        private const int SampleFrames = 300;

        private sealed class CaptureEntry : IGameEntry
        {
            public UniTask OnBeforeInitAsync() => UniTask.CompletedTask;
            public UniTask OnGameStartAsync() => UniTask.CompletedTask;
        }

        /// <summary>单个 marker 的采样器。先试 Scripts 分类，再退 Internal（自定义 marker 的分类在不同版本上不一致）。</summary>
        private sealed class Sampler
        {
            private readonly string name;
            private ProfilerRecorder recorder;

            public string Category = "";
            public long TotalTicks;
            public long MaxTicks;
            public int Samples;

            public Sampler(string name) => this.name = name;

            public bool TryStart()
            {
                foreach (ProfilerCategory category in new[] { ProfilerCategory.Scripts, ProfilerCategory.Internal })
                {
                    ProfilerRecorder candidate = ProfilerRecorder.StartNew(category, name, 1);
                    if (candidate.Valid)
                    {
                        recorder = candidate;
                        Category = category.Name;
                        return true;
                    }
                    candidate.Dispose();
                }
                return false;
            }

            public void Sample()
            {
                if (!recorder.Valid) return;
                long value = recorder.LastValue;
                if (value <= 0) return;
                TotalTicks += value;
                if (value > MaxTicks) MaxTicks = value;
                Samples++;
            }

            public double AvgMs => Samples == 0 ? 0 : TotalTicks / (double)Samples / 1_000_000.0;

            public double MaxMs => MaxTicks / 1_000_000.0;

            public string Name => name;

            public void Dispose()
            {
                if (recorder.Valid) recorder.Dispose();
            }
        }

        /// <summary>内置统计计数器（GC Alloc / Draw Calls 等）。单位由构造参数给定。</summary>
        private sealed class Counter
        {
            private readonly string name;
            private readonly string unit;
            private readonly (ProfilerCategory category, string statName)[] candidates;
            private ProfilerRecorder recorder;

            public long Total;
            public long Max;
            public int Samples;

            public Counter(string name, string unit, params (ProfilerCategory, string)[] candidates)
            {
                this.name = name;
                this.unit = unit;
                this.candidates = candidates;
            }

            public bool TryStart()
            {
                // 内置统计的名字在各版本间有出入（"GC Alloc" / "GC.Alloc" 等），逐个试，谁有效用谁
                for (int i = 0; i < candidates.Length; i++)
                {
                    ProfilerRecorder candidate = ProfilerRecorder.StartNew(candidates[i].category, candidates[i].statName, 1);
                    if (candidate.Valid)
                    {
                        recorder = candidate;
                        return true;
                    }
                    candidate.Dispose();
                }
                return false;
            }

            public void Sample()
            {
                if (!recorder.Valid) return;
                long value = recorder.LastValue;
                if (value < 0) return;
                Total += value;
                if (value > Max) Max = value;
                Samples++;
            }

            public string Name => name;
            public string Unit => unit;

            public double Avg => Samples == 0 ? 0 : Total / (double)Samples;

            public void Dispose()
            {
                if (recorder.Valid) recorder.Dispose();
            }
        }

        [Test]
        public async System.Threading.Tasks.Task Capture_Module_Update_Costs()
        {
            bool profilerWasEnabled = Profiler.enabled;
            Profiler.enabled = true;

            var samplers = new List<Sampler>();
            // 内置计数器：marker 只能解释"框架自己花了多少"，解释不了"这一帧分配了多少、画了多少。
            // GC Alloc 是最关键的一条——每帧分配为 0 才配叫"零 GC"。
            var counters = new List<Counter>
            {
                new Counter("GC Alloc", "Bytes",
                    (ProfilerCategory.Internal, "GC Alloc"),
                    (ProfilerCategory.Internal, "GC.Alloc"),
                    (ProfilerCategory.Memory, "GC Alloc"),
                    (ProfilerCategory.Memory, "GC.Alloc")),
                new Counter("Draw Calls", "Count",
                    (ProfilerCategory.Render, "Draw Calls"),
                    (ProfilerCategory.Render, "Draw Calls Count")),
            };
            GameEngine engine = null;
            try
            {
                IList<ICustomSystem> modules = ModulePresets.Full();
                InitResult result = await Bootstrap.StartAsync(new CaptureEntry(), modules);
                Assert.IsTrue(result.IsSuccess, "引擎启动失败：" + result.ErrorMessage);
                engine = GameEngine.Current;

                for (int i = 0; i < modules.Count; i++)
                {
                    samplers.Add(new Sampler("Gear.Module." + modules[i].GetType().Name));
                }
                samplers.Add(new Sampler("Gear.GameEngine.Update"));
                samplers.Add(new Sampler("Gear.GameEngine.Subject"));
                samplers.Add(new Sampler("Gear.GameEngine.FixedUpdate"));
                samplers.Add(new Sampler("Gear.GameEngine.LateUpdate"));

                // 场景内容（可选）：把示例场景叠加进来，让每个模块真的干活，
                // 而不是只测"引擎空转"。请求由脚本落在 Logs/profiler/capture-request.txt，
                // 不用环境变量——批处理链路上环境变量不保证被继承。
                string sceneName = ReadRequestedScene();
                if (!string.IsNullOrEmpty(sceneName))
                {
                    SceneManager.LoadScene(sceneName, LoadSceneMode.Additive);
                    await UniTask.Yield(); // 让场景加载启动
                    await UniTask.Delay(500); // 给 Awake/Start/UI 构建留出时间
                }

                // 预热：让索引、池、材质/JIT 的一次性开销落到采样窗口之外
                for (int i = 0; i < WarmupFrames; i++) await UniTask.Yield();

                // 重要：recorder 必须在预热之后才创建。marker 是首次使用时才注册的，
                // 启动前创建 recorder 会拿到 Valid=false，之后也不会自动复活。
                int started = 0;
                for (int i = 0; i < samplers.Count; i++)
                {
                    if (samplers[i].TryStart()) started++;
                }
                for (int i = 0; i < counters.Count; i++) counters[i].TryStart();

                var frameMs = new double[SampleFrames];
                float lastFrameTime = Time.unscaledDeltaTime;
                for (int i = 0; i < SampleFrames; i++)
                {
                    await UniTask.Yield();
                    frameMs[i] = (Time.unscaledDeltaTime + lastFrameTime) * 500.0; // 单位换算成 ms
                    lastFrameTime = Time.unscaledDeltaTime;
                    for (int k = 0; k < samplers.Count; k++) samplers[k].Sample();
                    for (int k = 0; k < counters.Count; k++) counters[k].Sample();
                }

                double frameTotal = 0, frameMax = 0;
                for (int i = 0; i < frameMs.Length; i++)
                {
                    frameTotal += frameMs[i];
                    if (frameMs[i] > frameMax) frameMax = frameMs[i];
                }
                double frameAvg = frameTotal / frameMs.Length;

                string path = WriteReport(samplers, counters, started, frameAvg, frameMax, sceneName);
                LogSummary(samplers, counters, frameAvg, frameMax, path);
            }
            finally
            {
                for (int i = 0; i < samplers.Count; i++) samplers[i].Dispose();
                for (int i = 0; i < counters.Count; i++) counters[i].Dispose();

                engine?.Dispose();
                GameObject driver = GameObject.Find("[GameEngineDriver]");
                if (driver != null) UnityEngine.Object.Destroy(driver);

                Profiler.enabled = profilerWasEnabled;
            }
        }

        private static string ReportPath()
        {
            // Assets/../Logs/profiler —— 与 unity-verify.ps1 的日志目录同级，且已被 .gitignore 覆盖
            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "../Logs/profiler"));
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, "module-costs.json");
        }

        /// <summary>
        /// 读取脚本落下的场景请求（Logs/profiler/capture-request.txt），空文件表示不加载场景。
        /// 用文件而不是环境变量：PowerShell → cmd → Unity 这条链路上环境变量不一定被继承。
        /// </summary>
        private static string ReadRequestedScene()
        {
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "../Logs/profiler/capture-request.txt"));
            if (!File.Exists(path)) return null;
            string requested = File.ReadAllText(path).Trim();
            return requested.Length == 0 ? null : requested;
        }

        private static string WriteReport(List<Sampler> samplers, List<Counter> counters, int started, double frameAvg, double frameMax, string sceneName)
        {
            samplers.Sort((a, b) => b.AvgMs.CompareTo(a.AvgMs));

            var sb = new StringBuilder();
            sb.Append("{\n");
            sb.Append("  \"unityVersion\": \"").Append(Application.unityVersion).Append("\",\n");
            sb.Append("  \"batchMode\": ").Append(Application.isBatchMode ? "true" : "false").Append(",\n");
            sb.Append("  \"graphicsDevice\": \"").Append(SystemInfo.graphicsDeviceType).Append("\",\n");
            sb.Append("  \"frames\": ").Append(SampleFrames).Append(",\n");
            sb.Append("  \"scene\": \"").Append(string.IsNullOrEmpty(sceneName) ? "(engine only)" : sceneName).Append("\",\n");
            sb.Append("  \"frameAvgMs\": ").Append(frameAvg.ToString("F4")).Append(",\n");
            sb.Append("  \"frameMaxMs\": ").Append(frameMax.ToString("F4")).Append(",\n");
            sb.Append("  \"recordersStarted\": ").Append(started).Append(",\n");
            sb.Append("  \"samplers\": [\n");

            for (int i = 0; i < samplers.Count; i++)
            {
                Sampler s = samplers[i];
                sb.Append("    { \"name\": \"").Append(s.Name).Append("\", ")
                  .Append("\"category\": \"").Append(s.Category).Append("\", ")
                  .Append("\"samples\": ").Append(s.Samples).Append(", ")
                  .Append("\"avgMs\": ").Append(s.AvgMs.ToString("F5")).Append(", ")
                  .Append("\"maxMs\": ").Append(s.MaxMs.ToString("F5")).Append(" }");
                if (i < samplers.Count - 1) sb.Append(',');
                sb.Append('\n');
            }

            sb.Append("  ],\n");
            sb.Append("  \"counters\": [\n");

            for (int i = 0; i < counters.Count; i++)
            {
                Counter c = counters[i];
                sb.Append("    { \"name\": \"").Append(c.Name).Append("\", ")
                  .Append("\"unit\": \"").Append(c.Unit).Append("\", ")
                  .Append("\"samples\": ").Append(c.Samples).Append(", ")
                  .Append("\"avg\": ").Append(c.Avg.ToString("F2")).Append(", ")
                  .Append("\"max\": ").Append(c.Max).Append(" }");
                if (i < counters.Count - 1) sb.Append(',');
                sb.Append('\n');
            }

            sb.Append("  ]\n}\n");

            string path = ReportPath();
            File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
            return path;
        }

        private static void LogSummary(List<Sampler> samplers, List<Counter> counters, double frameAvg, double frameMax, string path)
        {
            var report = new StringBuilder();
            report.Append("[ProfilerCapture] 报告: ").Append(path).Append('\n');
            report.Append(string.Format("[ProfilerCapture] 帧耗时 avg={0:F2}ms max={1:F2}ms\n", frameAvg, frameMax));
            report.Append("[ProfilerCapture] 有效 recorder: ")
                  .Append(samplers.FindAll(s => s.Samples > 0).Count)
                  .Append('/').Append(samplers.Count).Append('\n');

            for (int i = 0; i < counters.Count; i++)
            {
                Counter c = counters[i];
                if (c.Samples == 0) continue;
                report.Append(string.Format("[ProfilerCapture] {0,-42} avg {1,12:F2}  max {2,12}  ({3})\n",
                    c.Name, c.Avg, c.Max, c.Unit));
            }

            for (int i = 0; i < samplers.Count && i < 16; i++)
            {
                Sampler s = samplers[i];
                if (s.Samples == 0) continue;
                report.Append(string.Format("[ProfilerCapture] {0,-42} avg {1,8:F3}ms  max {2,8:F3}ms  ({3})\n",
                    s.Name, s.AvgMs, s.MaxMs, s.Category));
            }

            Debug.Log(report.ToString());
        }
    }
}
