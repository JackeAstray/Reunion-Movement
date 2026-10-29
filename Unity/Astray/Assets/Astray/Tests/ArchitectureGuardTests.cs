using NUnit.Framework;
using Astray.Editor.Guards;

namespace Astray.Tests
{
    /// <summary>
    /// 把架构守卫接入测试套件：CI 跑 EditMode 测试时，这些规则自动生效。
    /// 拆成三个用例而不是一个 <c>RunAll</c>，是为了失败时能直接看出是哪条规则挂了。
    /// </summary>
    public class ArchitectureGuardTests
    {
        [Test]
        public void FileSizes_HaveNoNewOffenders()
        {
            Assert.IsTrue(
                ArchitectureGuard.CheckFileSizes(),
                $"有源文件超过 {ArchitectureGuard.MaxLines} 行且未登记基线，或已登记的文件又变大了。"
                + "要么拆分，要么把新行数写进 file-size-baseline.txt（只能变大一次，之后只能变小）。");
        }

        [Test]
        public void EverySourceFile_HasMeta()
        {
            Assert.IsTrue(
                ArchitectureGuard.CheckMetaFiles(),
                "存在缺少 .meta 的源文件。移动/新建文件时必须连同 .meta 一起提交，否则引用会断。");
        }

        [Test]
        public void InstanceUsage_DoesNotGrow()
        {
            Assert.IsTrue(
                ArchitectureGuard.CheckInstanceUsage(),
                "新增了 XxxSystem.Instance 调用点。运行时请改用 Modules.Get<T>()，"
                + "编辑器/测试里才用 Instance（它不依赖引擎是否启动）。");
        }

        [Test]
        public void EveryCustomSystem_DeclaresModuleOrder()
        {
            Assert.IsTrue(
                ArchitectureGuard.CheckModuleOrder(),
                "有 ICustomSystem 实现没写 [ModuleOrder]。没有它，初始化顺序就取决于 CreateModules() 里的书写位置，"
                + "加一行就可能悄悄改变启动顺序。");
        }

        [Test]
        public void AssemblyReferences_PointDownwards()
        {
            Assert.IsTrue(
                ArchitectureGuard.CheckAssemblyLayers(),
                "asmdef 依赖方向颠倒。分层约定：Base → Common → Utils → Core，上层不能反过来被下层引用。");
        }
    }
}
