using NUnit.Framework;
using Gear.Framework.Runtime;
using Gear.Framework.Runtime.Scene;
using Gear.Framework.Runtime.UI;

namespace Gear.Tests
{
    /// <summary>
    /// 窗口目录的声明式元数据测试（纯反射，不实例化 MonoBehaviour）。
    /// </summary>
    public class UIWindowCatalogTests
    {
        [UIWindow("GearTestStaticWindow", Layer = PanelType.TipsUI, Static = true)]
        private sealed class StaticTestWindow : UIController
        {
        }

        [UIWindow("GearTestPooledWindow", Pooled = true)]
        private sealed class PooledTestWindow : UIController
        {
        }

        private sealed class PlainTestWindow : UIController
        {
        }

        [Test]
        public void StaticDeclaration_IsReadIntoDescriptor()
        {
            UIWindowDescriptor descriptor = UIWindowCatalog.Get("GearTestStaticWindow");

            Assert.IsTrue(descriptor.Declared);
            Assert.IsTrue(descriptor.Static, "[UIWindow(Static = true)] 应写入描述符");
            Assert.IsFalse(descriptor.Pooled);
            Assert.AreEqual(PanelType.TipsUI, descriptor.Layer);
        }

        [Test]
        public void PooledDeclaration_DoesNotEnableStatic()
        {
            UIWindowDescriptor descriptor = UIWindowCatalog.Get("GearTestPooledWindow");

            Assert.IsTrue(descriptor.Declared);
            Assert.IsTrue(descriptor.Pooled);
            Assert.IsFalse(descriptor.Static, "Pooled 与 Static 是两套语义，不能互相隐含");
        }

        [Test]
        public void UndeclaredWindow_FallsBackToDefaults()
        {
            UIWindowDescriptor descriptor = UIWindowCatalog.Get("PlainTestWindow");

            Assert.IsFalse(descriptor.Declared);
            Assert.IsFalse(descriptor.Static);
            Assert.IsFalse(descriptor.Pooled);
            Assert.AreEqual(PanelType.NormalUI, descriptor.Layer);
        }

        [Test]
        public void GetId_ByType_MatchesDeclaredId()
        {
            Assert.AreEqual("GearTestStaticWindow", UIWindowCatalog.GetId(typeof(StaticTestWindow)));
        }
    }
}
