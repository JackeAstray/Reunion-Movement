using UnityEngine;

namespace ReunionMovement.Core
{
    /// <summary>
    /// MonoBehaviour 桥接 —— 将 Unity 生命周期事件转发给纯 C# 的 GameEngine。
    /// 由 Bootstrap 创建并挂载到 DontDestroyOnLoad 的 GameObject 上。
    /// </summary>
    public sealed class GameEngineDriver : MonoBehaviour
    {
        /// <summary>关联的引擎实例</summary>
        public GameEngine Engine { get; private set; }

        /// <summary>
        /// 绑定引擎实例（由 Bootstrap 调用）
        /// </summary>
        internal void Bind(GameEngine engine)
        {
            Engine = engine;
        }

        private void Update()
        {
            if (Engine == null) return;
            Engine.OnUpdate(Time.deltaTime, Time.unscaledDeltaTime);
        }

        private void OnApplicationQuit()
        {
            Engine?.OnAppQuit();
        }

        private void OnApplicationFocus(bool focus)
        {
            Engine?.OnAppFocus(focus);
        }

        private void OnDestroy()
        {
            Engine?.Dispose();
        }
    }
}
