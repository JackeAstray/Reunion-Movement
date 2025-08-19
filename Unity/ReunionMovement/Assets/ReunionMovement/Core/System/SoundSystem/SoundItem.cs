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
        public AudioClip clip;
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
        /// <param name="index"></param>
        /// <param name="emitter"></param>
        /// <param name="loop"></param>
        /// <param name="volume"></param>
        /// <param name="nute"></param>
        public void Processing(int index, Transform emitter, bool loop, float volume, bool nute)
        {
            if (emitter != null)
            {
                transform.parent = emitter;
            }
            else
            {
                transform.parent = SoundSystem.Instance.sfxRoot.transform;
            }

            transform.localPosition = Vector3.zero;

            source.clip = clip;
            source.volume = volume;
            source.loop = loop;
            source.mute = nute;
            source.Play();

            // 如果不是循环播放，则在播放结束后自动回收
            if (!loop)
            {
                recycleCoroutine = StartCoroutine(RecycleAfterPlaying());
            }
        }

        /// <summary>
        /// 协程：在音频播放完成后回收对象
        /// </summary>
        /// <returns></returns>
        private IEnumerator RecycleAfterPlaying()
        {
            // 等待音频播放完成
            yield return new WaitForSeconds(clip.length);
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