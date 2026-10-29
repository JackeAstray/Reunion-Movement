using Cysharp.Threading.Tasks;
using NUnit.Framework;
using Gear.Framework.Runtime;
using Gear.Framework.Runtime.Base;
using System.Collections.Generic;

namespace Gear.Tests
{
    /// <summary>
    /// 模块目录（依赖声明 + 稳定拓扑排序 + 启动前校验）的 EditMode 测试。
    ///
    /// 回归保护：StartGame.CreateModules() 里的初始化顺序过去只靠注释维持，
    /// 顺序错了表现为"初始化时资源为空"而不是报错。这里把排序规则钉死在测试里。
    /// </summary>
    public class ModuleCatalogTests
    {
        [Test]
        public void Sorts_By_Order_Regardless_Of_Registration_Order()
        {
            var declared = new List<ICustomSystem>
            {
                new FakeThird(),
                new FakeFirst(),
                new FakeSecond(),
            };

            Assert.IsTrue(ModuleCatalog.TryBuild(declared, out var ordered, out var errors), errors);
            Assert.AreEqual(3, ordered.Count);
            Assert.IsInstanceOf<FakeFirst>(ordered[0]);
            Assert.IsInstanceOf<FakeSecond>(ordered[1]);
            Assert.IsInstanceOf<FakeThird>(ordered[2]);
        }

        [Test]
        public void Dependency_Forces_Provider_First_Even_With_Larger_Order()
        {
            // Consumer 的 Order 更小，但声明了依赖 Provider，必须排在后面
            var declared = new List<ICustomSystem> { new ConsumerWithLargeDependency(), new ProviderWithLargeOrder() };

            Assert.IsTrue(ModuleCatalog.TryBuild(declared, out var ordered, out var errors), errors);
            Assert.IsInstanceOf<ProviderWithLargeOrder>(ordered[0]);
            Assert.IsInstanceOf<ConsumerWithLargeDependency>(ordered[1]);
        }

        [Test]
        public void Equal_Order_Keeps_Registration_Order()
        {
            var a = new SameOrderA();
            var b = new SameOrderB();
            var declared = new List<ICustomSystem> { b, a };

            Assert.IsTrue(ModuleCatalog.TryBuild(declared, out var ordered, out _));
            Assert.AreSame(b, ordered[0], "Order 相同时必须保持注册顺序（稳定排序）");
            Assert.AreSame(a, ordered[1]);
        }

        [Test]
        public void Missing_Dependency_Is_Reported_And_Returns_False()
        {
            var declared = new List<ICustomSystem> { new ConsumerWithLargeDependency() };

            Assert.IsFalse(ModuleCatalog.TryBuild(declared, out var ordered, out var errors));
            StringAssert.Contains("未注册", errors);
            CollectionAssert.AreEqual(declared, ordered, "校验失败时应原样返回注册顺序");
        }

        [Test]
        public void Duplicate_Instance_Is_Reported()
        {
            var module = new FakeFirst();
            var declared = new List<ICustomSystem> { module, module };

            Assert.IsFalse(ModuleCatalog.TryBuild(declared, out _, out var errors));
            StringAssert.Contains("重复注册", errors);
        }

        [Test]
        public void Repeated_Dependency_Is_Not_A_Cycle()
        {
            // 同一个依赖写两遍：不能因此判成环，也不能把模块漏掉
            var declared = new List<ICustomSystem> { new ConsumerWithRepeatedDependency(), new ProviderWithLargeOrder() };

            Assert.IsTrue(ModuleCatalog.TryBuild(declared, out var ordered, out var errors), errors);
            Assert.IsInstanceOf<ProviderWithLargeOrder>(ordered[0]);
            Assert.IsInstanceOf<ConsumerWithRepeatedDependency>(ordered[1]);
        }

        [Test]
        public void Two_Instances_Of_Same_Type_Are_Reported()
        {
            // 同一类型的两个不同实例：只按实例判重会漏掉，导致重复 Init/Update
            var declared = new List<ICustomSystem> { new FakeFirst(), new FakeFirst() };

            Assert.IsFalse(ModuleCatalog.TryBuild(declared, out _, out var errors));
            StringAssert.Contains("两次", errors);
        }

        [Test]
        public void Dependency_On_Interface_Uses_First_Declared_Provider()
        {
            // DependsOn 可以写接口；多个提供者时取注册顺序里第一个（与 ModuleCatalog 的文档一致）
            var consumer = new InterfaceConsumer();
            var providerA = new InterfaceProviderA();
            var providerB = new InterfaceProviderB();
            var declared = new List<ICustomSystem> { consumer, providerB, providerA };

            Assert.IsTrue(ModuleCatalog.TryBuild(declared, out var ordered, out var errors), errors);
            Assert.Less(ordered.IndexOf(providerB), ordered.IndexOf(consumer),
                "消费者必须排在按注册顺序选中的提供者之后");
        }

        [Test]
        public void Dependency_On_Base_Class_Is_Supported()
        {
            var consumer = new BaseConsumer();
            var provider = new DerivedProvider();
            var declared = new List<ICustomSystem> { consumer, provider };

            Assert.IsTrue(ModuleCatalog.TryBuild(declared, out var ordered, out var errors), errors);
            Assert.Less(ordered.IndexOf(provider), ordered.IndexOf(consumer));
        }

        [Test]
        public void Self_Dependency_Is_Reported()
        {
            var declared = new List<ICustomSystem> { new SelfDependent() };

            Assert.IsFalse(ModuleCatalog.TryBuild(declared, out _, out var errors));
            StringAssert.Contains("依赖了自己", errors);
        }

        [Test]
        public void Null_Entry_Is_Reported()
        {
            var declared = new List<ICustomSystem> { null };

            Assert.IsFalse(ModuleCatalog.TryBuild(declared, out _, out var errors));
            StringAssert.Contains("null", errors);
        }

        [Test]
        public void Cycle_Is_Reported()
        {
            var declared = new List<ICustomSystem> { new CycleA(), new CycleB() };

            Assert.IsFalse(ModuleCatalog.TryBuild(declared, out _, out var errors));
            StringAssert.Contains("环", errors);
        }

        [Test]
        public void Empty_List_Is_Valid()
        {
            Assert.IsTrue(ModuleCatalog.TryBuild(new List<ICustomSystem>(), out var ordered, out _));
            Assert.AreEqual(0, ordered.Count);
        }

        [Test]
        public void Describe_Lists_Resolved_Order()
        {
            var declared = new List<ICustomSystem> { new FakeSecond(), new FakeFirst() };
            ModuleCatalog.TryBuild(declared, out var ordered, out _);

            var text = ModuleCatalog.Describe(ordered);
            StringAssert.Contains("FakeFirst(10)", text);
            StringAssert.Contains("FakeSecond(20)", text);
        }

        [Test]
        public void ExpandDependencies_Adds_Missing_Providers_Recursively()
        {
            var declared = new List<ICustomSystem> { new ChainConsumer() };

            var expanded = ModuleCatalog.ExpandDependencies(declared, out var added, out var unresolved);

            Assert.AreEqual(0, unresolved.Count);
            Assert.AreEqual(2, added.Count, "应补入直接依赖，以及它自己依赖的提供者");
            Assert.AreEqual(3, expanded.Count);
            Assert.IsTrue(ModuleCatalog.TryBuild(expanded, out var ordered, out var errors), errors);
            Assert.IsInstanceOf<ChainProviderC>(ordered[0]);
            Assert.IsInstanceOf<ChainProviderB>(ordered[1]);
            Assert.IsInstanceOf<ChainConsumer>(ordered[2]);
        }

        [Test]
        public void ExpandDependencies_Leaves_Satisfied_List_Unchanged()
        {
            var consumer = new PairConsumer();
            var provider = new PairProvider();
            var declared = new List<ICustomSystem> { consumer, provider };

            var expanded = ModuleCatalog.ExpandDependencies(declared, out var added, out var unresolved);

            Assert.AreEqual(0, added.Count);
            Assert.AreEqual(0, unresolved.Count);
            Assert.AreEqual(2, expanded.Count);
            Assert.AreSame(consumer, expanded[0]);
            Assert.AreSame(provider, expanded[1]);
        }

        [Test]
        public void ExpandDependencies_Prefers_Static_Instance_Entry()
        {
            // 真实模块多是 MonoBehaviour 单例，拿不到无参构造，只能走静态 Instance
            var declared = new List<ICustomSystem> { new ConsumerOfStaticEntry() };

            var expanded = ModuleCatalog.ExpandDependencies(declared, out var added, out var unresolved);

            Assert.AreEqual(0, unresolved.Count);
            Assert.AreEqual(1, added.Count);
            CollectionAssert.Contains(expanded, StaticEntryProvider.Instance);
        }

        [Test]
        public void ExpandDependencies_Reports_Unresolvable_Dependency()
        {
            var declared = new List<ICustomSystem> { new ConsumerOfMissingInterface() };

            var expanded = ModuleCatalog.ExpandDependencies(declared, out var added, out var unresolved);

            Assert.AreEqual(0, added.Count);
            Assert.AreEqual(1, unresolved.Count);
            StringAssert.Contains(nameof(IMissingProvider), unresolved[0]);
            Assert.AreEqual(1, expanded.Count, "补入失败时应保持原清单，不能吞掉模块");
        }

        [Test]
        public void ExpandDependencies_Handles_Null_And_Empty()
        {
            Assert.IsNull(ModuleCatalog.ExpandDependencies(null, out var addedForNull, out var unresolvedForNull));
            Assert.AreEqual(0, addedForNull.Count);
            Assert.AreEqual(0, unresolvedForNull.Count);

            var empty = new List<ICustomSystem>();
            Assert.AreSame(empty, ModuleCatalog.ExpandDependencies(empty, out var addedForEmpty, out var unresolvedForEmpty));
            Assert.AreEqual(0, addedForEmpty.Count);
            Assert.AreEqual(0, unresolvedForEmpty.Count);
        }

        // ---- 测试用假模块（顺序只由 [ModuleOrder] 决定）----

        private abstract class FakeModule : ICustomSystem
        {
            public double InitProgress => 100;

            public UniTask Init() => UniTask.CompletedTask;
        }

        [ModuleOrder(10)]
        private sealed class FakeFirst : FakeModule { }

        [ModuleOrder(20)]
        private sealed class FakeSecond : FakeModule { }

        [ModuleOrder(30)]
        private sealed class FakeThird : FakeModule { }

        [ModuleOrder(10)]
        private sealed class SameOrderA : FakeModule { }

        [ModuleOrder(10)]
        private sealed class SameOrderB : FakeModule { }

        [ModuleOrder(100)]
        private sealed class ProviderWithLargeOrder : FakeModule { }

        [ModuleOrder(1, typeof(ProviderWithLargeOrder))]
        private sealed class ConsumerWithLargeDependency : FakeModule { }

        [ModuleOrder(1, typeof(ProviderWithLargeOrder), typeof(ProviderWithLargeOrder))]
        private sealed class ConsumerWithRepeatedDependency : FakeModule { }

        [ModuleOrder(1, typeof(CycleB))]
        private sealed class CycleA : FakeModule { }

        [ModuleOrder(2, typeof(CycleA))]
        private sealed class CycleB : FakeModule { }

        [ModuleOrder(10, typeof(PairProvider))]
        private sealed class PairConsumer : FakeModule { }

        [ModuleOrder(20)]
        private sealed class PairProvider : FakeModule
        {
            public PairProvider() { }
        }

        [ModuleOrder(10, typeof(ChainProviderB))]
        private sealed class ChainConsumer : FakeModule { }

        [ModuleOrder(20, typeof(ChainProviderC))]
        private sealed class ChainProviderB : FakeModule
        {
            public ChainProviderB() { }
        }

        [ModuleOrder(30)]
        private sealed class ChainProviderC : FakeModule
        {
            public ChainProviderC() { }
        }

        private interface IMissingProvider : ICustomSystem { }

        [ModuleOrder(10, typeof(IMissingProvider))]
        private sealed class ConsumerOfMissingInterface : FakeModule { }

        [ModuleOrder(10, typeof(StaticEntryProvider))]
        private sealed class ConsumerOfStaticEntry : FakeModule { }

        private interface IProviderRole { }

        [ModuleOrder(10)]
        private sealed class InterfaceProviderA : FakeModule, IProviderRole { }

        [ModuleOrder(20)]
        private sealed class InterfaceProviderB : FakeModule, IProviderRole { }

        [ModuleOrder(1, typeof(IProviderRole))]
        private sealed class InterfaceConsumer : FakeModule { }

        private abstract class ProviderBase : FakeModule { }

        [ModuleOrder(10)]
        private sealed class DerivedProvider : ProviderBase { }

        [ModuleOrder(1, typeof(ProviderBase))]
        private sealed class BaseConsumer : FakeModule { }

        [ModuleOrder(10, typeof(SelfDependent))]
        private sealed class SelfDependent : FakeModule { }

        [ModuleOrder(20)]
        private sealed class StaticEntryProvider : FakeModule
        {
            public static StaticEntryProvider Instance { get; } = new StaticEntryProvider();
            private StaticEntryProvider() { }
        }
    }
}
