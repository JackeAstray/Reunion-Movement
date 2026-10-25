using System;
using ReunionMovement.Common;
using ReunionMovement.Editor.Shared;

namespace ReunionMovement.EditorTools
{
    /// <summary>
    /// ExcelSystem 的路径与包安装模式策略。
    /// 单独一个 partial 文件，避免主文件继续变长（架构守卫的行数棘轮）。
    /// </summary>
    public partial class ExcelSystem
    {
        // 源表格：源码安装时就在框架内；包安装时落到可写的生成目录
        static readonly string toDirSO = ReunionPaths.InWritable("Editor/Excel/Resources");

        // 生成物一律走可写根：源码安装 = 框架根，包安装 = Assets/ReunionMovementGenerated
        static readonly string scriptOutPutPath = ReunionPaths.InWritable("GenerateScript/AutoScripts/");
        static readonly string scriptableOutPutPath = ReunionPaths.InWritable("Resources/ScriptableObjects/");
        static readonly string jsonOutPutPath = ReunionPaths.InWritable("Resources/AutoDatabase/");

        /// <summary>
        /// 包安装模式下包内已经带了预生成的类型；同名再生成会落到 Assembly-CSharp，
        /// 与包内类型重复（CS0433）。返回 true 表示本次应跳过。
        /// </summary>
        private static bool SkipPackageGenerated(string typeName)
        {
            if (!ReunionPaths.IsInstalledAsPackage) return false;
            if (!IsPreGeneratedInPackage(typeName)) return false;

            Log.Error($"[Excel] 包安装模式下不能重新生成已存在的类型 {typeName}；" +
                      "请换一个新的表名，或把框架以源码/嵌入式包方式安装。");
            return true;
        }

        private static bool IsPreGeneratedInPackage(string typeName)
        {
            Type type = ResolveRuntimeType($"ReunionMovement.{typeName}");
            if (type == null) return false;

            string assemblyName = type.Assembly.GetName().Name;
            return assemblyName != null && assemblyName.StartsWith("ReunionMovement.", StringComparison.Ordinal);
        }
    }
}
