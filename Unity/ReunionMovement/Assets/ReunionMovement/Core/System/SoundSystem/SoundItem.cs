using System.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace ReunionMovement.Core.Sound
{
    /// <summary>
    /// 声音对象
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class SoundItem : MonoBehaviour
    {
        private AudioSource source;
        private Coroutine recycleCoroutine;

        private void Awake()
        {
            // 缓存AudioSource组件
            source = GetComponent<AudioSource>();
        }

        /// <summary>
        /// 处理声音对象
        /// </summary>
        /// <param name="audioClip">要播放的音频剪辑</param>
        /// <param name="emitter">声音发射源</param>
        /// <param name="loop">是否循环</param>
        /// <param name="volume">音量</param>
        /// <param name="mute">是否静音</param>
        /// <param name="pitch">音高</param>
        public void Processing(AudioClip audioClip, Transform emitter, bool loop, float volume, bool mute, float pitch = 1f)
        {
            if (audioClip == null)
            {
                // 如果没有有效的音频剪辑，则立即回收
                SoundSystem.Instance.Recycle(gameObject);
                return;
            }

            if (emitter != null)
            {
                transform.parent = emitter;
            }
            else
            {
                transform.parent = SoundSystem.Instance.sfxRoot.transform;
            }

            transform.localPosition = Vector3.zero;

            source.clip = audioClip;
            source.volume = volume;
            source.loop = loop;
            source.mute = mute;
            source.pitch = pitch;

            // 使用 PlayScheduled 替代 Play，消除 Unity 音频线程调度抖动
            // 调度到当前 DSP 时间 + 20ms，确保音频线程有足够时间处理
            double scheduleTime = AudioSettings.dspTime + 0.02;
            source.PlayScheduled(scheduleTime);

            // 停止任何可能正在运行的旧的回收协程
            if (recycleCoroutine != null)
            {
                StopCoroutine(recycleCoroutine);
            }

            // 如果不是循环播放，则在播放结束后自动回收
            if (!loop)
            {
                // 考虑音高和调度偏移对播放时长的影响
                float clipDuration = Mathf.Abs(pitch) > 0f ? audioClip.length / Mathf.Abs(pitch) : audioClip.length;
                float scheduledOffset = 0.02f; // 与 PlayScheduled 的偏移保持一致
                recycleCoroutine = StartCoroutine(RecycleAfterPlaying(scheduledOffset + clipDuration));
            }
        }

        /// <summary>
        /// 更新音量与静音状态（供 SetSfxProperties 批量调用）
        /// </summary>
        public void SetProperties(float volume, bool muted)
        {
            if (source != null)
            {
                source.volume = volume;
                source.mute = muted;
            }
        }

        /// <summary>
        /// 协程：在音频播放完成后回收对象
        /// </summary>
        /// <returns></returns>
        private IEnumerator RecycleAfterPlaying(float duration)
        {
            // 等待音频播放完成
            yield return new WaitForSeconds(duration);
            // 回收对象
            SoundSystem.Instance.Recycle(gameObject);
            recycleCoroutine = null;
        }

        /// <summary>
        /// 在对象被禁用时停止协程
        /// </summary>
        private void OnDisable()
        {
            // 如果对象在播放完成前被禁用（例如，被回收），停止协程以防万一
            if (recycleCoroutine != null)
            {
                StopCoroutine(recycleCoroutine);
                recycleCoroutine = null;
            }
        }
    }
}