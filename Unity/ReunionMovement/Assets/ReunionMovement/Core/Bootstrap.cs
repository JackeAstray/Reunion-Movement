using ReunionMovement.Common;
using ReunionMovement.Common.Util;
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

        /// <summary>承载 GameEngineDriver 的持久化 GameObject（启动失败时必须销毁，避免残留多个 Driver）</summary>
        private static GameObject driverGo;

        /// <summary>
        /// 启动失败/异常时的统一清理：销毁 Driver、Dispose 引擎、复位状态。
        /// 防止重试时残留多个 GameEngineDriver 导致每帧事件重复触发。
        /// </summary>
        private static void CleanupFailedStartup()
        {
            isInitialized = false;
            GameEngine.Current?.Dispose();
            if (driverGo != null)
            {
                UnityEngine.Object.Destroy(driverGo);
                driverGo = null;
            }
        }

        /// <summary>
        /// 强制禁用自动启动（设置为 true 后，所有场景都不会触发 Bootstrap）。
        /// 适用于测试场景或需要在 Play Mode 中手动控制初始化流程的情况。
        /// 设置后只在当前 Play Mode 会话有效（Domain Reload 时重置为 false）。
        /// </summary>
        public static bool ForceDisable { get; set; }

        /// <summary>当前是否为测试/调试场景（引擎仍启动，但跳过自动场景跳转）</summary>
        public static bool IsTestScene { get; private set; }

        /// <summary>
        /// 关闭 Domain Reload（Enter Play Mode Options → Disable Domain Reload）时，
        /// 静态字段不会自动重置。SubsystemRegistration 在每次进入 Play Mode 时都会执行，
        /// 在此复位所有静态启动状态，避免跨 Play 会话污染（引擎漏启动/误判测试场景等）。
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticStateOnPlay()
        {
            isInitialized = false;
            ForceDisable = false;
            IsTestScene = false;
            driverGo = null;
            GameEngine.ResetStaticState();
        }

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
            // 编辑器下：测试/沙盒/示例场景 —— 仍启动引擎（初始化所有模块），但标记为测试场景
            // 以便 StartGame.OnGameStartAsync() 跳过自动场景跳转
            var activeScene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
            if (activeScene.name.StartsWith("Test") ||
                activeScene.name.StartsWith("_") ||
                activeScene.name.Contains("Example") ||
                activeScene.name.Contains("UIPlaneScene") ||
                activeScene.name.Contains("Music") ||
                activeScene.name.Contains("Audio") ||
                activeScene.name.Contains("Debug"))
            {
                Log.Debug("[Bootstrap] 测试场景 '{0}'，引擎正常启动但跳过自动场景跳转", activeScene.name);
                IsTestScene = true;
                // 不 return，继续初始化引擎
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
                // 统一清理：销毁 Driver、Dispose 引擎、复位状态
                CleanupFailedStartup();
            });
        }

        /// <summary>
        /// 初始化引擎的异步流程
        /// </summary>
        private static async UniTask InitializeEngineAsync()
        {
            // 启动早期启用全局错误捕获（崩溃日志落盘 + 可选上报），幂等
            ErrorReporter.Initialize();

            // 若上次失败残留了 driver，先清理，避免重复 Driver
            if (driverGo != null)
            {
                UnityEngine.Object.Destroy(driverGo);
                driverGo = null;
            }

            // 创建持久化 GameObject 承载 GameEngineDriver
            var go = new GameObject("[GameEngineDriver]");
            driverGo = go;
            // 仅当场景确实无 AudioListener 时才挂载，避免与场景相机自带监听器形成双监听器警告
            if (UnityEngine.Object.FindFirstObjectByType<AudioListener>() == null)
            {
                go.AddComponent<AudioListener>(); // 兼容旧版场景，避免缺少 AudioListener 报错
            }
            UnityEngine.Object.DontDestroyOnLoad(go);
            var driver = go.AddComponent<GameEngineDriver>();

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
                // 失败时统一清理：销毁 Driver、Dispose 引擎（含已初始化模块），
                // 避免 GameEngine.State == Failed 与残留 GameObject
                CleanupFailedStartup();
            }
        }
    }
}
