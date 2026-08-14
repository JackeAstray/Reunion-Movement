using ReunionMovement.Common;
using System;
using UnityEngine;
using ReunionMovement.Core.Sound;

namespace ReunionMovement.Core
{
    public static class GameOption
    {
        [Serializable]
        public class Option
        {
            // 版本号
            public string version = "1.0.0";
            // 全屏模式
            public bool fullscreen = true;
            // 分辨率宽度
            public int resolutionWidth = 1920;
            // 分辨率高度
            public int resolutionHeight = 1080;
            // 垂直同步
            public bool vsync = true;
            // 帧率
            public int framerate = 60;
            // 多语言支持
            public Multilingual language = Multilingual.ZH_CN;
            // 图形质量
            public int graphicsQuality = 2; // 0: 低, 1: 中, 2: 高
            // 亮度
            public float brightness = 1.0f;

            #region 声音
            // 自动暂停（默认关闭以避免加载时静音）
            public bool autoPause = false;
            // 主音量设置（默认不静音）
            public bool masterVolumeMuted = false;
            // 主音量
            public float masterVolume = 0.8f;
            // 音乐设置
            public bool musicMuted = false;
            // 音乐音量
            public float musicVolume = 0.5f;
            // 音效设置
            public bool sfxMuted = false;
            // 音效音量
            public float sfxVolume = 0.5f;
            //淡入淡出时间
            public float musicFadeTime = 2f;
            #endregion

            #region UI 输入
            // 键盘导航 —— 上
            public string uiNavUp = "w";
            // 键盘导航 —— 下
            public string uiNavDown = "s";
            // 键盘导航 —— 左
            public string uiNavLeft = "a";
            // 键盘导航 —— 右
            public string uiNavRight = "d";
            // 键盘提交/确认
            public string uiSubmit = "enter";
            // 键盘取消/返回
            public string uiCancel = "escape";
            // 切换到 UI 控制模式
            public string uiToggleToUI = "tab";
            // 退出 UI 控制模式
            public string uiToggleToGameplay = "escape";
            #endregion
        }

        /// <summary>当前选项（私有：外部只能读取引用、修改字段值，无法整体替换，防止状态被意外重置）</summary>
        private static Option currentOption = new Option();

        /// <summary>
        /// 当前选项（只读访问器）。返回的 Option 实例字段仍可读写，
        /// 但外部代码无法替换整个实例。
        /// </summary>
        public static Option CurrentOption => currentOption;

        private static bool isLoaded = false;

        /// <summary>
        /// 加载游戏选项从 PlayerPrefs（默认仅首次加载，后续从内存读取）。
        /// 读取 JSON 格式存档；如不存在或反序列化失败则使用默认选项。
        /// </summary>
        /// <param name="forceReload">强制重新从 PlayerPrefs 读取（例如恢复默认后重新加载）</param>
        public static void LoadOptions(bool forceReload = false)
        {
            if (isLoaded && !forceReload) return;
            isLoaded = true;

            const string jsonKey = "game_options_json";
            if (PlayerPrefs.HasKey(jsonKey))
            {
                var json = PlayerPrefs.GetString(jsonKey);
                try
                {
                    var loaded = JsonUtility.FromJson<Option>(json);
                    if (loaded != null)
                    {
                        currentOption = loaded;
                        ApplyOptions();
                        return;
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning("JSON 反序列化 GameOption 失败，使用默认选项: {0}", ex.Message);
                }
            }

            // 无存档或反序列化失败，使用默认选项
            currentOption = new Option();
            ApplyOptions();
        }

        /// <summary>
        /// 保存游戏选项到 PlayerPrefs（JSON 格式，单次写入）
        /// </summary>
        public static void SaveOptions()
        {
#if UNITY_WEBGL
            // WebGL 上 LoadOptions 被跳过（PlayerPrefs 为异步 IndexedDB，同步读写不可靠），
            // 保存同样跳过，避免写入"永远读不回来"的设置造成行为不一致。
            return;
#else
            const string jsonKey = "game_options_json";
            try
            {
                var json = JsonUtility.ToJson(currentOption);
                PlayerPrefs.SetString(jsonKey, json);
                PlayerPrefs.Save();
            }
            catch (Exception ex)
            {
                Log.Error("保存 GameOption 失败: {0}", ex.Message);
            }
#endif
        }

        /// <summary>
        /// 将当前选项完整应用到游戏（分辨率、画质、音量等）——供加载/重置时全量应用。
        /// 运行中单字段变更请使用 <see cref="ApplyOption{T}"/>（按字段类别走轻/重路径）。
        /// </summary>
        public static void ApplyOptions()
        {
            ApplyDisplayOptions();
            ApplyLightOptions();
        }

        /// <summary>
        /// 应用分辨率/画质等“重路径”设置。
        /// Screen.SetResolution 会切换显示模式（移动端闪屏/卡顿），仅在相关字段变化时调用。
        /// </summary>
        private static void ApplyDisplayOptions()
        {
            try
            {
#if UNITY_WEBGL
                // WebGL 平台限制：
                // - Screen.SetResolution   → 不支持（分辨率由浏览器控制）
                // - QualitySettings.vSyncCount → 不支持（垂直同步由浏览器控制）
                // - Application.targetFrameRate → 无效（帧率由 requestAnimationFrame 控制）
                // - Screen.fullScreen → 需要用户手势触发，不能代码强制
                // 因此跳过分辨率/全屏/垂直同步相关设置
#else
                // 分辨率与全屏
                Screen.SetResolution(currentOption.resolutionWidth, currentOption.resolutionHeight, currentOption.fullscreen);

                // 垂直同步
                QualitySettings.vSyncCount = currentOption.vsync ? 1 : 0;

                // 目标帧率
                Application.targetFrameRate = currentOption.framerate;
#endif

                // 图形质量
                int qualityIndex = Mathf.Clamp(currentOption.graphicsQuality, 0, QualitySettings.names.Length - 1);
                QualitySettings.SetQualityLevel(qualityIndex, true);
            }
            catch (Exception ex)
            {
                Log.Error("ApplyDisplayOptions 异常: {0}", ex);
            }
        }

        /// <summary>
        /// 应用音量/亮度等“轻路径”设置（无闪屏副作用，可随任意选项变更调用）。
        /// </summary>
        private static void ApplyLightOptions()
        {
            try
            {
                // 主音量（使用 AudioListener 作为全局主音量）
                AudioListener.volume = currentOption.masterVolumeMuted ? 0f : currentOption.masterVolume;

                // 自动暂停：仅同步当前暂停状态（前台不静音），
                // 实际的"切后台暂停/回前台恢复"由 GameEngine.OnAppPause 驱动（见 GameEngineDriver.OnApplicationPause）。
                // 修复：原先此处直接 AudioListener.pause = autoPause，开启选项即永久静音（语义错误）。
                AudioListener.pause = GameEngine.IsApplicationPaused && currentOption.autoPause;

                // 应用音乐和音效设置到 SoundSystem（如果已初始化）
                var ss = SoundSystem.Instance;
                if (ss != null)
                {
                    // 将淡入淡出时间同步
                    try { ss.fadeDuration = currentOption.musicFadeTime; }
                    catch (Exception ex) { Log.Warning("同步淡入淡出时间失败: {0}", ex.Message); }

                    // 使用公共方法设置音乐属性（替代反射）
                    ss.SetMusicProperties(currentOption.musicVolume, currentOption.musicMuted);
                    ss.SetSfxProperties(currentOption.sfxVolume, currentOption.sfxMuted);
                }

                // 其它可扩展的应用（亮度等）：尝试设置全局 shader 属性 以便 shader 使用
                Shader.SetGlobalFloat("_GameBrightness", currentOption.brightness);
            }
            catch (Exception ex)
            {
                Log.Error("ApplyLightOptions 异常: {0}", ex);
            }
        }

        /// <summary>字段是否为需要走“重路径”的显示相关设置（变化时才会 SetResolution/SetQualityLevel）</summary>
        private static bool IsHeavyDisplayField(string fieldName)
        {
            switch (fieldName)
            {
                case "resolutionWidth":
                case "resolutionHeight":
                case "fullscreen":
                case "vsync":
                case "framerate":
                case "graphicsQuality":
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// 获取单个选项
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="key"></param>
        /// <param name="defaultValue"></param>
        /// <returns></returns>
        /// <exception cref="NotSupportedException"></exception>
        public static T GetOption<T>(string key, T defaultValue)
        {
            return defaultValue switch
            {
                bool b => (T)(object)(PlayerPrefs.GetInt(key, b ? 1 : 0) == 1),
                int i => (T)(object)PlayerPrefs.GetInt(key, i),
                float f => (T)(object)PlayerPrefs.GetFloat(key, f),
                string s => (T)(object)PlayerPrefs.GetString(key, s),
                // 枚举：按字符串持久化（与 SetOption 的 Enum 分支配对）
                // 存档被篡改/损坏时 TryParse 兜底返回默认值，避免崩溃
                _ when typeof(T).IsEnum => Enum.TryParse(typeof(T), PlayerPrefs.GetString(key, defaultValue.ToString()), ignoreCase: true, out var enumValue)
                    ? (T)enumValue
                    : defaultValue,
                _ => throw new NotSupportedException($"不支持的类型: {typeof(T)}")
            };
        }

        /// <summary>
        /// 设置单个选项
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <exception cref="NotSupportedException"></exception>
        public static void SetOption<T>(string key, T value)
        {
            switch (value)
            {
                case bool b: PlayerPrefs.SetInt(key, b ? 1 : 0); break;
                case int i: PlayerPrefs.SetInt(key, i); break;
                case float f: PlayerPrefs.SetFloat(key, f); break;
                case string s: PlayerPrefs.SetString(key, s); break;
                // 枚举按字符串持久化，避免抛 NotSupportedException 导致整批设置丢失
                case Enum e: PlayerPrefs.SetString(key, e.ToString()); break;
                default: throw new NotSupportedException($"不支持的类型: {typeof(T)}");
            }
        }

        /// <summary>
        /// 设置单个选项并立即应用
        /// </summary>
        public static void ApplyOption<T>(string key, T value)
        {
            // 用反射按字段名更新 currentOption（字段名与 key 约定一致，见 Option 定义）。
            // 消除了原先硬编码的 20+ 字符串 case，新增设置项无需再改 ApplyOption。
            try
            {
                var field = typeof(Option).GetField(key,
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                object converted = null;
                if (field != null)
                {
                    try
                    {
                        if (field.FieldType.IsEnum)
                        {
                            // 枚举字段：支持 string / 同类型枚举 / 数值三种来源
                            if (value is string str) converted = Enum.Parse(field.FieldType, str, ignoreCase: true);
                            else if (value is Enum) converted = value;
                            else converted = Enum.ToObject(field.FieldType, Convert.ToInt32(value));
                        }
                        else
                        {
                            converted = Convert.ChangeType(value, field.FieldType, System.Globalization.CultureInfo.InvariantCulture);
                        }
                        field.SetValue(currentOption, converted);
                    }
                    catch (Exception ex)
                    {
                        Log.Warning("ApplyOption 字段 {0} 赋值失败: {1}", key, ex.Message);
                    }
                }
                else
                {
                    Log.Warning("ApplyOption 未知字段: {0}", key);
                }

                // 持久化：使用按字段类型转换后的值（而非 value 的运行时类型）。
                // 修复：枚举字段传 int 时若按 int 持久化，GetOption 的枚举分支按字符串读取会失败，
                // 表现为“设置生效但不持久”；bool 字段传字符串同理。
                if (converted != null)
                {
                    if (converted is bool b) SetOption(key, b);
                    else if (converted is int i) SetOption(key, i);
                    else if (converted is float f) SetOption(key, f);
                    else if (converted is string s) SetOption(key, s);
                    else if (converted is Enum e) SetOption(key, e);
                }
                else
                {
                    // 字段不存在时按原始值持久化（保持旧行为）
                    SetOption(key, value);
                }
                PlayerPrefs.Save();

                // 仅显示相关字段变化才执行重路径（SetResolution + SetQualityLevel），
                // 调音量不再闪屏/切换全屏（移动端卡顿、窗口状态破坏）
                if (field != null && IsHeavyDisplayField(field.Name))
                {
                    ApplyDisplayOptions();
                }
                else
                {
                    ApplyLightOptions();
                }
            }
            catch (Exception ex)
            {
                Log.Error("ApplyOption 异常: {0}", ex);
            }
        }

        /// <summary>
        /// 重置游戏选项为默认值并保存/应用。
        /// 编辑器模式下可通过菜单调用：Tools/ReunionMovement/Reset Game Options
        /// </summary>
        public static void ResetOptions()
        {
            try
            {
                currentOption = new Option();
                SaveOptions();
                ApplyOptions();
            }
            catch (Exception ex)
            {
                Log.Error("ResetOptions 异常: {0}", ex);
            }
        }
    }
}