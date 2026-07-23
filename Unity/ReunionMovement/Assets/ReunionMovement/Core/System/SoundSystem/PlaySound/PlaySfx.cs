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
                PlaySfxClipAsync().Forget();
            }
        }

        /// <summary>
        /// 播放指定ID的音效（Button 可绑定）
        /// </summary>
        /// <param name="id"></param>
        public void PlaySfxById(int id)
        {
            PlaySfxByIdAsync(id).Forget();
        }

        private async UniTask PlaySfxByIdAsync(int id)
        {
            try
            {
                await SoundSystem.Instance.PlaySfx(id, emitter, loop, volume, pitch);
            }
            catch (System.Exception ex)
            {
                Log.Error("[PlaySfx] PlaySfxById 异常: {0}", ex);
            }
        }

        /// <summary>
        /// 播放音效（Button 可绑定）
        /// </summary>
        public void PlaySfxClip()
        {
            PlaySfxClipAsync().Forget();
        }

        private async UniTask PlaySfxClipAsync()
        {
            try
            {
                await SoundSystem.Instance.PlaySfx(sfxIndex, emitter, loop, volume, pitch);
            }
            catch (System.Exception ex)
            {
                Log.Error("[PlaySfx] PlaySfxClip 异常: {0}", ex);
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