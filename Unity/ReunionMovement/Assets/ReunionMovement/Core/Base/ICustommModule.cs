using System.Collections;
using System.Threading.Tasks;
using UnityEngine;

namespace ReunionMovement.Core.Base
{
    /// <summary>
    /// 自定义模块
    /// </summary>
    public interface ICustommModule
    {
        /// <summary>
        /// 初始化进度
        /// </summary>
        double InitProgress { get; }

        /// <summary>
        /// 初始化协程
        /// </summary>
        /// <returns></returns>
        Task InitAsync();

        /// <summary>
        /// 更新模块时间 （以秒为单位）
        /// </summary>
        /// <param name="logicTime">逻辑流逝时间</param>
        /// <param name="realTime">真实流逝时间(timeScale从上一帧到当前帧的独立间隔（秒）)</param>
        void Update(float logicTime, float realTime);

        /// <summary>
        /// 清理
        /// </summary>
        void Clear();
    }
}