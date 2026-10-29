using System;

namespace ReunionMovement.Framework.Runtime
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

        /// <summary>
        /// 显式指定游戏入口；为 null 时按 <c>[assembly: ReunionGameEntry]</c> 解析。
        /// 适用于不想/不能在程序集上写特性的场景（例如入口由配置决定）。
        /// 只在 <c>BeforeSceneLoad</c> 之前设置有效，推荐用 <c>AfterAssembliesLoaded</c> 钩子。
        /// </summary>
        public GameEntry Entry;

        /// <summary>
        /// 没有任何入口声明时，是否回退到框架内置的 <see cref="StartGame"/>。
        /// 默认 false：避免框架被导入已有工程后，自动接管宿主工程的启动流程。
        /// 只有仓库自带的示例工程才需要打开它（本仓库已在 Game/StartGameEntry.cs 声明 StartGame，无需打开）。
        /// </summary>
        public bool AllowBuiltInEntryFallback;

        /// <summary>
        /// 启动时是否按各模块 <c>[ModuleOrder(..., DependsOn)]</c> 声明的依赖，把"被依赖但没注册"的模块自动补入。
        /// <para>
        /// 默认 true：接入方删模块时不用背依赖表，缺的提供者会被补回来（启动日志会打印补入了谁）。
        /// 需要严格按自己的清单启动（例如刻意不启用 Addressables）时设为 false，
        /// 此时缺依赖退回改造前的行为：告警 + 按注册顺序启动。
        /// </para>
        /// </summary>
        public bool AutoExpandModuleDependencies = true;

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
