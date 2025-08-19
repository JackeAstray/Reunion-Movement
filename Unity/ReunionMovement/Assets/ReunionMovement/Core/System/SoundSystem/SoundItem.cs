using System.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace ReunionMovement.Core.Sound
{
    /// <summary>
    /// 声音对象
    /// </summary>
    public class SoundItem : MonoBehaviour
    {
        public AudioClip clip;

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
            StartCoroutine(ObjectProcessing(index, emitter, loop, volume, nute));
        }

        /// <summary>
        /// 处理声音对象
        /// </summary>
        /// <param name="index"></param>
        /// <param name="emitter"></param>
        /// <param name="loop"></param>
        /// <param name="volume"></param>
        /// <param name="nute"></param>
        /// <returns></returns>
        IEnumerator ObjectProcessing(int index, Transform emitter, bool loop, float volume, bool nute)
        {
            if (emitter != null)
            {
                gameObject.transform.parent = emitter;
            }
            else
            {
                gameObject.transform.parent = SoundSystem.Instance.sfxRoot.transform;
            }

            gameObject.transform.localPosition = Vector3.zero;
            gameObject.SetActive(true);

            AudioSource source = gameObject.GetComponent<AudioSource>();
            source.clip = clip;
            source.volume = volume;
            source.loop = loop;
            source.mute = nute;
            source.Play();

            yield return new WaitForSeconds(clip.length);

            SoundSystem.Instance.Recycle(gameObject);
        }
    }
}