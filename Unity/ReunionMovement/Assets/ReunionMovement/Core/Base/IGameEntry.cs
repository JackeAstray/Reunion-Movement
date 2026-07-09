using Cysharp.Threading.Tasks;
using UnityEngine;

namespace ReunionMovement.Core.Base
{
    /// <summary>
    /// 游戏入口接口
    /// </summary>
    public interface IGameEntry
    {
        /// <summary>
        /// 在初始化之前
        /// </summary>
        /// <returns></returns>
        UniTask OnBeforeInitAsync();

        /// <summary>
        /// 游戏启动
        /// </summary>
        /// <returns></returns>
        UniTask OnGameStartAsync();
    }
}