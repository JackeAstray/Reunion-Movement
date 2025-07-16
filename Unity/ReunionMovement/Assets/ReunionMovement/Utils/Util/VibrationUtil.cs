using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace ReunionMovement.Common.Utils
{
    /// <summary>
    /// iOS Android 震动
    /// </summary>
    public class VibrationUtil
    {
        public const string VIBRATION_PREFS = "VibrationStatus";

        // 单例实现
        private static readonly VibrationUtil instance = new VibrationUtil();
        public static VibrationUtil Instance => instance;


        //private VibrationUtil()
        //{
        //    iOSInitializeHaptics();
        //}

        // 震动参数
        private const long LightDuration = 20;
        private const long MediumDuration = 40;
        private const long HeavyDuration = 80;
        private const int LightAmplitude = 40;
        private const int MediumAmplitude = 120;
        private const int HeavyAmplitude = 255;

        private static readonly long[] successPattern = { 0, LightDuration, LightDuration, HeavyDuration };
        private static readonly int[] successPatternAmplitude = { 0, LightAmplitude, 0, HeavyAmplitude };
        private static readonly long[] warningPattern = { 0, HeavyDuration, LightDuration, MediumDuration };
        private static readonly int[] warningPatternAmplitude = { 0, HeavyAmplitude, 0, MediumAmplitude };
        private static readonly long[] failurePattern = { 0, MediumDuration, LightDuration, MediumDuration, LightDuration, HeavyDuration, LightDuration, LightDuration };
        private static readonly int[] failurePatternAmplitude = { 0, MediumAmplitude, 0, MediumAmplitude, 0, HeavyAmplitude, 0, LightAmplitude };


        private static int sdkVersion = -1;
        private static bool iOSHapticsInitialized = false;

        private VibrationUtil()
        {
            if (IsIOS()) iOSInitializeHaptics();
        }

        private bool IsVibrationDisabled() => PlayerPrefs.GetInt(VIBRATION_PREFS, 2) == 1;

        // 统一触发接口
        public void TriggerDefault() => Trigger(() =>
        {
#if UNITY_IOS || UNITY_ANDROID
            Handheld.Vibrate();
#endif
        });

        // 触发默认的 Vibrate 方法，这将导致 Android 上产生中等振动，iOS 上产生中等影响
        public void TriggerVibrate() => Trigger(() => Vibrate());
        // 触发选择触觉反馈，Android 上轻微振动，iOS 上轻微冲击
        public void TriggerSelection() => Trigger(() => Haptic(HapticTypes.Selection));
        // 触发成功触觉反馈，Android 上先轻后重的振动，iOS 上成功影响
        public void TriggerSuccess() => Trigger(() => Haptic(HapticTypes.Success));
        // 触发警告触觉反馈，Android 上会出现先重后中的振动，iOS 上会产生警告影响
        public void TriggerWarning() => Trigger(() => Haptic(HapticTypes.Warning));
        // 触发故障触觉反馈，Android 上为中/重/重/轻振动模式，iOS 上为故障影响
        public void TriggerFailure() => Trigger(() => Haptic(HapticTypes.Failure));
        // 在 iOS 上触发轻微冲击，在 Android 上触发短暂且轻微的振动
        public void TriggerLightImpact() => Trigger(() => Haptic(HapticTypes.LightImpact));
        // 在 iOS 上触发中等影响，在 Android 上触发中等且有规律的振动
        public void TriggerMediumImpact() => Trigger(() => Haptic(HapticTypes.MediumImpact));
        // 对 iOS 造成严重冲击，对 Android 造成长时间剧烈震动
        public void TriggerHeavyImpact() => Trigger(() => Haptic(HapticTypes.HeavyImpact));

        private void Trigger(System.Action action)
        {
            if (!IsVibrationDisabled()) action();
        }

        /// <summary>
        /// 停止当前平台的所有震动
        /// </summary>
        public static void CancelVibrations()
        {
            if (IsAndroid())
            {
                AndroidCancelVibrations();
            }
            else if (IsIOS())
            {
                // iOS 不支持取消已触发的震动或触觉反馈
                // ReleaseFeedbackGenerators(); // 仅释放生成器资源，不会中断反馈
            }
        }

        /// <summary>
        /// 如果当前平台是 Android，则返回 true，否则返回 false。
        /// </summary>
        /// <returns></returns>
        private static bool IsAndroid()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
			return true;
#else
            return false;
#endif
        }

        /// <summary>
        /// 如果当前平台是 iOS，则返回 true，否则返回 false
        /// </summary>
        /// <returns></returns>
        private static bool IsIOS()
        {
#if UNITY_IOS && !UNITY_EDITOR
			return true;
#else
            return false;
#endif
        }

        /// <summary>
        /// 触发简单的振动
        /// </summary>
        private static void Vibrate()
        {
            if (IsAndroid())
            {
                AndroidVibrate(MediumDuration);
            }
            else if (IsIOS())
            {
                iOSTriggerHaptics(HapticTypes.MediumImpact);
            }
        }

        /// <summary>
        /// 触发指定类型的触觉反馈
        /// </summary>
        /// <param name="type"></param>
        private static void Haptic(HapticTypes type)
        {
            if (IsAndroid())
            {
                switch (type)
                {
                    case HapticTypes.Selection:
                        AndroidVibrate(LightDuration, LightAmplitude);
                        break;

                    case HapticTypes.Success:
                        AndroidVibrate(successPattern, successPatternAmplitude, -1);
                        break;

                    case HapticTypes.Warning:
                        AndroidVibrate(warningPattern, warningPatternAmplitude, -1);
                        break;

                    case HapticTypes.Failure:
                        AndroidVibrate(failurePattern, failurePatternAmplitude, -1);
                        break;

                    case HapticTypes.LightImpact:
                        AndroidVibrate(LightDuration, LightAmplitude);
                        break;

                    case HapticTypes.MediumImpact:
                        AndroidVibrate(MediumDuration, MediumAmplitude);
                        break;

                    case HapticTypes.HeavyImpact:
                        AndroidVibrate(HeavyDuration, HeavyAmplitude);
                        break;
                }
            }
            else if (IsIOS())
            {
                iOSTriggerHaptics(type);
            }
        }
        // INTERFACE END ---------------------------------------------------------------------------------------------------------

        // Android ---------------------------------------------------------------------------------------------------------
#if UNITY_ANDROID && !UNITY_EDITOR
		private static AndroidJavaClass UnityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
		private static AndroidJavaObject CurrentActivity = UnityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
		private static AndroidJavaObject AndroidVibrator = CurrentActivity.Call<AndroidJavaObject>("getSystemService", "vibrator");
		private static AndroidJavaClass VibrationEffectClass;
		private static AndroidJavaObject VibrationEffect;
		private static int DefaultAmplitude;
#else
        private static AndroidJavaClass UnityPlayer;
        private static AndroidJavaObject CurrentActivity;
        private static AndroidJavaObject AndroidVibrator = null;
        private static AndroidJavaClass VibrationEffectClass = null;
        private static AndroidJavaObject VibrationEffect;
        private static int DefaultAmplitude;
#endif

        /// <summary>
        /// 请求 Android 上的默认振动持续指定的持续时间（以毫秒为单位）
        /// </summary>
        /// <param name="milliseconds"></param>
        private static void AndroidVibrate(long milliseconds)
        {
            if (!IsAndroid())
            {
                return;
            }

            AndroidVibrator.Call("vibrate", milliseconds);
        }

        /// <summary>
        /// 请求指定幅度和持续时间的振动。 如果设备的 SDK 不支持振幅，则会请求默认振动
        /// </summary>
        /// <param name="milliseconds"></param>
        /// <param name="amplitude"></param>
        private static void AndroidVibrate(long milliseconds, int amplitude)
        {
            if (!IsAndroid())
            {
                return;
            }

            // 仅支持振幅
            if ((AndroidSDKVersion() < 26))
            {
                AndroidVibrate(milliseconds);
            }
            else
            {
                VibrationEffectClassInitialization();
                VibrationEffect = VibrationEffectClass.CallStatic<AndroidJavaObject>("createOneShot", new object[] { milliseconds, amplitude });
                AndroidVibrator.Call("vibrate", VibrationEffect);
            }
        }

        /// <summary>
        /// 在 Android 上请求振动以指定模式和可选重复
        /// </summary>
        /// <param name="pattern"></param>
        /// <param name="repeat"></param>
        private static void AndroidVibrate(long[] pattern, int repeat)
        {
            if (!IsAndroid())
            {
                return;
            }

            if ((AndroidSDKVersion() < 26))
            {
                AndroidVibrator.Call("vibrate", pattern, repeat);
            }
            else
            {
                VibrationEffectClassInitialization();
                VibrationEffect = VibrationEffectClass.CallStatic<AndroidJavaObject>("createWaveform", new object[] { pattern, repeat });
                AndroidVibrator.Call("vibrate", VibrationEffect);
            }
        }

        /// <summary>
        /// 在 Android 上请求振动以指定模式、幅度和可选重复
        /// </summary>
        /// <param name="pattern"></param>
        /// <param name="amplitudes"></param>
        /// <param name="repeat"></param>
        private static void AndroidVibrate(long[] pattern, int[] amplitudes, int repeat)
        {
            if (!IsAndroid())
            {
                return;
            }

            if ((AndroidSDKVersion() < 26))
            {
                AndroidVibrator.Call("vibrate", pattern, repeat);
            }
            else
            {
                VibrationEffectClassInitialization();
                VibrationEffect = VibrationEffectClass.CallStatic<AndroidJavaObject>("createWaveform", new object[] { pattern, amplitudes, repeat });
                AndroidVibrator.Call("vibrate", VibrationEffect);
            }
        }

        /// <summary>
        /// 停止所有可能处于活动状态的 Android 振动
        /// </summary>
        private static void AndroidCancelVibrations()
        {
            if (!IsAndroid())
            {
                return;
            }

            AndroidVibrator.Call("cancel");
        }

        /// <summary>
        /// 如果需要，初始化 VibrationEffectClass。
        /// </summary>
        private static void VibrationEffectClassInitialization()
        {
            if (VibrationEffectClass == null)
            {
                VibrationEffectClass = new AndroidJavaClass("android.os.VibrationEffect");
            }
        }

        /// <summary>
        /// 以 int 形式返回当前 Android SDK 版本
        /// </summary>
        /// <returns></returns>
        private static int AndroidSDKVersion()
        {
            if (sdkVersion == -1)
            {
                int apiLevel = int.Parse(SystemInfo.operatingSystem.Substring(SystemInfo.operatingSystem.IndexOf("-") + 1, 3));
                sdkVersion = apiLevel;
                return apiLevel;
            }
            else
            {
                return sdkVersion;
            }
        }
        // Android End ---------------------------------------------------------------------------------------------------------

        // iOS ----------------------------------------------------------------------------------------------------------------
#if UNITY_IOS && !UNITY_EDITOR
        [DllImport ("__Internal")] private static extern void InstantiateFeedbackGenerators();
        [DllImport ("__Internal")] private static extern void ReleaseFeedbackGenerators();
        [DllImport ("__Internal")] private static extern void SelectionHaptic();
        [DllImport ("__Internal")] private static extern void SuccessHaptic();
        [DllImport ("__Internal")] private static extern void WarningHaptic();
        [DllImport ("__Internal")] private static extern void FailureHaptic();
        [DllImport ("__Internal")] private static extern void LightImpactHaptic();
        [DllImport ("__Internal")] private static extern void MediumImpactHaptic();
        [DllImport ("__Internal")] private static extern void HeavyImpactHaptic();
#else
        private static void InstantiateFeedbackGenerators() { }
        private static void ReleaseFeedbackGenerators() { }
        private static void SelectionHaptic() { }
        private static void SuccessHaptic() { }
        private static void WarningHaptic() { }
        private static void FailureHaptic() { }
        private static void LightImpactHaptic() { }
        private static void MediumImpactHaptic() { }
        private static void HeavyImpactHaptic() { }
#endif

        /// <summary>
        /// 调用此方法来初始化触觉。 如果您忘记执行此操作，Nice Vibrations 会在您第一次调用 iOSTriggerHaptics 时为您执行此操作。
        /// </summary>
        private static void iOSInitializeHaptics()
        {
            if (!IsIOS())
            {
                return;
            }

            InstantiateFeedbackGenerators();
            iOSHapticsInitialized = true;
        }

        /// <summary>
        /// 调用此方法来释放触觉。 如果您忘记执行此操作，Nice Vibrations 会在应用程序退出时为您执行此操作。
        /// </summary>
        private static void iOSReleaseHaptics()
        {
            if (!IsIOS())
            {
                return;
            }
            ReleaseFeedbackGenerators();
        }

        /// <summary>
        /// 这种方法根据不支持触觉的设备列表测试当前设备生成，如果支持触觉，则返回true，否则返回false。
        /// </summary>
        /// <returns></returns>
        public static bool HapticsSupported()
        {
            bool hapticsSupported = false;
#if UNITY_IOS
            switch (generation)
            {
                case DeviceGeneration.iPhone3G:
                case DeviceGeneration.iPhone3GS:
                case DeviceGeneration.iPodTouch1Gen:
                case DeviceGeneration.iPodTouch2Gen:
                case DeviceGeneration.iPodTouch3Gen:
                case DeviceGeneration.iPodTouch4Gen:
                case DeviceGeneration.iPhone4:
                case DeviceGeneration.iPhone4S:
                case DeviceGeneration.iPhone5:
                case DeviceGeneration.iPhone5C:
                case DeviceGeneration.iPhone5S:
                case DeviceGeneration.iPhone6:
                case DeviceGeneration.iPhone6Plus:
                case DeviceGeneration.iPhone6S:
                case DeviceGeneration.iPhone6SPlus:
                    hapticsSupported = false; break;
                default:
                    hapticsSupported = true; break;
            }
#endif
            return hapticsSupported;
        }

        /// 仅限iOS：触发指定类型的触觉反馈
        private static void iOSTriggerHaptics(HapticTypes type)
        {
            if (!IsIOS())
            {
                return;
            }

            if (!iOSHapticsInitialized)
            {
                iOSInitializeHaptics();
            }

            if (!HapticsSupported())
            {
                return;
            }

            switch (type)
            {
                case HapticTypes.Selection: SelectionHaptic(); break;
                case HapticTypes.Success: SuccessHaptic(); break;
                case HapticTypes.Warning: WarningHaptic(); break;
                case HapticTypes.Failure: FailureHaptic(); break;
                case HapticTypes.LightImpact: LightImpactHaptic(); break;
                case HapticTypes.MediumImpact: MediumImpactHaptic(); break;
                case HapticTypes.HeavyImpact: HeavyImpactHaptic(); break;
            }
        }
        // iOS End ----------------------------------------------------------------------------------------------------------------
    }
}
