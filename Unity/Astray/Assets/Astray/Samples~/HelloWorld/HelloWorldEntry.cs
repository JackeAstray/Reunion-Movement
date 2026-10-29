using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Astray.Framework.Runtime;
using Astray.Framework.Runtime.Base;
using UnityEngine;
using UnityEngine.UI;

// 示例入口：只在导入 Sample 后存在。
// 如果宿主工程已经声明了自己的入口，非框架程序集的声明会优先，这个示例不会生效。
[assembly: AstrayGameEntry(typeof(Astray.Samples.HelloWorld.HelloWorldEntry))]

namespace Astray.Samples.HelloWorld
{
    /// <summary>
    /// 最小 GameEntry：不注册模块，直接在 OnGameStartAsync 里创建一个 uGUI 文本。
    /// </summary>
    public sealed class HelloWorldEntry : GameEntry
    {
        public override IList<ICustomSystem> CreateModules()
        {
            return new List<ICustomSystem>();
        }

        public override UniTask OnBeforeInitAsync()
        {
            return UniTask.CompletedTask;
        }

        public override UniTask OnGameStartAsync()
        {
            var canvasGo = new GameObject("HelloWorldCanvas", typeof(RectTransform));
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGo.AddComponent<CanvasScaler>();
            canvasGo.AddComponent<GraphicRaycaster>();

            var textGo = new GameObject("HelloWorldText", typeof(RectTransform));
            textGo.transform.SetParent(canvasGo.transform, false);

            var text = textGo.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = "Hello, Astray!";
            text.fontSize = 48;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.rectTransform.sizeDelta = new Vector2(1200, 200);

            Object.DontDestroyOnLoad(canvasGo);
            return UniTask.CompletedTask;
        }
    }
}
