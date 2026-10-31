using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using R3;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Gear.Framework.Runtime;
using Gear.Framework.Runtime.Base;
using Gear.Framework.Runtime.UI;
using Gear.Framework.Runtime.UIInput;

namespace Gear.Tests.PlayMode
{
    /// <summary>
    /// UIRouter / UISystem 的 PlayMode 集成回归：
    /// 1) Back 先关模态，不误关下面被遮住的窗口；
    /// 2) [Static] 窗口关闭后保留实例，重开复用同一实例。
    ///
    /// 用最小引擎（ModulePresets.Minimal）+ 反射注入窗口状态，避免依赖示例场景/预制体。
    /// </summary>
    public class UIRouterPlayModeTests
    {
        private sealed class TestEntry : GameEntry
        {
            public override UniTask OnBeforeInitAsync() => UniTask.CompletedTask;
            public override UniTask OnGameStartAsync() => UniTask.CompletedTask;
        }

        private readonly List<GameObject> spawned = new List<GameObject>();

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Bootstrap.Disable();
            if (GameEngine.Current != null) GameEngine.Current.Dispose();

            var task = Bootstrap.StartAsync(new TestEntry(), ModulePresets.Minimal());
            while (!task.GetAwaiter().IsCompleted) yield return null;

            InitResult result = task.GetAwaiter().GetResult();
            Assert.IsTrue(result.IsSuccess, "最小引擎启动失败: " + result.ErrorMessage);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            UIRouter.ClearStack();
            UIRouter.ResetModal();
            for (int i = 0; i < spawned.Count; i++)
            {
                if (spawned[i] != null) Object.Destroy(spawned[i]);
            }
            spawned.Clear();

            if (GameEngine.Current != null) GameEngine.Current.Dispose();
            var driver = Object.FindFirstObjectByType<GameEngineDriver>();
            if (driver != null) Object.Destroy(driver.gameObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Cancel_TextInputFocused_DoesNotReachGlobalCancel()
        {
            UISystem ui = Modules.Get<UISystem>();
            Assert.IsNotNull(ui.EventSystem, "UISystem 应创建 EventSystem");

            // 最小引擎不含 UIInputSystem：手动初始化，只为验证 DispatchCancel 的文本聚焦闸门
            UIInputSystem input = UIInputSystem.Instance;
            var initTask = input.Init();
            while (!initTask.GetAwaiter().IsCompleted) yield return null;
            initTask.GetAwaiter().GetResult();

            var canvasGo = new GameObject("TestCanvas", typeof(Canvas));
            spawned.Add(canvasGo);

            var inputGo = new GameObject("TestInput", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            inputGo.SetActive(false);
            var field = inputGo.AddComponent<InputField>();
            inputGo.transform.SetParent(canvasGo.transform, false);
            var textGo = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            textGo.transform.SetParent(inputGo.transform, false);
            field.textComponent = textGo.GetComponent<Text>();
            inputGo.SetActive(true);
            spawned.Add(inputGo);

            int cancelCount = 0;
            var subscription = input.CancelSubject.Subscribe(_ => cancelCount++);
            MethodInfo dispatch = typeof(UIInputSystem).GetMethod("DispatchCancel", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(dispatch, "DispatchCancel 不存在，测试需要随实现更新");

            // 文本输入聚焦：Cancel 被输入框消费，不应广播全局 CancelSubject
            ui.EventSystem.SetSelectedGameObject(inputGo);
            dispatch.Invoke(input, new object[] { null, 0 });
            Assert.AreEqual(0, cancelCount, "文本聚焦时 ESC 不应触发全局返回");

            // 取消聚焦：同样的派发应广播一次全局取消
            ui.EventSystem.SetSelectedGameObject(null);
            dispatch.Invoke(input, new object[] { null, 0 });
            Assert.AreEqual(1, cancelCount, "非文本聚焦时 ESC 应触发全局返回");

            subscription.Dispose();
            yield return null;
        }

        [UnityTest]
        public IEnumerator OpenTwice_ReusesSameInstanceAndStaysOpen()
        {
            UISystem ui = Modules.Get<UISystem>();
            UIController window = CreateWindow("RepeatWindow");
            Inject(ui, window, "RepeatWindow", isStatic: false);

            UILoadState first = ui.OpenWindow("RepeatWindow");
            UILoadState second = ui.OpenWindow("RepeatWindow");

            Assert.IsNotNull(first);
            Assert.IsNotNull(second);
            Assert.AreSame(window, second.uiWindow, "重复打开应复用同一实例");
            Assert.IsTrue(ui.IsOpen("RepeatWindow"), "重复打开后应保持可见");
            yield return null;
        }

        [UnityTest]
        public IEnumerator Back_ClosesTopModal_NotWindowUnderIt()
        {
            UISystem ui = Modules.Get<UISystem>();
            UIController under = CreateWindow("Underlying");
            UIController modal = CreateWindow("Modal");

            Inject(ui, under, "Underlying", isStatic: false);
            Inject(ui, modal, "Modal", isStatic: false);

            UIRouter.Stack.Push("Underlying");
            UIRouter.NotifyPushed("Modal", true);

            Assert.IsTrue(UIRouter.Back(), "有模态时 Back 应返回 true");
            Assert.IsFalse(ui.IsOpen("Modal"), "模态应被关闭");
            Assert.IsTrue(ui.IsOpen("Underlying"), "下层窗口不应被误关");
            Assert.AreEqual(1, UIRouter.Depth, "导航栈不应被弹出（只关了模态）");
            yield return null;
        }

        [UnityTest]
        public IEnumerator StaticWindow_KeepsInstanceAfterClose_AndReopens()
        {
            UISystem ui = Modules.Get<UISystem>();
            UIController window = CreateWindow("StaticWindow");
            Inject(ui, window, "StaticWindow", isStatic: true);

            // 关闭：Static 不销毁、只隐藏；cache 仍保留同一实例
            ui.CloseWindow("StaticWindow");
            Assert.IsFalse(ui.IsOpen("StaticWindow"), "关闭后应不可见");
            Assert.IsTrue(window != null, "Static 窗口关闭后实例必须保留");

            // 重开：走 cache 路径，复用同一实例
            UILoadState reopened = ui.OpenWindow("StaticWindow");
            Assert.IsNotNull(reopened);
            Assert.AreSame(window, reopened.uiWindow, "Static 窗口应复用同一实例");
            Assert.IsTrue(ui.IsOpen("StaticWindow"), "重开后应可见");
            yield return null;
        }

        private UIController CreateWindow(string name)
        {
            var go = new GameObject(name);
            go.SetActive(true);
            var controller = go.AddComponent<UIController>();
            controller.uiName = name;
            controller.OnInit();
            spawned.Add(go);
            return controller;
        }

        /// <summary>
        /// 注入一个"已加载"的窗口状态，绕开 Resources/Addressables 预制体。
        /// 这样测试的是 UISystem/UIRouter 的生命周期逻辑，而不是资源加载。
        /// </summary>
        private static void Inject(UISystem ui, UIController controller, string name, bool isStatic)
        {
            FieldInfo field = typeof(UISystem).GetField("uiStateCache", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, "UISystem.uiStateCache 字段不存在，测试需要随实现更新");

            var cache = (IDictionary)field.GetValue(ui);
            Assert.IsNotNull(cache);

            cache[name] = new UILoadState(name)
            {
                uiWindow = controller,
                isOnInit = true,
                isStaticUI = isStatic,
                openWhenFinish = false,
            };
        }
    }
}
