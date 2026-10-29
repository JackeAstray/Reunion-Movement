using NUnit.Framework;
using Gear.Editor.Guards;
using Gear.Editor.Shared;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;

namespace Gear.Tests
{
    /// <summary>
    /// 把架构守卫接入测试套件：CI 跑 EditMode 测试时，这些规则自动生效。
    /// 拆成多个用例而不是一个 <c>RunAll</c>，是为了失败时能直接看出是哪条规则挂了。
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
                "asmdef 依赖方向颠倒。分层约定：Base → Common → Utilities → Modules → Runtime，上层不能反过来被下层引用。");
        }

        [Test]
        public void LegacyNames_HaveNoResidue()
        {
            Assert.IsTrue(
                ArchitectureGuard.CheckLegacyNames(),
                "代码、模板或随包文档里还有改名前的旧程序集名/旧目录路径。"
                + "请改成当前的 Gear.Framework.Runtime / Framework/Runtime/System 等命名。");
        }

        [Test]
        public void LinkXml_PreservedTypes_Exist_In_Declared_Assembly()
        {
            string path = GearPaths.InFramework("link.xml");
            Assert.IsTrue(File.Exists(path), "link.xml 缺失：" + path);

            var missing = new List<string>();
            XDocument document = XDocument.Load(path);
            foreach (XElement assemblyElement in document.Root.Elements("assembly"))
            {
                string assemblyName = (string)assemblyElement.Attribute("fullname");
                Assembly assembly = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == assemblyName);
                if (assembly == null)
                {
                    try { assembly = Assembly.Load(assemblyName); }
                    catch { /* 交给下面的报告 */ }
                }

                if (assembly == null)
                {
                    // 整程序集 preserve=all 没有类型条目，不能因为没被加载就判失败
                    if (!assemblyElement.Elements("type").Any()) continue;
                    missing.Add($"程序集未加载：{assemblyName}");
                    continue;
                }

                foreach (XElement typeElement in assemblyElement.Elements("type"))
                {
                    string typeName = (string)typeElement.Attribute("fullname");
                    if (assembly.GetType(typeName, throwOnError: false) == null)
                    {
                        missing.Add($"{typeName} 不在 {assemblyName} 程序集里（link.xml 写错程序集）");
                    }
                }
            }

            CollectionAssert.IsEmpty(missing, string.Join("\n", missing));
        }
    }
}
