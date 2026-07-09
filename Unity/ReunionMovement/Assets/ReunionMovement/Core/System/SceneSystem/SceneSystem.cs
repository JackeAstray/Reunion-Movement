using ReunionMovement.Common;
using ReunionMovement.Common.Util.Coroutiner;
using ReunionMovement.Core.Base;
using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine;
using ReunionMovement.Core.UI;

namespace ReunionMovement.Core.Scene
{
    /// <summary>
    /// 场景系统
    /// </summary>
    public class SceneSystem : ICustomSystem
    {
        #region 单例与初始化
        private static readonly Lazy<SceneSystem> instance = new(() => new SceneSystem());
        public static SceneSystem Instance => instance.Value;

        public bool isInited { get; private set; }

        private double initProgress = 0;
        public double InitProgress { get { return initProgress; } }
        #endregion

        #region 数据
        private UnityAction beforeSceneLoadingCompletionCallback = null;  // 场景加载前回调
        private UnityAction sceneLoadingCompletionCallback = null;        // 场景加载完成回调

        private string targetSceneName = null;                            // 将要加载的场景名
        private string currentSceneName = null;                           // 当前场景名，如若没有场景，则默认返回
        private string previousSceneName = null;                          // 上一个场景名
        private bool isLoading = false;                                   // 是否正在加载中
        private const string loadSceneName = "LoadingScene";              // 加载场景名字
        // 场景切换时不隐藏的窗口名称集合（由 LoadScene 调用方在切换前注册）
        private readonly HashSet<string> excludeFromSceneHide = new HashSet<string>();

        public event Action<float> getProgress;                           // 事件 用于处理进度条

        public float startProgressWaitingTime;                            // 开始 - 等待时长
        public float endProgressWaitingTime;                              // 结束 - 等待时长

        public bool openLoad;
        #endregion

        public UniTask Init()
        {
            initProgress = 0;

            currentSceneName = SceneManager.GetActiveScene().name;
            startProgressWaitingTime = 0.5f;
            endProgressWaitingTime = 0.5f;

            initProgress = 100;
            isInited = true;
            Log.Debug("SceneSystem 初始化完成");
            return UniTask.CompletedTask;
        }

        public void Update(float logicTime, float realTime)
        {

        }

        public void Clear()
        {
            Log.Debug("SceneSystem 清除数据");

            isInited = false;
            isLoading = false;
            getProgress = null;
            beforeSceneLoadingCompletionCallback = null;
            sceneLoadingCompletionCallback = null;
            targetSceneName = null;
        }

        #region Load
        /// <summary>
        /// 返回上一场景
        /// </summary>
        public async UniTask LoadPreScene()
        {
            if (!string.IsNullOrEmpty(previousSceneName))
            {
                await LoadScene(previousSceneName);
            }
        }

        /// <summary>
        /// 返回上一场景
        /// </summary>
        public async UniTask LoadPreScene_OpenLoad(UnityAction bslcc = null, UnityAction slcc = null)
        {
            if (string.IsNullOrEmpty(previousSceneName))
            {
                return;
            }
            await LoadScene(previousSceneName, true, bslcc, slcc);
        }

        /// <summary>
        /// 加载场景
        /// </summary>
        /// <param name="levelName">要加载的场景名称</param>
        /// <param name="openLoad">是否开启load场景</param>
        /// <param name="bslcc">场景加载完成前回调</param>
        /// <param name="slcc">场景加载完成回调</param>
        public async UniTask LoadScene(string levelName, bool openLoad = false, UnityAction bslcc = null, UnityAction slcc = null)
        {
            // 正在加载其他场景：拒绝新请求并记录警告
            if (isLoading)
            {
                Log.Warning($"场景加载被拒绝：当前正在加载 {targetSceneName}，无法同时加载 {levelName}");
                return;
            }

            // 目标场景已加载：直接回调
            if (currentSceneName == levelName)
            {
                bslcc?.Invoke();
                slcc?.Invoke();
                return;
            }

            // 锁屏
            isLoading = true;
            // 开始加载
            sceneLoadingCompletionCallback = slcc;
            beforeSceneLoadingCompletionCallback = bslcc;
            targetSceneName = levelName;
            previousSceneName = currentSceneName;
            currentSceneName = loadSceneName;
            this.openLoad = openLoad;

            HideUIWindowsOnSceneChange();

            try
            {
                if (openLoad)
                {
                    await OnLoadingSceneAsync(loadSceneName, LoadSceneMode.Single);
                }
                await OnLoadTargetSceneAsync(targetSceneName, LoadSceneMode.Single);
            }
            catch (Exception ex)
            {
                Log.Error($"场景加载异常：{ex}");
                // 确保异常情况下回调也被触发、状态被重置
                ExecuteBslcc();
                ExecuteSlcc();
                isLoading = false;
                targetSceneName = null;
                currentSceneName = previousSceneName;
            }
        }

        /// <summary>
        /// 加载过渡场景
        /// </summary>
        private async UniTask OnLoadingSceneAsync(string loadSceneName, LoadSceneMode loadSceneMode)
        {
            var async = SceneManager.LoadSceneAsync(loadSceneName, loadSceneMode);
            if (async == null)
            {
                Log.Warning($"过渡场景 {loadSceneName} 加载失败，跳过过渡场景");
                // 即便失败也要触发回调，避免回调泄漏
                ExecuteBslcc();
                CallbackProgress(0);
                return;
            }

            while (!async.isDone)
            {
                await UniTask.Yield(PlayerLoopTiming.Update);
            }

            Log.Debug("Loading场景加载完成！");
            ExecuteBslcc();
            CallbackProgress(0);
        }

        /// <summary>
        /// 加载目标场景
        /// </summary>
        private async UniTask OnLoadTargetSceneAsync(string levelName, LoadSceneMode loadSceneMode)
        {
            AsyncOperation async = SceneManager.LoadSceneAsync(levelName, loadSceneMode);

            if (async == null)
            {
                Log.Error($"加载场景失败：{nameof(AsyncOperation)} 为 null");
                // 触发并清空所有回调，避免泄漏
                ExecuteBslcc();
                ExecuteSlcc();
                isLoading = false;
                targetSceneName = null;
                currentSceneName = previousSceneName;
                return;
            }

            // WebGL 不支持 allowSceneActivation，缓存判断避免重复调用
            bool canControlActivation = Application.platform != RuntimePlatform.WebGLPlayer;
            if (canControlActivation)
            {
                async.allowSceneActivation = false;
            }

            CallbackProgress(0.15f);
            await UniTask.Delay((int)(startProgressWaitingTime * 1000));

            // 加载进度 —— 节流：每 5 帧回调一次，减少 ~80% 事件触发
            int frameSkip = 5;
            int frameCounter = 0;
            while (async.progress < 0.9f)
            {
                if (++frameCounter >= frameSkip)
                {
                    CallbackProgress(async.progress);
                    frameCounter = 0;
                }
                await UniTask.Delay(16);
            }

            await UniTask.Delay((int)(endProgressWaitingTime * 1000));

            CallbackProgress(1f);

            if (canControlActivation)
            {
                async.allowSceneActivation = true;
            }

            while (!async.isDone)
            {
                await UniTask.Delay(16);
            }

            if (!openLoad)
            {
                ExecuteBslcc();
            }

            OnTargetSceneLoaded();

            Log.Debug("目标场景加载完成！");

            ClearExcludeSet();

            ExecuteSlcc();
        }

        /// <summary>
        /// 加载下一场景完成回调
        /// </summary>
        private void OnTargetSceneLoaded()
        {
            isLoading = false;
            currentSceneName = targetSceneName;
            targetSceneName = null;
        }

        /// <summary>
        /// 场景加载完成前回调
        /// </summary>
        private void ExecuteBslcc()
        {
            beforeSceneLoadingCompletionCallback?.Invoke();
            beforeSceneLoadingCompletionCallback = null;
        }
        /// <summary>
        /// 场景加载完成回调
        /// </summary>
        private void ExecuteSlcc()
        {
            sceneLoadingCompletionCallback?.Invoke();
            sceneLoadingCompletionCallback = null;
        }

        /// <summary>
        /// 回调用于返回进度（零分配，单次 try-catch 保护）
        /// </summary>
        public void CallbackProgress(float progress)
        {
            try
            {
                getProgress?.Invoke(progress);
            }
            catch (Exception ex)
            {
                Log.Error($"getProgress 回调异常（忽略）：{ex}");
            }
        }

        /// <summary>
        /// 在场景切换时隐藏UI窗口（跳过 excludeFromSceneHide 中注册的窗口）
        /// </summary>
        private void HideUIWindowsOnSceneChange()
        {
            var windows = UnityEngine.Object.FindObjectsByType<UIWindowAsset>(FindObjectsSortMode.None);

            foreach (var window in windows)
            {
                if (!window.isHiddenWhenLeaveScene) continue;
                if (excludeFromSceneHide.Contains(window.name)) continue;

                window.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// 注册在本次场景切换中不隐藏的窗口（场景加载完成后自动清空）
        /// </summary>
        public void ExcludeWindowFromSceneHide(string windowName)
        {
            excludeFromSceneHide.Add(windowName);
        }

        /// <summary>
        /// 清空排除列表（由场景加载完成后自动调用）
        /// </summary>
        private void ClearExcludeSet()
        {
            excludeFromSceneHide.Clear();
        }
        #endregion
    }
}