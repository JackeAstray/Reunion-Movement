using ReunionMovement.Core;
using ReunionMovement.Core.Sound;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 播放音乐
/// </summary>
public class PlayMusic : MonoBehaviour
{
    // 音乐索引
    public int musicIndex;

    // 可配置的播放参数
    public float volume = -1f; // -1 表示使用全局设置
    public bool useFade = false;
    public float fadeDuration = 3.0f;
    public bool playOnAwake = false;

    private async void Start()
    {
        if (playOnAwake)
        {
            await PlayMusicClip();
        }
    }

    /// <summary>
    /// 播放音乐剪辑
    /// </summary>
    /// <returns></returns>
    public async Task PlayMusicClip()
    {
        // 将淡出/淡入时间传递给SoundSystem
        SoundSystem.Instance.fadeDuration = fadeDuration;

        if (useFade)
        {
            // 使用渐入渐出切换
            await SoundSystem.Instance.PlaySwitch(musicIndex);
        }
        else
        {
            await SoundSystem.Instance.PlayMusic(musicIndex, volume);
        }
    }

    /// <summary>
    /// 暂停音乐
    /// </summary>
    public void PauseMusic()
    {
        SoundSystem.Instance.PauseMusic();
    }

    /// <summary>
    /// 恢复音乐
    /// </summary>
    public void ResumeMusic()
    {
        SoundSystem.Instance.PlayMusic();
    }

    /// <summary>
    /// 停止音乐
    /// </summary>
    public void StopMusic()
    {
        SoundSystem.Instance.StopMusic();
    }

    /// <summary>
    /// 切换静音状态
    /// </summary>
    public void ToggleMute()
    {
        GameOption.currentOption.musicMuted = !GameOption.currentOption.musicMuted;
        // 立即应用到当前AudioSource（通过反射访问私有字段）
        var ssType = SoundSystem.Instance.GetType();
        var field = ssType.GetField("source", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (field != null)
        {
            var audio = field.GetValue(SoundSystem.Instance) as AudioSource;
            if (audio != null) audio.mute = GameOption.currentOption.musicMuted;
        }
    }

    /// <summary>
    /// 设置音乐音量
    /// </summary>
    /// <param name="v"></param>
    public void SetVolume(float v)
    {
        // 0-1 范围
        GameOption.currentOption.musicVolume = v;
        // 立即应用到当前AudioSource
        var ssType = SoundSystem.Instance.GetType();
        var field = ssType.GetField("source", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (field != null)
        {
            var audio = field.GetValue(SoundSystem.Instance) as AudioSource;
            if (audio != null) audio.volume = v;
        }
    }
}
