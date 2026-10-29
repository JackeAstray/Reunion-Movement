using System;
using NUnit.Framework;
using Gear.Framework.Runtime;
using Gear.Framework.Runtime.UI;
using UnityEngine;

namespace Gear.Tests
{
    /// <summary>
    /// Modules 统一访问器的 EditMode 测试。
    /// 覆盖"引擎未启动 / 类型未注册"两类失败路径，确保报错信息带类型名，
    /// 避免调用方拿到 NullReferenceException 后无从下手。
    /// </summary>
    public class ModulesAccessorTests
    {
        [TearDown]
        public void TearDown()
        {
            // 本组测试不启动引擎；若前序用例留下了引擎实例，只做提示，不强行销毁
            if (GameEngine.Current != null)
            {
                Debug.Log("[ModulesAccessorTests] 检测到已存在的 GameEngine 实例，部分断言依赖未启动状态");
            }
        }

        [Test]
        public void All_IsNeverNull()
        {
            Assert.IsNotNull(Modules.All, "Modules.All 在引擎未启动时应返回空集合而不是 null");
        }

        [Test]
        public void TryGet_UnregisteredType_ReturnsFalse()
        {
            bool found = Modules.TryGet<UISystem>(out var module);

            Assert.IsFalse(found, "UISystem 不应在引擎未启动时被解析到");
            Assert.IsNull(module);
        }

        [Test]
        public void Contains_UnregisteredType_IsFalse()
        {
            Assert.IsFalse(Modules.Contains<UISystem>());
        }

        [Test]
        public void Get_UnregisteredType_ThrowsWithTypeName()
        {
            var ex = Assert.Throws<InvalidOperationException>(() => Modules.Get<UISystem>());

            StringAssert.Contains(nameof(UISystem), ex.Message,
                "异常信息必须包含类型名，否则调用方无法定位是哪个模块没注册");
        }

        [Test]
        public void DescribeAll_AlwaysReturnsText()
        {
            string text = Modules.DescribeAll();

            Assert.IsNotNull(text);
            Assert.IsNotEmpty(text, "即使没有任何模块，也应给出可读的提示文本");
        }
    }
}
