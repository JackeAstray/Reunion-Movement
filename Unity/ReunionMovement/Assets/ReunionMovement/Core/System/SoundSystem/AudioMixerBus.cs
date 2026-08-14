using UnityEngine;
using UnityEngine.Audio;

namespace ReunionMovement.Core.Sound
{
    /// <summary>
    /// AudioMixer 音量分组工具 —— 代码侧接口层。
    ///
    /// 接入步骤（资产层，需音频美术配合）：
    ///   1. 创建 AudioMixer 资产（.mixer），为 BGM/SFX/UI/Voice 各建分组（Group）与输出链路；
    ///   2. 在 Mixer 中对各分组"暴露参数"（Exposed Parameters），参数名与下方常量对应；
    ///   3. 将 mixer 引用配置到调用方（如 SoundSystem 序列化字段），
    ///      用 SetBusVolume 按组控制音量（0~1 线性，内部转 dB）；
    ///   4. 需要 Ducking（BGM 压低）等效果时在 Mixer 中配置 Snapshot，用 SwitchSnapshot 切换。
    ///
    /// 未配置 Mixer 时本类所有方法安全 no-op / 返回 false，不影响现有 SoundSystem 行为。
    /// 说明：接入 Mixer 后应让 AudioSource.outputAudioMixerGroup 路由到对应分组，
    /// 总线音量（Mixer 参数）作为最终控制，source.volume 作为相对音量，两者叠加。
    /// </summary>
    public static class AudioMixerBus
    {
        /// <summary>建议的总线参数名（与 Mixer 暴露参数对应，可按项目自定义）</summary>
        public const string MusicVolumeParam = "MusicVolume";
        public const string SfxVolumeParam = "SfxVolume";
        public const string UiVolumeParam = "UIVolume";
        public const string VoiceVolumeParam = "VoiceVolume";

        /// <summary>静音阈值 dB（低于此按静音处理，避免 Log10(0)）</summary>
        private const float SilenceDb = -80f;

        /// <summary>线性音量(0~1) 转 dB（-80dB 视为静音）</summary>
        public static float LinearToDb(float linear)
        {
            linear = Mathf.Clamp01(linear);
            if (linear <= 0.0001f) return SilenceDb;
            return Mathf.Clamp(20f * Mathf.Log10(linear), SilenceDb, 0f);
        }

        /// <summary>dB 转线性音量(0~1)</summary>
        public static float DbToLinear(float db)
        {
            if (db <= SilenceDb) return 0f;
            return Mathf.Clamp01(Mathf.Pow(10f, db / 20f));
        }

        /// <summary>设置总线线性音量（0~1；mixer 为 null 时 no-op 返回 false）</summary>
        public static bool SetBusVolume(AudioMixer mixer, string paramName, float linearVolume)
        {
            if (mixer == null) return false;
            return mixer.SetFloat(paramName, LinearToDb(linearVolume));
        }

        /// <summary>获取总线线性音量（mixer 为 null 或参数不存在返回 null）</summary>
        public static float? GetBusVolume(AudioMixer mixer, string paramName)
        {
            if (mixer == null) return null;
            if (!mixer.GetFloat(paramName, out float db)) return null;
            return DbToLinear(db);
        }

        /// <summary>切换 Mixer Snapshot（Ducking 等效果），支持指定过渡时长（秒）</summary>
        public static void SwitchSnapshot(AudioMixer mixer, AudioMixerSnapshot snapshot, float transitionSeconds = 0.25f)
        {
            if (mixer == null || snapshot == null) return;
            snapshot.TransitionTo(Mathf.Max(0f, transitionSeconds));
        }
    }
}
