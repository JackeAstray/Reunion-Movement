using ReunionMovement.Common;
using Cysharp.Threading.Tasks;
using System;
using UnityEngine;

namespace ReunionMovement.Core
{
    /// <summary>
    /// 游戏启动引导器 —— 通过 RuntimeInitializeOnLoadMethod 在场景加载前自动启动引擎。
    /// 替代原有的场景 GameObject + GameEntry.Awake() 模式。
    ///
    /// 在测试场景中跳过自动启动的方法（任选其一）：
    ///   1. 将测试场景命名为 "Test" 或 "_" 开头（如 Test_Physics、_Sandbox）
    ///   2. 在进入 Play Mode 前通过任意脚本设置：Bootstrap.ForceDisable = true
    /// </summary>
    public static class Bootstrap
    {
        /// <summary>防止编辑器 Domain Reload 时重复初始化</summary>
        private static bool isInitialized;

        /// <summary>
        /// 强制禁用自动启动（设置为 true 后，所有场景都不会触发 Bootstrap）。
        /// 适用于测试场景或需要在 Play Mode 中手动控制初始化流程的情况。
        /// 设置后只在当前 Play Mode 会话有效（Domain Reload 时重置为 false）。
        /// </summary>
        public static bool ForceDisable { get; set; }

        /// <summary>
        /// 在第一个场景加载前自动执行，初始化游戏引擎。
        /// 使用 UniTask.Forget() 替代 async void，确保异常能被 UniTask 调度器正确捕获。
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void OnBeforeSceneLoad()
        {
            // 防止编辑器 Domain Reload 时重复执行
            if (isInitialized) return;

            // 手动禁用开关（可在任意 [RuntimeInitializeOnLoadMethod] 中提前设置）
            if (ForceDisable)
            {
                Log.Debug("[Bootstrap] ForceDisable = true，跳过自动启动");
                return;
            }

#if UNITY_EDITOR
            // 编辑器下：测试/沙盒/示例场景跳过自动启动
            var testScene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
            if (testScene.name.StartsWith("Test") ||
                testScene.name.StartsWith("_") ||
                testScene.name.Contains("Example") ||
                testScene.name.Contains("UIPlaneScene"))
            {
                Log.Debug("[Bootstrap] 测试场景 '{0}'，跳过自动启动", testScene.name);
                return;
            }
#endif

            isInitialized = true;

            // 如果引擎已存在且正在运行/启动中，跳过
            var existingEngine = GameEngine.Current;
            if (existingEngine != null)
            {
                var state = existingEngine.State;
                if (state == EngineState.Running ||
                    state == EngineState.BeforeInit ||
                    state == EngineState.Initializing ||
                    state == EngineState.Starting)
                {
                    Log.Debug("[Bootstrap] 引擎已在运行中，跳过重复初始化");
                    return;
                }
            }

            // 使用 Forget() 替代 async void：
            // - 异常会通过 UniTaskScheduler.UnobservedTaskException 传播（可全局订阅）
            // - 同时提供内联错误处理，清理部分初始化的引擎状态
            InitializeEngineAsync().Forget(ex =>
            {
                Log.Error("[Bootstrap] 启动过程发生未处理异常: {0}\n{1}", ex.Message, ex.StackTrace);
                isInitialized = false;
                // 清理可能已部分初始化的引擎，避免状态残留与 GameObject 泄漏
                GameEngine.Current?.Dispose();
            });
        }

        /// <summary>
        /// 初始化引擎的异步流程
        /// </summary>
        private static async UniTask InitializeEngineAsync()
        {
            // 创建持久化 GameObject 承载 GameEngineDriver
            var driverGo = new GameObject("[GameEngineDriver]");
            driverGo.AddComponent<AudioListener>(); // 兼容旧版场景，避免缺少 AudioListener 报错
            UnityEngine.Object.DontDestroyOnLoad(driverGo);
            var driver = driverGo.AddComponent<GameEngineDriver>();

            // 创建引擎
            var engine = GameEngine.Create();

            // 绑定驱动
            driver.Bind(engine);

            // 创建游戏入口并获取模块列表
            var entry = new StartGame();
            var modules = entry.CreateModules();

            // 启动引擎（可被 await）
            var result = await engine.LaunchAsync(entry, modules);

            if (!result.IsSuccess)
            {
                Log.Error("[Bootstrap] 游戏启动失败: {0}", result.ErrorMessage);
                isInitialized = false;
                // 失败时不阻塞，GameEngine.State == Failed 可供 UI 层查询
            }
        }
    }
}
