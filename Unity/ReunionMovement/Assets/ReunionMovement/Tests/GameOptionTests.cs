using NUnit.Framework;
using ReunionMovement.Core;
using UnityEngine;

namespace ReunionMovement.Tests
{
    /// <summary>
    /// GameOption 应用流程 EditMode 测试。
    ///
    /// 回归保护：LoadOptions 必须在所有平台都把选项应用一次 —— 此前 WebGL 分支直接 return，
    /// 导致 masterVolume / 亮度在 WebGL 上永远停留在引擎默认值（同一份设置 WebGL 比桌面响）。
    /// </summary>
    public class GameOptionTests
    {
        [Test]
        public void LoadOptions_AppliesOptions()
        {
            GameOption.LoadOptions(forceReload: true);

            // 主音量：应用后应与当前选项一致（WebGL 分支不读 PlayerPrefs，取默认 0.8；
            // 桌面分支读存档，可能带静音标记，故按生产同款公式计算期望值）
            float expectedVolume = GameOption.CurrentOption.masterVolumeMuted
                ? 0f
                : GameOption.CurrentOption.masterVolume;
            Assert.AreEqual(expectedVolume, AudioListener.volume, 0.0001f,
                "LoadOptions 后 AudioListener.volume 应等于 GameOption 的主音量设置");

            // 亮度：通过全局 shader 属性下发，同样必须在 LoadOptions 后生效
            Assert.AreEqual(GameOption.CurrentOption.brightness, Shader.GetGlobalFloat("_GameBrightness"), 0.0001f,
                "LoadOptions 后 _GameBrightness 应等于 GameOption 的亮度设置");
        }
    }
}
