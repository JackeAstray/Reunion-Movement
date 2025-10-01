using ReunionMovement.Common;
using ReunionMovement.Common.Util.Coroutiner;
using ReunionMovement.Core.Base;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine;
using ReunionMovement.Core.UI;

namespace ReunionMovement.Core.Scene
{
    /// <summary>
    /// 场景系统
    /// </summary>
    public class SceneSystem : ICustommSystem
    {
        #region 单例与初始化
        private static readonly Lazy<SceneSystem> instance = new(() => new SceneSystem());
        public static SceneSystem Instance => instance.Value;
        public bool IsInited { get; private set; }
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

        public event Action<float> getProgress;                           // 事件 用于处理进度条

        public float startProgressWaitingTime;                            // 开始 - 等待时长
        public float endProgressWaitingTime;                              // 结束 - 等待时长

        public bool openLoad;
        #endregion

        public async Task Init()
        {
            initProgress = 0;

            await OnInit();

            initProgress = 100;
            IsInited = true;
            Log.Debug("SceneSystem 初始化完成");
        }

        public void Update(float logicTime, float realTime)
        {

        }

        public void Clear()
        {
            Log.Debug("SceneSystem 清除数据");
        }

        private Task OnInit()
        {
            currentSceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            startProgressWaitingTime = 0.5f;
            endProgressWaitingTime = 0.5f;
            return Task.CompletedTask;
        }

        #region Load
        /// <summary>
        /// 返回上一场景
        /// </summary>
        public async Task LoadPreScene()
        {
            if (!string.IsNullOrEmpty(previousSceneName))
            {
                await LoadScene(previousSceneName);
            }
        }

        /// <summary>
        /// 返回上一场景
        /// </summary>
        public async Task LoadPreScene_OpenLoad(UnityAction bslcc = null, UnityAction slcc = null)
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
        /// <param name="strLevelName">要加载的场景名称</param>
        /// <param name="openLoad">是否开启load场景</param>
        /// <param name="bslcc">场景加载完成前回调</param>
        /// <param name="slcc">场景加载完成回调</param>
        public async Task LoadScene(string strLevelName, bool openLoad = false, UnityAction bslcc = null, UnityAction slcc = null)
        {
            await LoadSceneAsync(strLevelName, openLoad, bslcc, slcc);
        }

        /// <summary>
        /// 加载场景
        /// </summary>
        /// <param name="levelName">要加载的场景名称</param>
        /// <param name="openLoad">是否开启load场景</param>
        /// <param name="slcc">场景加载完成回调</param>
        /// <param name="bslcc">场景加载完成前回调</param>
        private async Task LoadSceneAsync(string levelName, bool openLoad, UnityAction bslcc, UnityAction slcc)
        {
            if (isLoading || currentSceneName == levelName)
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

            if (openLoad)
            {
                await OnLoadingSceneAsync(loadSceneName, LoadSceneMode.Single);
            }
            await OnLoadTargetSceneAsync(targetSceneName, LoadSceneMode.Single);
        }

        /// <summary>
        /// 加载过渡场景
        /// </summary>
        /// <param name="loadSceneName"></param>
        /// <param name="onSecenLoaded"></param>
        /// <param name="loadSceneMode"></param>
        /// <returns></returns>
        private async Task OnLoadingSceneAsync(string loadSceneName, LoadSceneMode loadSceneMode)
        {
            var async = SceneManager.LoadSceneAsync(loadSceneName, loadSceneMode);
            if (async == null) return;

            while (!async.isDone)
            {
                await Task.Yield();
            }

            Log.Debug("Loading场景加载完成！");
            ExecuteBslcc();
            CallbackProgress(0);
        }

        /// <summary>
        /// 加载目标场景
        /// </summary>
        /// <param name="levelName"></param>
        /// <param name="loadSceneMode"></param>
        /// <returns></returns>
        private async Task OnLoadTargetSceneAsync(string levelName, LoadSceneMode loadSceneMode)
        {
            AsyncOperation async = SceneManager.LoadSceneAsync(levelName, loadSceneMode);

            if (async == null)
            {
                Log.Error($"加载场景失败：{nameof(AsyncOperation)} 为 null");
                return;
            }

            if (Application.platform != RuntimePlatform.WebGLPlayer)
            {
                async.allowSceneActivation = false;
            }

            CallbackProgress(0.15f);
            await Task.Delay(TimeSpan.FromSeconds(startProgressWaitingTime));

            //加载进度
            while (async.progress < 0.9f)
            {
                CallbackProgress(async.progress);
                await Task.Yield();
            }

            await Task.Delay(TimeSpan.FromSeconds(endProgressWaitingTime));

            CallbackProgress(1f);

            if (Application.platform != RuntimePlatform.WebGLPlayer)
            {
                async.allowSceneActivation = true;
            }

            while (!async.isDone)
            {
                await Task.Yield();
            }

            OnTargetSceneLoaded();

            Log.Debug("目标场景加载完成！");
            if (!openLoad)
            {
                ExecuteBslcc();
            }

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
            beforeSceneLoadingCompletionCallback = null;
        }

        /// <summary>
        /// 场景加载完成前回调
        /// </summary>
        private void ExecuteBslcc()
        {
            beforeSceneLoadingCompletionCallback?.Invoke();
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
        /// 回调用于返回进度
        /// </summary>
        /// <param name="progress"></param>
        public void CallbackProgress(float progress)
        {
            Log.Debug("加载场景进度：" + progress);
            if (getProgress != null)
            {
                getProgress(progress);
            }
        }

        /// <summary>
        /// 在场景切换时隐藏UI窗口
        /// </summary>
        private void HideUIWindowsOnSceneChange()
        {
            // 获取所有激活的UIWindowAsset  
            var windows = UnityEngine.Object.FindObjectsByType<UIWindowAsset>(FindObjectsSortMode.None);

            foreach (var window in windows)
            {
                if (window.IsHidenWhenLeaveScene)
                {
                    // 这里可以是隐藏、关闭或销毁窗口的逻辑  
                    window.gameObject.SetActive(false);
                }
            }
        }
        #endregion
    }
}