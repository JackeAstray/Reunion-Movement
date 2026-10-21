using System;

namespace ReunionMovement.Core
{
    /// <summary>
    /// Bootstrap 启动配置。默认值与原硬编码行为完全一致，修改它不会破坏现有工程。
    /// <para>
    /// 设置时机：必须在 <c>RuntimeInitializeLoadType.BeforeSceneLoad</c> 之前，
    /// 推荐用你自己的初始化钩子：
    /// </para>
    /// <code>
    /// [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
    /// static void ConfigureFramework()
    /// {
    ///     Bootstrap.Options.TestScenePrefixes = new[] { "Test", "_", "Sandbox" };
    /// }
    /// </code>
    /// </summary>
    public sealed class BootstrapOptions
    {
        /// <summary>
        /// 测试场景名前缀：命中后引擎照常启动，但跳过自动场景跳转（保留当前场景用于调试）。
        /// </summary>
        public string[] TestScenePrefixes = { "Test", "_" };

        /// <summary>
        /// 测试场景名包含的关键字（前缀匹配不够用时的补充）。
        /// 默认值即旧版本硬编码的判断集合。
        /// </summary>
        public string[] TestSceneContains = { "Example", "UIPlaneScene", "Music", "Audio", "Debug" };

        /// <summary>引擎初始化总超时（秒）。任一模块 Init 或 LoadScene 挂起时强制失败。</summary>
        public float LaunchTimeoutSeconds = 60f;

        /// <summary>场景名是否命中测试规则（前缀优先，其次关键字包含）。</summary>
        public bool IsTestScene(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName)) return false;

            if (TestScenePrefixes != null)
            {
                for (int i = 0; i < TestScenePrefixes.Length; i++)
                {
                    var prefix = TestScenePrefixes[i];
                    if (!string.IsNullOrEmpty(prefix) && sceneName.StartsWith(prefix, StringComparison.Ordinal))
                    {
                        return true;
                    }
                }
            }

            if (TestSceneContains != null)
            {
                for (int i = 0; i < TestSceneContains.Length; i++)
                {
                    var keyword = TestSceneContains[i];
                    if (!string.IsNullOrEmpty(keyword) && sceneName.IndexOf(keyword, StringComparison.Ordinal) >= 0)
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
