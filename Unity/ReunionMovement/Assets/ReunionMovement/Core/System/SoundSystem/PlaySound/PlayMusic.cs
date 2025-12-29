using ReunionMovement.Core;
using ReunionMovement.Core.Sound;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace ReunionMovement.Core.Sound
{
    /// <summary>
    /// 播放音乐
    /// </summary>
    public class PlayMusic : MonoBehaviour
    {
        // 单曲索引（兼容旧设置）
        public int musicIndex;

        // 播放列表（存储SoundConfig索引）
        public List<int> playlist = new List<int>();
        // 播放列表当前位置
        public int playlistPosition = 0;

        // 可配置的播放参数
        public float volume = -1f; // -1 表示使用全局设置
        public bool useFade = false;
        public float fadeDuration = 3.0f;
        public bool playOnAwake = false;

        // 播放列表行为
        public bool autoPlayNext = true; // 是否在曲目结束后自动播放下一曲
        public bool loopPlaylist = true; // 到达列表末尾后是否循环到开头

        private CancellationTokenSource playbackMonitorCts;

        private async void Start()
        {
            if (playOnAwake)
            {
                // 优先使用playlist，如果为空则使用单独的musicIndex
                if (playlist != null && playlist.Count > 0)
                {
                    playlistPosition = Mathf.Clamp(playlistPosition, 0, playlist.Count - 1);
                    await PlayPlaylistAt(playlistPosition);
                }
                else
                {
                    await PlayMusicClip();
                }
            }
        }

        /// <summary>
        /// 播放音乐剪辑（兼容旧单曲字段）
        /// </summary>
        /// <returns></returns>
        public async Task PlayMusicClip()
        {
            // 如果存在播放列表并且有内容，播放列表当前位置的曲目
            if (playlist != null && playlist.Count > 0)
            {
                playlistPosition = Mathf.Clamp(playlistPosition, 0, playlist.Count - 1);
                await PlayPlaylistAt(playlistPosition);
                return;
            }

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

            ApplyLoopSetting();
            StartPlaybackMonitorIfNeeded();
        }

        /// <summary>
        /// 播放播放列表中指定位置的曲目
        /// </summary>
        public async Task PlayPlaylistAt(int pos)
        {
            if (playlist == null || playlist.Count == 0) return;

            playlistPosition = Mathf.Clamp(pos, 0, playlist.Count - 1);
            int index = playlist[playlistPosition];

            // 将淡出/淡入时间传递给SoundSystem
            SoundSystem.Instance.fadeDuration = fadeDuration;

            if (useFade)
            {
                await SoundSystem.Instance.PlaySwitch(index);
            }
            else
            {
                await SoundSystem.Instance.PlayMusic(index, volume);
            }

            ApplyLoopSetting();
            StartPlaybackMonitorIfNeeded();
        }

        /// <summary>
        /// 播放下一曲
        /// </summary>
        public async void PlayNext()
        {
            if (playlist == null || playlist.Count == 0) return;
            CancelPlaybackMonitor();

            playlistPosition++;
            if (playlistPosition >= playlist.Count)
            {
                if (loopPlaylist)
                {
                    playlistPosition = 0;
                }
                else
                {
                    // 到达末尾且不循环，则停在最后一首
                    playlistPosition = playlist.Count - 1;
                    return;
                }
            }

            await PlayPlaylistAt(playlistPosition);
        }

        /// <summary>
        /// 播放上一曲
        /// </summary>
        public async void PlayPrevious()
        {
            if (playlist == null || playlist.Count == 0) return;
            CancelPlaybackMonitor();

            playlistPosition--;
            if (playlistPosition < 0)
            {
                if (loopPlaylist)
                {
                    playlistPosition = playlist.Count - 1;
                }
                else
                {
                    playlistPosition = 0;
                    return;
                }
            }

            await PlayPlaylistAt(playlistPosition);
        }

        /// <summary>
        /// 暂停音乐
        /// </summary>
        public void PauseMusic()
        {
            SoundSystem.Instance.PauseMusic();
            CancelPlaybackMonitor();
        }

        /// <summary>
        /// 恢复音乐
        /// </summary>
        public async void ResumeMusic()
        {
            // 如果已经有clip，则直接恢复播放并启动监视器
            var audio = GetAudioSource();
            if (audio != null && audio.clip != null)
            {
                SoundSystem.Instance.PlayMusic();
                StartPlaybackMonitorIfNeeded();
                return;
            }

            // 否则尝试根据playlist或单曲索引加载并播放
            CancelPlaybackMonitor();

            if (playlist != null && playlist.Count > 0)
            {
                // 确保playlistPosition在有效范围内
                playlistPosition = Mathf.Clamp(playlistPosition, 0, playlist.Count - 1);
                await PlayPlaylistAt(playlistPosition);
            }
            else
            {
                await PlayMusicClip();
            }
        }

        /// <summary>
        /// 停止音乐
        /// </summary>
        public void StopMusic()
        {
            SoundSystem.Instance.StopMusic();
            CancelPlaybackMonitor();
        }

        /// <summary>
        /// 切换静音状态
        /// </summary>
        public void ToggleMute()
        {
            GameOption.currentOption.musicMuted = !GameOption.currentOption.musicMuted;
            // 立即应用到当前AudioSource（通过反射访问私有字段）
            var audio = GetAudioSource();
            if (audio != null) audio.mute = GameOption.currentOption.musicMuted;
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
            var audio = GetAudioSource();
            if (audio != null) audio.volume = v;
        }

        /// <summary>
        /// 应用loop设置到SoundSystem的AudioSource
        /// 如果autoPlayNext为true，则需要让单曲不循环以便在结束时触发下一曲
        /// </summary>
        private void ApplyLoopSetting()
        {
            var audio = GetAudioSource();
            if (audio == null) return;

            audio.loop = !autoPlayNext; // 如果自动播放下一曲则设为不循环
        }

        /// <summary>
        /// 使用反射获取SoundSystem的私有AudioSource
        /// </summary>
        /// <returns></returns>
        private AudioSource GetAudioSource()
        {
            var ssType = SoundSystem.Instance.GetType();
            var field = ssType.GetField("source", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (field != null)
            {
                return field.GetValue(SoundSystem.Instance) as AudioSource;
            }
            return null;
        }

        /// <summary>
        /// 启动播放监视器，用于在曲目结束后自动切换到下一曲
        /// </summary>
        private void StartPlaybackMonitorIfNeeded()
        {
            CancelPlaybackMonitor();
            if (!autoPlayNext) return;

            var audio = GetAudioSource();
            if (audio == null || audio.clip == null) return;

            playbackMonitorCts = new CancellationTokenSource();
            var token = playbackMonitorCts.Token;

            // 启动异步任务监视播放结束（在主线程执行，频率由Task.Yield驱动）
            _ = MonitorPlaybackAsync(audio, token);
        }

        private async Task MonitorPlaybackAsync(AudioSource audio, CancellationToken token)
        {
            // 等待直到播放开始
            await Task.Yield();

            while (!token.IsCancellationRequested)
            {
                if (audio == null || audio.clip == null) break;

                // 如果正在播放则等待
                if (audio.isPlaying)
                {
                    await Task.Yield();
                    continue;
                }

                // 如果没有在播放且音量不为0，可能是刚停止或已完成
                break;
            }

            if (token.IsCancellationRequested) return;

            // 曲目结束，播放下一曲
            if (autoPlayNext)
            {
                // 延迟一帧以确保SoundSystem内部状态更新
                await Task.Yield();
                PlayNext();
            }
        }

        private void CancelPlaybackMonitor()
        {
            if (playbackMonitorCts != null)
            {
                try
                {
                    playbackMonitorCts.Cancel();
                    playbackMonitorCts.Dispose();
                }
                catch { }
                playbackMonitorCts = null;
            }
        }
    }
}