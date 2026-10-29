using Gear.Common;
using Gear.Utilities;
using Gear.Framework.Runtime.Base;
using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

// 本仓库的示例入口声明在 Assets/Gear/Game/StartGameEntry.cs（Assembly-CSharp）。
// 框架程序集本身不再声明入口：作为 UPM 包导入宿主工程时，不会自动接管启动。
// 接入自己的游戏：在你自己的程序集里声明 [assembly: GearGameEntry(typeof(MyGameEntry))]。
[assembly: InternalsVisibleTo("Gear.Tests")]

namespace Gear.Framework.Runtime
{
    /// <summary>
    /// 游戏启动引导器 —— 通过 RuntimeInitializeOnLoadMethod 在场景加载前自动启动引擎。
    /// 替代原有的场景 GameObject + GameEntry.Awake() 模式。
    ///
    /// 三种使用方式：
    /// 1. <b>自动（默认）</b>：入口取 <c>[assembly: GearGameEntry]</c> 的声明；没有声明时
    ///    直接报错并跳过启动，<b>不再</b>隐式回退到内置 StartGame（避免接管宿主工程）。
    /// 2. <b>自动 + 改配置</b>：在 BeforeSceneLoad 之前的钩子里改 <see cref="Options"/>，
    ///    包括用 <c>Options.Entry</c> 显式指定入口。
    /// 3. <b>手动</b>：<c>Bootstrap.Disable()</c> 后自己调 <see cref="StartAsync"/>。
    ///
    /// 测试场景（引擎照常启动，但跳过自动场景跳转）的判定规则见 <see cref="BootstrapOptions"/>。
    /// </summary>
    public static class Bootstrap
    {
        /// <summary>启动配置。需在 BeforeSceneLoad 之前修改。</summary>
        public static BootstrapOptions Options { get; set; } = new BootstrapOptions();

        /// <summary>
        /// 强制禁用自动启动。设置后所有场景都不会触发 Bootstrap。
        /// 适用于测试场景或需要在 Play Mode 中手动控制初始化流程的情况。
        /// 设置后只在当前 Play Mode 会话有效（Domain Reload 时重置为 false）。
        /// </summary>
        public static bool ForceDisable { get; set; }

        /// <summary>当前是否为测试/调试场景（引擎仍启动，但跳过自动场景跳转）</summary>
        public static bool IsTestScene { get; private set; }

        /// <summary>防止编辑器 Domain Reload 时重复初始化</summary>
        private static bool isInitialized;

        /// <summary>承载 GameEngineDriver 的持久化 GameObject（启动失败时必须销毁，避免残留多个 Driver）</summary>
        private static GameObject driverGo;

        /// <summary>关闭自动启动（等价于 <c>ForceDisable = true</c>），之后可自行调用 <see cref="StartAsync"/>。</summary>
        public static void Disable()
        {
            ForceDisable = true;
        }

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

            // 入口解析结果与引擎静态状态都带会话语义，必须一起复位
            GameEntryResolver.Refresh();
            GameEngine.ResetStaticState();

            // MonoBehaviour 单例的复位由 Modules 程序集自己的
            // [RuntimeInitializeOnLoadMethod(SubsystemRegistration)] 完成（见 SingletonResets）。
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
            if (Options.IsTestScene(activeScene.name))
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
        /// 手动启动引擎（配合 <see cref="Disable"/> 使用）。
        /// 会按需创建 GameEngineDriver 与 GameEngine；失败时自动清理。
        /// </summary>
        /// <param name="entry">游戏入口实例</param>
        /// <param name="modules">模块列表；传 null 时使用 <c>entry.CreateModules()</c></param>
        public static async UniTask<InitResult> StartAsync(IGameEntry entry, IList<ICustomSystem> modules = null)
        {
            if (entry == null)
            {
                Log.Error("[Bootstrap] StartAsync: entry 不能为 null");
                return InitResult.Failure("GameEntry 不能为 null");
            }

            if (modules == null && entry is GameEntry gameEntry)
            {
                modules = gameEntry.CreateModules();
            }
            modules ??= new List<ICustomSystem>();

            var driver = EnsureDriver();
            var engine = GameEngine.Create();
            driver.Bind(engine);

            InitResult result;
            try
            {
                result = await engine.LaunchAsync(entry, modules, default, Options.LaunchTimeoutSeconds);
            }
            catch (Exception ex)
            {
                // LaunchAsync 内部已兜住常规异常，这里防的是它自身抛出的意外
                Log.Error("[Bootstrap] StartAsync 未预期异常: {0}", ex.Message);
                CleanupFailedStartup();
                return InitResult.Failure(ex.Message, ex);
            }

            if (!result.IsSuccess)
            {
                Log.Error("[Bootstrap] 游戏启动失败: {0}", result.ErrorMessage);
                // 失败时统一清理：销毁 Driver、Dispose 引擎（含已初始化模块），
                // 避免 GameEngine.State == Failed 与残留 GameObject
                CleanupFailedStartup();
            }
            return result;
        }

        /// <summary>
        /// 保证存在且仅存在一个 GameEngineDriver（持久化物件）。
        /// </summary>
        private static GameEngineDriver EnsureDriver()
        {
            if (driverGo != null)
            {
                var existing = driverGo.GetComponent<GameEngineDriver>();
                if (existing != null) return existing;

                // 物件还在但组件没了（被外部误删）：回收后重建
                UnityEngine.Object.Destroy(driverGo);
                driverGo = null;
            }

            // 注意：AudioListener 不在此处挂载。此处处于 BeforeSceneLoad，场景对象虽已载入但尚未激活，
            // FindFirstObjectByType 在 Unity 2022.2+ 找不到它们，会把"场景自带监听器"误判为"没有监听器"
            // 而重复创建。改由 OnAfterSceneLoad / OnSceneLoaded（场景就绪后）统一校准。
            var go = new GameObject("[GameEngineDriver]");
            driverGo = go;
            UnityEngine.Object.DontDestroyOnLoad(go);
            return go.AddComponent<GameEngineDriver>();
        }

        /// <summary>
        /// 默认入口：<c>Options.Entry</c> → <c>[assembly: GearGameEntry]</c> →（可选）内置 StartGame。
        /// 没有任何入口时返回 null 并报错，<b>不再</b>默默接管宿主工程的启动流程。
        /// </summary>
        private static GameEntry ResolveEntry()
        {
            var entry = ResolveEntry(Options.Entry, GameEntryResolver.CreateEntry, Options.AllowBuiltInEntryFallback);
            if (entry == null)
            {
                Log.Error("[Bootstrap] 未声明游戏入口。请加 [assembly: GearGameEntry(typeof(MyGameEntry))]，" +
                          "或设置 Bootstrap.Options.Entry；内置 StartGame 只在示例工程中使用" +
                          "（确需回退时显式设置 Bootstrap.Options.AllowBuiltInEntryFallback = true）。");
            }
            return entry;
        }

        /// <summary>
        /// 可测试的入口选择逻辑：配置优先，其次声明，最后按开关决定是否回退内置 StartGame。
        /// </summary>
        internal static GameEntry ResolveEntry(GameEntry configured, Func<GameEntry> declaredFactory,
            bool allowBuiltInFallback)
        {
            if (configured != null) return configured;

            var declared = declaredFactory?.Invoke();
            if (declared != null) return declared;

            return allowBuiltInFallback ? new StartGame() : null;
        }

        /// <summary>
        /// 初始化引擎的异步流程（自动启动路径）。
        /// </summary>
        private static async UniTask InitializeEngineAsync()
        {
            // 启动早期启用全局错误捕获（崩溃日志落盘 + 可选上报），幂等。
            // ErrorReporter 在 Modules 程序集里注册到 Base 的桥，Runtime 不直接引用 Modules。
            ErrorReportingBridge.TryInitialize();

            // 若上次失败残留了 driver，先清理，避免重复 Driver
            if (driverGo != null)
            {
                UnityEngine.Object.Destroy(driverGo);
                driverGo = null;
            }

            var entry = ResolveEntry();
            if (entry == null)
            {
                // 已在 ResolveEntry 内报错；不创建 Driver，也不把启动失败伪装成引擎异常。
                // 调用方仍可在之后手动 Bootstrap.StartAsync(entry) 启动。
                return;
            }

            await StartAsync(entry);
        }

        /// <summary>
        /// 场景加载完成后校准 AudioListener，并订阅后续场景切换（幂等）。
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void OnAfterSceneLoad()
        {
            SyncAudioListener();

            // 关闭 Domain Reload 时静态事件订阅会跨 Play 会话残留，先移除再订阅避免重复回调
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
        }

        /// <summary>场景切换后重新校准（首个场景可能自带监听器，后续场景可能没有）</summary>
        private static void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene,
            UnityEngine.SceneManagement.LoadSceneMode mode)
        {
            SyncAudioListener();
        }

        /// <summary>
        /// 保证任意场景下都有且仅有一个可用 AudioListener：
        /// 场景自带监听器时移除 Driver 上的监听器，场景没有监听器时挂到 Driver 上。
        /// 必须在场景加载完成后调用 —— 对象的 Awake/OnEnable 已执行、状态为激活，
        /// FindObjectsByType 才能正确枚举到场景内的监听器。
        /// </summary>
        private static void SyncAudioListener()
        {
            // driverGo 为 null（ForceDisable / 启动失败已清理）时无需处理
            if (driverGo == null) return;

            var driverListener = driverGo.GetComponent<AudioListener>();
            var listeners = UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None);

            // 除 Driver 自身外是否还有可用监听器
            bool hasSceneListener = false;
            for (int i = 0; i < listeners.Length; i++)
            {
                if (listeners[i] != null && listeners[i] != driverListener)
                {
                    hasSceneListener = true;
                    break;
                }
            }

            if (hasSceneListener)
            {
                // 场景已提供监听器，移除 Driver 上的，避免出现双监听器警告。
                // Destroy 要到帧末才生效，先置 enabled=false 立即退出启用计数，避免过渡帧告警
                if (driverListener != null)
                {
                    driverListener.enabled = false;
                    UnityEngine.Object.Destroy(driverListener);
                    Log.Debug("[Bootstrap] 场景已存在 AudioListener，移除 Driver 上的监听器");
                }
            }
            else if (driverListener == null)
            {
                driverGo.AddComponent<AudioListener>(); // 兼容旧版场景，避免缺少 AudioListener 报错
                Log.Debug("[Bootstrap] 当前场景无 AudioListener，已在 Driver 上补建");
            }
        }
    }
}
