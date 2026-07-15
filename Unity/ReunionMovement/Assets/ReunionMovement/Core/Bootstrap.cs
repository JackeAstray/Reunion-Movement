using ReunionMovement.Common;
using Cysharp.Threading.Tasks;
using System;
using UnityEngine;

namespace ReunionMovement.Core
{
    /// <summary>
    /// 游戏启动引导器 —— 通过 RuntimeInitializeOnLoadMethod 在场景加载前自动启动引擎。
    /// 替代原有的场景 GameObject + GameEntry.Awake() 模式。
    /// </summary>
    public static class Bootstrap
    {
        /// <summary>防止编辑器 Domain Reload 时重复初始化</summary>
        private static bool isInitialized;

        /// <summary>
        /// 在第一个场景加载前自动执行，初始化游戏引擎。
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static async void OnBeforeSceneLoad()
        {
            // 防止编辑器 Domain Reload 时重复执行
            if (isInitialized) return;
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

            try
            {
                await InitializeEngineAsync();
            }
            catch (Exception ex)
            {
                Log.Error($"[Bootstrap] 启动过程发生未处理异常: {ex.Message}\n{ex.StackTrace}");
                isInitialized = false;
            }
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
                Log.Error($"[Bootstrap] 游戏启动失败: {result.ErrorMessage}");
                isInitialized = false;
                // 失败时不阻塞，GameEngine.State == Failed 可供 UI 层查询
            }
        }
    }
}
