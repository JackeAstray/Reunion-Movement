using System;
using System.Reflection;
using System.Reflection.Emit;

namespace Astray.HeadlessCheck
{
    /// <summary>
    /// 用 Reflection.Emit 在内存里造"假模块"。
    /// <para>
    /// 真实模块多是 <c>SingletonMgr&lt;T&gt; : MonoBehaviour</c>，构造即 new GameObject，
    /// 纯 .NET 进程里造不出来。但 <c>ModuleCatalog</c> 只关心 <c>ICustomSystem</c> 和
    /// <c>[ModuleOrder]</c> 两件事，所以造几个只实现接口的最小类型就够了。
    /// </para>
    /// <para>
    /// 分两步：<see cref="Define"/> 建类型但不落地，<see cref="Finish"/> 打特性并 CreateType。
    /// 这样能构造出"A 依赖 B、B 依赖 A"的环——直接一次性建类型做不到。
    /// </para>
    /// </summary>
    internal sealed class FakeModuleFactory
    {
        /// <summary>动态程序集名。ModuleOrder 的 Type 参数会以程序集限定名保存，需要能被解析回来。</summary>
        public const string DynamicAssemblyName = "Astray.HeadlessFakes";

        private static int assemblyCounter;

        private readonly Type icustomSystem;
        private readonly Type initializable;
        private readonly Type uniTask;
        private readonly Type moduleOrderAttribute;
        private readonly ModuleBuilder module;
        private int counter;

        /// <summary>
        /// 本工厂的程序集简单名（<see cref="DynamicAssemblyName"/> + 序号）。
        /// <para>
        /// 必须每个工厂唯一：<c>[ModuleOrder(typeof(X))]</c> 的参数以"类型名, 程序集名"保存，
        /// 若所有工厂都用同一个程序集名，读回特性时只能按名解析到其中任意一个程序集，
        /// 依赖类型就会指向别的工厂里同名的假模块（顺序/依赖全乱）。唯一名字 + 按名查表可根治。
        /// </para>
        /// </summary>
        public string AssemblyName { get; }

        /// <summary>内存里的假模块程序集（不落盘，需要 AssemblyResolve 才能被属性反序列化找到）。</summary>
        public Assembly DynamicAssembly { get; }

        public FakeModuleFactory(Type icustomSystem, Type initializable, Type uniTask, Type moduleOrderAttribute)
        {
            this.icustomSystem = icustomSystem;
            this.initializable = initializable;
            this.uniTask = uniTask;
            this.moduleOrderAttribute = moduleOrderAttribute;

            AssemblyName = DynamicAssemblyName + "." + (++assemblyCounter);
            var assembly = AssemblyBuilder.DefineDynamicAssembly(
                new AssemblyName(AssemblyName), AssemblyBuilderAccess.Run);
            module = assembly.DefineDynamicModule(AssemblyName);
            DynamicAssembly = assembly;
        }

        /// <summary>一次性建好并落地一个模块类型（无依赖环时用这个）。</summary>
        public Type Create(int order, params Type[] dependencies)
        {
            return Finish(Define(), order, dependencies);
        }

        /// <summary>声明类型骨架，但先不 CreateType——用于构造互相依赖的两个类型。</summary>
        public TypeBuilder Define()
        {
            string name = "FakeModule" + (++counter);
            TypeBuilder tb = module.DefineType(
                name,
                TypeAttributes.Public | TypeAttributes.Class | TypeAttributes.Sealed,
                typeof(object),
                new[] { icustomSystem });

            tb.DefineDefaultConstructor(MethodAttributes.Public);

            // double InitProgress => 100;
            MethodBuilder getProgress = tb.DefineMethod(
                "get_InitProgress",
                MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.NewSlot |
                MethodAttributes.Final | MethodAttributes.HideBySig | MethodAttributes.SpecialName,
                typeof(double),
                Type.EmptyTypes);
            ILGenerator il1 = getProgress.GetILGenerator();
            il1.Emit(OpCodes.Ldc_R8, 100.0);
            il1.Emit(OpCodes.Ret);

            PropertyBuilder property = tb.DefineProperty("InitProgress", PropertyAttributes.None, typeof(double), null);
            property.SetGetMethod(getProgress);

            // UniTask Init() => default;
            MethodBuilder init = tb.DefineMethod(
                "Init",
                MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.NewSlot |
                MethodAttributes.Final | MethodAttributes.HideBySig,
                uniTask,
                Type.EmptyTypes);
            ILGenerator il2 = init.GetILGenerator();
            LocalBuilder local = il2.DeclareLocal(uniTask);
            il2.Emit(OpCodes.Ldloca, local);
            il2.Emit(OpCodes.Initobj, uniTask);
            il2.Emit(OpCodes.Ldloc, local);
            il2.Emit(OpCodes.Ret);

            tb.DefineMethodOverride(getProgress, initializable.GetProperty("InitProgress").GetGetMethod());
            tb.DefineMethodOverride(init, initializable.GetMethod("Init"));

            return tb;
        }

        /// <summary>给类型打上 <c>[ModuleOrder]</c> 并落地。</summary>
        public Type Finish(TypeBuilder tb, int order, params Type[] dependencies)
        {
            ConstructorInfo ctor = moduleOrderAttribute.GetConstructor(new[] { typeof(int), typeof(Type[]) });
            var attribute = new CustomAttributeBuilder(ctor, new object[] { order, dependencies ?? Type.EmptyTypes });
            tb.SetCustomAttribute(attribute);
            return tb.CreateType();
        }

        public object Instantiate(Type type)
        {
            return Activator.CreateInstance(type);
        }
    }
}
