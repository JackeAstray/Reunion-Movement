using ReunionMovement.Core.Sound;
using UnityEngine;

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
            PlaySfxClip();
        }
    }

    public void PlaySfxClip()
    {
        SoundSystem.Instance.PlaySfx(sfxIndex, emitter, loop, volume, pitch);
    }

    public void StopSfx()
    {
        SoundSystem.Instance.StopSfx();
    }
}
