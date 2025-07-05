using System.Collections;
using System.Threading.Tasks;
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
        Task OnBeforeInitAsync();

        /// <summary>
        /// 游戏启动
        /// </summary>
        /// <returns></returns>
        Task OnGameStartAsync();
    }
}