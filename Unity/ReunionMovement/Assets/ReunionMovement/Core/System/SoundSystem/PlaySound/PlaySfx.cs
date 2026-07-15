using Cysharp.Threading.Tasks;
using ReunionMovement.Common;
using ReunionMovement.Core.Sound;
using UnityEngine;

namespace ReunionMovement.Core.Sound
{
    /// <summary>
    /// 播放音效
    /// </summary>
    public class PlaySfx : MonoBehaviour
    {
        // 音效索引
        public int sfxIndex;

        // 可配置参数
        public Transform emitter;
        public bool loop = false;
        public bool playOnAwake = false;
        public float volume = -1f; // -1 使用全局
        public float pitch = 1f;

        private void Start()
        {
            if (playOnAwake)
            {
                PlaySfxClip().Forget();
            }
        }

        /// <summary>
        /// 播放指定ID的音效
        /// </summary>
        /// <param name="id"></param>
        public async UniTaskVoid PlaySfxById(int id)
        {
            try
            {
                await SoundSystem.Instance.PlaySfx(id, emitter, loop, volume, pitch);
            }
            catch (System.Exception ex)
            {
                Log.Error($"[PlaySfx] PlaySfxById 异常: {ex}");
            }
        }

        /// <summary>
        /// 播放音效剪辑
        /// </summary>
        public async UniTaskVoid PlaySfxClip()
        {
            try
            {
                await SoundSystem.Instance.PlaySfx(sfxIndex, emitter, loop, volume, pitch);
            }
            catch (System.Exception ex)
            {
                Log.Error($"[PlaySfx] PlaySfxClip 异常: {ex}");
            }
        }

        /// <summary>
        /// 停止音效
        /// </summary>
        public void StopSfx()
        {
            SoundSystem.Instance.StopSfx();
        }
    }
}