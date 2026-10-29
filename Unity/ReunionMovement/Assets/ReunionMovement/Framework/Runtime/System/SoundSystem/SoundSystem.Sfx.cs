using ReunionMovement.Common;
using ReunionMovement.Framework.Runtime.Base;
using ReunionMovement.Framework.Runtime.Resources;
using ReunionMovement.Framework.Runtime.Scene;
using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.Rendering;

namespace ReunionMovement.Framework.Runtime.Sound
{
    public partial class SoundSystem
    {
        #region 播放声音
        /// <summary>
        /// 播放声音
        /// </summary>
        /// <param name="index">声音配置索引</param>
        /// <param name="emitter">声音发射器（3D 音效时建议传入，决定声源位置）</param>
        /// <param name="loop">是否循环</param>
        /// <param name="volume">音量（null 用全局设置）</param>
        /// <param name="pitch">音高</param>
        /// <param name="spatialBlend">3D 空间混合（0=纯 2D，1=纯 3D；>0 时未传 emitter 会在原点播放）</param>
        public async UniTask PlaySfx(int index, Transform emitter = null, bool loop = false, float? volume = null, float pitch = 1f, float spatialBlend = 0f)
        {
            try
            {
                if (soundConfigDict != null && soundConfigDict.TryGetValue(index, out SoundConfig soundConfig))
                {
                    // 同音效限流：在 await 之前判断，防止连点瞬间叠发多个加载协程与同声源实例
                    if (minSfxRetriggerInterval > 0f && !string.IsNullOrEmpty(soundConfig.Path))
                    {
                        float now = Time.unscaledTime;
                        if (lastSfxPlayTimes.TryGetValue(soundConfig.Path, out float last)
                            && now - last < minSfxRetriggerInterval)
                        {
                            return;
                        }
                        lastSfxPlayTimes[soundConfig.Path] = now;
                    }

                    AudioClip clip = await GetAudioClipAsync(soundConfig.Path, soundConfig.Name);
                    // await 恢复后复查：Clear() 可能已销毁 sfxRoot（代次校验已让 clip 为 null，
                    // 但 cachedSfxPrefab/sfxRoot 仍需检查，否则 Spawn 访问 sfxRoot.transform 抛 NRE）
                    if (clip != null && cachedSfxPrefab != null && sfxRoot != null && isInited)
                    {
                        GameObject go = Spawn(cachedSfxPrefab);
                        if (go != null)
                        {
                            if (emitter != null)
                            {
                                go.transform.SetParent(emitter);
                                go.transform.localPosition = Vector3.zero;
                            }
                            else if (spatialBlend > 0f)
                            {
                                // 3D 音效未指定发射器：提示并保持在世界原点播放
                                Log.Warning("PlaySfx: 索引 {0} 的 spatialBlend>0 但未传 emitter，将在世界原点播放 3D 音效", index);
                            }
                            SoundItem soundObj = go.GetComponent<SoundItem>();
                            if (soundObj != null)
                            {
                                float effectiveVolume = volume ?? GameOption.CurrentOption.sfxVolume;
                                soundObj.Processing(clip, emitter, loop, effectiveVolume, GameOption.CurrentOption.sfxMuted, pitch, spatialBlend);
                            }
                            else
                            {
                                Log.Warning("PlaySfx: SoundItem 组件缺失于 {0}", go.name);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error("PlaySfx 异常: {0}", ex.Message);
            }
        }

        /// <summary>
        /// 停止所有音效
        /// </summary>
        public void StopSfx()
        {
            RecycleAll();
        }
        #endregion

        /// <summary>
        /// 应用自适应音频设置：
        /// - 有线耳机/扬声器：使用较小 DSP buffer 以降低延迟
        /// - 蓝牙耳机：使用较大 DSP buffer，因为蓝牙 A2DP 自带 150-300ms 延迟，
        ///   过小的 buffer 反而导致音频欠载（卡顿/爆音），无法降低实际延迟
        /// </summary>
        private void ApplyLowLatencyAudioSettings()
        {
            try
            {
                var config = AudioSettings.GetConfiguration();

                // 判断是否使用蓝牙音频输出
                bool isBluetooth = IsBluetoothAudioActive();

#if UNITY_ANDROID || UNITY_IOS
                if (isBluetooth)
                {
                    // 蓝牙模式：使用较大缓冲确保音频流稳定
                    // 蓝牙瓶颈在传输层，DSP buffer 大小不影响实际听到的延迟
                    config.dspBufferSize = 1024;
                    Log.Debug("[AudioSettings] 检测到蓝牙音频，使用最佳性能模式 (Buffer: 1024)");
                }
                else
                {
                    // 有线/扬声器模式：使用适中缓冲
                    // 256 对很多安卓设备过于激进，512 在有线模式下延迟可接受
                    config.dspBufferSize = 512;
                    Log.Debug("[AudioSettings] 有线/扬声器模式 (Buffer: 512)");
                }
#else
                config.dspBufferSize = 512; // 桌面端平衡延迟与性能
#endif
                config.speakerMode = AudioSpeakerMode.Stereo;
                config.sampleRate = 0;
                config.numVirtualVoices = 32;
                config.numRealVoices = 16;

                if (AudioSettings.Reset(config))
                {
                    Log.Debug("[AudioSettings] 音频配置已应用 DSP Buffer: {0} samples, Bluetooth: {1}", config.dspBufferSize, isBluetooth);
                }
                else
                {
                    Log.Debug("[AudioSettings] 音频配置重置失败，使用系统默认");
                }
            }
            catch (System.Exception ex)
            {
                Log.Warning("[AudioSettings] 音频配置异常: {0}", ex.Message);
            }
        }

        /// <summary>
        /// 检测当前是否使用蓝牙音频输出（A2DP 或 SCO）
        /// </summary>
        private bool IsBluetoothAudioActive()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var audioManager = new AndroidJavaClass("android.media.AudioManager"))
                using (var context = new AndroidJavaClass("com.unity3d.player.UnityPlayer")
                    .GetStatic<AndroidJavaObject>("currentActivity"))
                {
                    var service = context.Call<AndroidJavaObject>("getSystemService", "audio");
                    if (service == null) return false;

                    bool isBluetoothA2dpOn = service.Call<bool>("isBluetoothA2dpOn");
                    bool isBluetoothScoOn = service.Call<bool>("isBluetoothScoOn");
                    return isBluetoothA2dpOn || isBluetoothScoOn;
                }
            }
            catch
            {
                // 检测失败时保守估计，使用较大 buffer
                return true;
            }
#elif UNITY_IOS && !UNITY_EDITOR
            // TODO: 通过原生插件调用 AVAudioSession.CurrentRoute 检测蓝牙端口类型
            // (AVAudioSessionPortBluetoothA2DP / BluetoothLE / BluetoothHFP)
            // 当前无法在 C# 层可靠检测 iOS 蓝牙，默认使用有线优化值 512。
            // 如需蓝牙稳定性，可在 Player Settings 中将 DSP Buffer 设为 Best Performance。
            try
            {
                // 检查当前音频输出类型（通过 AudioSettings 间接判断）
                var config = AudioSettings.GetConfiguration();
                // 如果用户手动将采样率设为较低值，可能是蓝牙设备
                return false; // 默认假设有线连接以获得更低延迟
            }
            catch
            {
                return false;
            }
#else
            return false;
#endif
        }

    }
}
