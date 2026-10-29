using Astray.Common;
using Astray.Framework.Runtime.Base;
using Astray.Framework.Runtime.Resources;
using Astray.Framework.Runtime.Scene;
using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.Rendering;

namespace Astray.Framework.Runtime.Sound
{
    public partial class SoundSystem
    {
        /// <summary>
        /// 设置音乐属性（供 GameOption 直接调用，避免反射）
        /// </summary>
        public void SetMusicProperties(float volume, bool muted)
        {
            EnsureAudioSource();
            if (source != null)
            {
                source.volume = volume;
                source.mute = muted;
            }
        }

        /// <summary>
        /// 设置音效属性（批量更新所有活跃音效）
        /// </summary>
        public void SetSfxProperties(float volume, bool muted)
        {
            if (sfxObjects.Count == 0) return;
            // 使用可复用列表避免每次分配
            var snapshot = GetSpawnedList();
            foreach (var kvp in sfxObjects)
            {
                snapshot.Add(kvp.Key);
            }
            foreach (var obj in snapshot)
            {
                if (obj != null)
                {
                    var item = obj.GetComponent<SoundItem>();
                    if (item != null)
                    {
                        item.SetProperties(volume, muted);
                    }
                }
            }
            snapshot.Clear();
        }

        /// <summary>
        /// 播放音乐
        /// </summary>
        /// <param name="index">音乐配置索引</param>
        /// <param name="volume">音量（null 表示使用默认音量）</param>
        public async UniTask PlayMusic(int index, float? volume = null)
        {
            if (soundConfigDict != null && soundConfigDict.TryGetValue(index, out SoundConfig soundConfig))
            {
                // 与 PlaySwitch 共用版本号：并发 PlayMusic/PlaySwitch 时最新调用优先，旧链 await 恢复后退出
                int version = ++musicSwitchVersion;
                currentMusicIndex = index;
                AudioClip audioClip = await GetAudioClipAsync(soundConfig.Path, soundConfig.Name);
                if (version != musicSwitchVersion) return; // 已被更新的 PlayMusic/PlaySwitch 取代
                if (audioClip != null)
                {
                    EnsureAudioSource();
                    if (source == null) return;

                    source.clip = audioClip;
                    source.volume = volume ?? GameOption.CurrentOption.musicVolume;
                    source.loop = true;
                    source.mute = GameOption.CurrentOption.musicMuted;
                    source.Play();
                }
            }
        }

        /// <summary>
        /// 播放BGM
        /// </summary>
        public void PlayMusic()
        {
            EnsureAudioSource();
            source?.Play();
        }

        /// <summary>
        /// 暂停BGM
        /// </summary>
        public void PauseMusic()
        {
            EnsureAudioSource();
            source?.Pause();
        }

        /// <summary>
        /// 结束背景音乐
        /// </summary>
        public void StopMusic()
        {
            EnsureAudioSource();
            source?.Stop();
        }

        /// <summary>
        /// 音乐切换-带渐入渐出效果（优化版：新曲加载与淡出并行，减少等待时间）
        /// </summary>
        /// <param name="index"></param>
        // 音乐切换版本号：最新调用优先，旧链在每次 await 恢复后校验版本并退出，
        // 避免并发 PlaySwitch 互相改写共享 fadeTcs/fadeState 导致淡入淡出被截断
        private int musicSwitchVersion;

        public async UniTask PlaySwitch(int index)
        {
            if (soundConfigDict == null || !soundConfigDict.TryGetValue(index, out SoundConfig soundConfig))
                return;

            int version = ++musicSwitchVersion;

            // 启动淡出（不等待），同时并行加载新曲目
            var fadeOutTask = FadeOut();
            AudioClip newClip = await GetAudioClipAsync(soundConfig.Path, soundConfig.Name);
            if (version != musicSwitchVersion) return; // 已被更新的 PlaySwitch 取代

            // 等待淡出完成
            await fadeOutTask;
            if (version != musicSwitchVersion) return;

            // 切换曲目并淡入
            EnsureAudioSource();
            if (source != null && newClip != null)
            {
                source.clip = newClip;
                source.volume = 0f;
                source.loop = true;
                source.mute = GameOption.CurrentOption.musicMuted;
                source.Play();
                currentMusicIndex = index;
            }
            if (version != musicSwitchVersion) return;
            await FadeIn();
        }

        /// <summary>
        /// 渐入（由 Update 驱动，避免每帧 async 状态机分配）
        /// </summary>
        private UniTask FadeIn()
        {
            EnsureAudioSource();
            if (source == null) return UniTask.CompletedTask;

            // 如果上一次淡入淡出尚未完成，先完成旧的 TCS 防止泄漏
            fadeTcs?.TrySetResult(false);

            fadeStartVolume = 0f;
            fadeTargetVolume = GameOption.CurrentOption.musicVolume;
            fadeTimer = 0f;
            fadeState = FadeState.FadingIn;
            fadeTcs = new UniTaskCompletionSource<bool>();
            return fadeTcs.Task;
        }

        /// <summary>
        /// 渐出（由 Update 驱动，配合 UniTaskCompletionSource 实现零 GC）
        /// </summary>
        private UniTask FadeOut()
        {
            EnsureAudioSource();
            if (source == null) return UniTask.CompletedTask;

            // 如果上一次淡入淡出尚未完成，先完成旧的 TCS 防止泄漏
            fadeTcs?.TrySetResult(false);

            fadeStartVolume = source.volume;
            fadeTargetVolume = 0f;
            fadeTimer = 0f;
            fadeState = FadeState.FadingOut;
            fadeTcs = new UniTaskCompletionSource<bool>();
            return fadeTcs.Task;
        }

    }
}
