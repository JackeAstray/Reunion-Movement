using NUnit.Framework;
using ReunionMovement.Core;
using UnityEngine;

namespace ReunionMovement.Tests
{
    /// <summary>
    /// GameOption 应用与持久化 EditMode 测试。
    ///
    /// 回归保护：
    /// 1) LoadOptions 必须在所有平台都把选项应用一次 —— 此前 WebGL 分支直接 return，
    ///    导致 masterVolume / 亮度在 WebGL 上永远停留在引擎默认值（同一份设置 WebGL 比桌面响）。
    /// 2) 存档读写在所有平台都要生效 —— 此前 LoadOptions/SaveOptions 在 WebGL 被整段跳过，
    ///    音量/语言/画质在 WebGL 上无法跨会话保留，而抽卡保底/按键绑定却照存，策略不一致。
    ///    注：活动构建目标为 WebGL 时 Unity 会在编辑器下也定义 UNITY_WEBGL，因此本测试能真正
    ///    覆盖到那条分支，而不是只在桌面路径上打转。
    /// </summary>
    public class GameOptionTests
    {
        private const string JsonKey = "game_options_json";

        // 测试会写入真实 PlayerPrefs：先备份，TearDown 恢复，避免污染开发机的编辑器设置
        private bool hadOriginalJson;
        private string originalJson;

        [SetUp]
        public void SetUp()
        {
            hadOriginalJson = PlayerPrefs.HasKey(JsonKey);
            originalJson = hadOriginalJson ? PlayerPrefs.GetString(JsonKey) : null;
        }

        [TearDown]
        public void TearDown()
        {
            if (hadOriginalJson) PlayerPrefs.SetString(JsonKey, originalJson);
            else PlayerPrefs.DeleteKey(JsonKey);
            PlayerPrefs.Save();

            // 内存态也复位，避免污染同一测试会话内的后续用例
            GameOption.LoadOptions(forceReload: true);
        }

        [Test]
        public void LoadOptions_AppliesOptions()
        {
            GameOption.LoadOptions(forceReload: true);

            // 主音量：应用后应与当前选项一致（可能带静音标记，故按生产同款公式计算期望值）
            float expectedVolume = GameOption.CurrentOption.masterVolumeMuted
                ? 0f
                : GameOption.CurrentOption.masterVolume;
            Assert.AreEqual(expectedVolume, AudioListener.volume, 0.0001f,
                "LoadOptions 后 AudioListener.volume 应等于 GameOption 的主音量设置");

            // 亮度：通过全局 shader 属性下发，同样必须在 LoadOptions 后生效
            Assert.AreEqual(GameOption.CurrentOption.brightness, Shader.GetGlobalFloat("_GameBrightness"), 0.0001f,
                "LoadOptions 后 _GameBrightness 应等于 GameOption 的亮度设置");
        }

        [Test]
        public void SaveOptions_ThenReload_RoundTrips()
        {
            GameOption.SetMasterMuted(false);
            GameOption.SetMasterVolume(0.42f);

            // 从 PlayerPrefs 强制重读：若持久化在某个平台被跳过，这里会读回默认值
            GameOption.LoadOptions(forceReload: true);

            Assert.AreEqual(0.42f, GameOption.CurrentOption.masterVolume, 0.0001f,
                "写入的音量应能跨 LoadOptions 读回（存档未被跳过）");
            Assert.AreEqual(0.42f, AudioListener.volume, 0.0001f,
                "读档后主音量应再次应用到 AudioListener");
        }

        [Test]
        public void ResetOptions_RestoresDefaults_AndPersists()
        {
            GameOption.SetMasterVolume(0.42f);
            GameOption.ResetOptions();

            Assert.AreEqual(0.8f, GameOption.CurrentOption.masterVolume, 0.0001f,
                "ResetOptions 应恢复默认音量");

            GameOption.LoadOptions(forceReload: true);
            Assert.AreEqual(0.8f, GameOption.CurrentOption.masterVolume, 0.0001f,
                "恢复默认值后应已持久化（重读仍是默认值）");
        }
    }
}
