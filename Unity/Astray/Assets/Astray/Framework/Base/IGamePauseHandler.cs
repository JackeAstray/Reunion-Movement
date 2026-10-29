namespace Astray.Framework.Runtime.Base
{
    /// <summary>
    /// 游戏级暂停的执行者。
    /// <para>
    /// 引擎层（<see cref="Astray.Framework.Runtime.GameEngine"/>）只依赖本接口，不依赖任何具体模块：
    /// 需要暂停时向已注册的模块里找一个实现者，找不到就只切换引擎状态。
    /// </para>
    /// <para>
    /// 具体实现见 <c>Astray.Framework.Runtime.Pause.PauseSystem</c>（timeScale + 音频 + 多原因计票）。
    /// </para>
    /// </summary>
    public interface IGamePauseHandler
    {
        /// <summary>当前是否处于游戏级暂停。</summary>
        bool IsPaused { get; }

        /// <summary>切换暂停状态（实现方负责 timeScale / 音频等副作用）。</summary>
        void SetPaused(bool paused);
    }
}
