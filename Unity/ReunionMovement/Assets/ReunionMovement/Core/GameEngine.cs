using ReunionMovement.Common;
using ReunionMovement.Core.Base;
using System.Collections;
using System;
using System.Collections.Generic;
using UnityEngine;
using System.Threading.Tasks;

namespace ReunionMovement.Core
{
    /// <summary>
    /// 游戏引擎类
    /// </summary>

    public class GameEngine : SingletonMgr<GameEngine>
    {
        /// <summary>
        /// 所有自定义游戏逻辑模块
        /// </summary>
        public IList<ICustommSystem> gameModules { get; private set; }

        /// <summary>
        /// 进入
        /// </summary>
        public IGameEntry gameEntry { get; private set; }

        /// <summary>
        /// 是否初始化完成
        /// </summary>
        public bool isInited { get; private set; }
        /// <summary>
        /// 在初始化之前
        /// </summary>
        public bool isBeforeInit { get; private set; }
        /// <summary>
        /// 正在初始化
        /// </summary>
        public bool isOnInit { get; private set; }
        /// <summary>
        /// 是否开始游戏
        /// </summary>
        public bool isStartGame { get; private set; }

        // 应用程序退出
        public static bool isApplicationQuit = false;
        // 应用程序焦点（是否被压入后台）
        public static bool isApplicationFocus = true;
        // 应用程序正在运行
        public static bool isAppPlaying = false;
        // 更新事件
        public static Action UpdateEvent;
        // 每300ms事件更新一次
        public static Action UpdatePer300msEvent;
        // 每1s事件更新一次
        public static Action UpdatePer1sEvent;

        // 更新间隔时间 (每1s)
        private float time_update_per1s;
        // 更新间隔时间 (每300ms)
        private float time_update_per300ms;

        private bool isWindowsEditor;

        /// <summary>
        /// 启动入口
        /// </summary>
        /// <param name="gameObjectToAttach"></param>
        /// <param name="entry"></param>
        /// <param name="modules"></param>
        /// <returns></returns>
        public static GameEngine StartEngine(GameObject gameObjectToAttach, IGameEntry entry, IList<ICustommSystem> modules)
        {
            GameEngine appEngine = gameObjectToAttach.AddComponent<GameEngine>();
            appEngine.gameModules = modules;
            appEngine.gameEntry = entry;

            return appEngine;
        }

        protected override void Awake()
        {
            base.Awake();

            _ = InitAsync();
        }

        void Start()
        {
            isAppPlaying = true;

            if (Application.platform == RuntimePlatform.WindowsEditor)
            {
                isWindowsEditor = true;
            }
        }

        /// <summary>
        /// 初始化游戏引擎
        /// </summary>
        private async Task InitAsync()
        {
            var t0 = Time.realtimeSinceStartup; // 记录开始时间
            await Task.Yield();

            isBeforeInit = true;
            if (gameEntry != null)
            {
                await gameEntry.OnBeforeInitAsync();
            }

            isOnInit = true;
            await OnInitModulesAsync(gameModules);

            isStartGame = true;
            if (gameEntry != null)
            {
                await gameEntry.OnGameStartAsync();
            }

            isInited = true;

            var t1 = Time.realtimeSinceStartup; // 记录结束时间
            Log.Debug($"[GameEngine]->[InitAsync] 总耗时: {Math.Round(t1 - t0, 3)} 秒");
        }

        /// <summary>
        /// 执行初始化模块（异步）
        /// </summary>
        private async Task OnInitModulesAsync(IList<ICustommSystem> modules)
        {
            foreach (ICustommSystem initModule in modules)
            {
                if (isWindowsEditor)
                {
                    var startInitTime = Time.time;
                    var startMem = GC.GetTotalMemory(false);
                }

                var startTime = Time.realtimeSinceStartup;
                await initModule.Init();
                var endTime = Time.realtimeSinceStartup;

                if (isWindowsEditor)
                {
                    var nowMem = GC.GetTotalMemory(false);
                }

                Log.Debug($"Module {initModule.GetType().Name} InitAsync耗时: {Math.Round(endTime - startTime, 3)}秒");
            }
        }

        protected virtual void Update()
        {
            UpdateEvent?.Invoke();
            float time = Time.time;
            if (time > time_update_per1s)
            {
                time_update_per1s = time + 1.0f;
                UpdatePer1sEvent?.Invoke();
            }
            if (time > time_update_per300ms)
            {
                time_update_per300ms = time + 0.3f;
                UpdatePer300msEvent?.Invoke();
            }

            foreach (ICustommSystem module in gameModules)
            {
                module.Update(Time.deltaTime, Time.unscaledDeltaTime);
            }
        }

        /// <summary>
        /// 退出处理
        /// </summary>
        void OnApplicationQuit()
        {
            isApplicationQuit = true;
            isAppPlaying = false;
        }
        /// <summary>
        /// 焦点处理
        /// </summary>
        /// <param name="focus"></param>
        void OnApplicationFocus(bool focus)
        {
            isApplicationFocus = focus;
        }

        /// <summary>
        /// 清除数据，比如切换帐号/低内存等清空缓存数据
        /// </summary>
        public void ClearModuleData()
        {
            foreach (ICustommSystem initModule in gameModules)
            {
                initModule.Clear();
            }
        }
    }
}