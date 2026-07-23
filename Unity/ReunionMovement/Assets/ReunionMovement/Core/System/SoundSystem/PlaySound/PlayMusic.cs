using ReunionMovement.Common;
using ReunionMovement.Core;
using ReunionMovement.Core.Sound;
using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using System.Threading;
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

        private void Start()
        {
            if (playOnAwake)
            {
                InitializeOnStartAsync().Forget();
            }
        }

        private async UniTaskVoid InitializeOnStartAsync()
        {
            try
            {
                // 优先使用playlist，如果为空则使用单独的musicIndex
                if (playlist != null && playlist.Count > 0)
                {
                    playlistPosition = Mathf.Clamp(playlistPosition, 0, playlist.Count - 1);
                    await PlayPlaylistAtAsync(playlistPosition);
                }
                else
                {
                    await PlayMusicClipAsync();
                }
            }
            catch (System.Exception ex)
            {
                Log.Error("[PlayMusic] Start 初始化异常: {0}", ex);
            }
        }

        /// <summary>
        /// 播放音乐剪辑 - Button 可绑定
        /// </summary>
        public void PlayMusicClip()
        {
            PlayMusicClipAsync().Forget();
        }

        private async UniTask PlayMusicClipAsync()
        {
            if (playlist != null && playlist.Count > 0)
            {
                playlistPosition = Mathf.Clamp(playlistPosition, 0, playlist.Count - 1);
                await PlayPlaylistAtAsync(playlistPosition);
                return;
            }

            SoundSystem.Instance.fadeDuration = fadeDuration;

            if (useFade)
            {
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
        /// 播放列表中指定位置 - Button 可绑定（参数 int pos）
        /// </summary>
        public void PlayPlaylistAt(int pos)
        {
            PlayPlaylistAtAsync(pos).Forget();
        }

        private async UniTask PlayPlaylistAtAsync(int pos)
        {
            if (playlist == null || playlist.Count == 0) return;

            playlistPosition = Mathf.Clamp(pos, 0, playlist.Count - 1);
            int index = playlist[playlistPosition];

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
        /// 播放下一曲 - Button 可绑定
        /// </summary>
        public void PlayNext()
        {
            PlayNextAsync().Forget();
        }

        private async UniTask PlayNextAsync()
        {
            try
            {
                if (playlist == null || playlist.Count == 0) return;
                CancelPlaybackMonitor();

                playlistPosition++;
                if (playlistPosition >= playlist.Count)
                {
                    if (loopPlaylist)
                        playlistPosition = 0;
                    else
                    {
                        playlistPosition = playlist.Count - 1;
                        return;
                    }
                }

                await PlayPlaylistAtAsync(playlistPosition);
            }
            catch (System.Exception ex)
            {
                Log.Error("[PlayMusic] PlayNext 异常: {0}", ex);
            }
        }

        /// <summary>
        /// 播放上一曲 - Button 可绑定
        /// </summary>
        public void PlayPrevious()
        {
            PlayPreviousAsync().Forget();
        }

        private async UniTask PlayPreviousAsync()
        {
            try
            {
                if (playlist == null || playlist.Count == 0) return;
                CancelPlaybackMonitor();

                playlistPosition--;
                if (playlistPosition < 0)
                {
                    if (loopPlaylist)
                        playlistPosition = playlist.Count - 1;
                    else
                    {
                        playlistPosition = 0;
                        return;
                    }
                }

                await PlayPlaylistAtAsync(playlistPosition);
            }
            catch (System.Exception ex)
            {
                Log.Error("[PlayMusic] PlayPrevious 异常: {0}", ex);
            }
        }

        /// <summary>
        /// 暂停音乐 - Button 可绑定
        /// </summary>
        public void PauseMusic()
        {
            SoundSystem.Instance.PauseMusic();
            CancelPlaybackMonitor();
        }

        /// <summary>
        /// 恢复音乐 - Button 可绑定
        /// </summary>
        public void ResumeMusic()
        {
            ResumeMusicAsync().Forget();
        }

        private async UniTask ResumeMusicAsync()
        {
            try
            {
                var audio = GetAudioSource();
                if (audio != null && audio.clip != null)
                {
                    SoundSystem.Instance.PlayMusic();
                    StartPlaybackMonitorIfNeeded();
                    return;
                }

                CancelPlaybackMonitor();

                if (playlist != null && playlist.Count > 0)
                {
                    playlistPosition = Mathf.Clamp(playlistPosition, 0, playlist.Count - 1);
                    await PlayPlaylistAtAsync(playlistPosition);
                }
                else
                {
                    await PlayMusicClipAsync();
                }
            }
            catch (System.Exception ex)
            {
                Log.Error("[PlayMusic] ResumeMusic 异常: {0}", ex);
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
            // 立即应用到当前AudioSource
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
        /// 获取 SoundSystem 的音乐 AudioSource（通过公共属性）
        /// </summary>
        /// <returns></returns>
        private AudioSource GetAudioSource()
        {
            return SoundSystem.Instance.MusicAudioSource;
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

            // 启动 UniTask 监视播放结束（零 GC，事件驱动）
            MonitorPlaybackAsync(audio, token).Forget();
        }

        private async UniTaskVoid MonitorPlaybackAsync(AudioSource audio, CancellationToken token)
        {
            // 等待直到播放开始
            await UniTask.Yield(PlayerLoopTiming.Update);

            // 等待直到播放停止（零 GC，事件驱动，不忙等）
            await UniTask.WaitUntil(() => audio == null || audio.clip == null || !audio.isPlaying,
                PlayerLoopTiming.Update, token);

            if (token.IsCancellationRequested) return;

            // 曲目结束，播放下一曲
            if (autoPlayNext)
            {
                // 延迟一帧以确保SoundSystem内部状态更新
                await UniTask.Yield(PlayerLoopTiming.Update);
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
                catch (System.Exception ex) { Log.Warning("PlayMusic 取消监控令牌异常: {0}", ex.Message); }
                playbackMonitorCts = null;
            }
        }
    }
}