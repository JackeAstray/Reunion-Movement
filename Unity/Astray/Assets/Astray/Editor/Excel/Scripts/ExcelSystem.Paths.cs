using System;
using Astray.Common;
using Astray.Editor.Shared;

namespace Astray.EditorTools
{
    /// <summary>
    /// ExcelSystem 的路径与包安装模式策略。
    /// 单独一个 partial 文件，避免主文件继续变长（架构守卫的行数棘轮）。
    /// </summary>
    public partial class ExcelSystem
    {
        // 源表格：源码安装时就在框架内；包安装时落到可写的生成目录
        static readonly string toDirSO = AstrayPaths.InWritable("Editor/Excel/Resources");

        // 生成物一律走可写根：源码安装 = 框架根，包安装 = Assets/AstrayGenerated
        static readonly string scriptOutPutPath = AstrayPaths.InWritable("GenerateScript/AutoScripts/");
        static readonly string scriptableOutPutPath = AstrayPaths.InWritable("Resources/ScriptableObjects/");
        static readonly string jsonOutPutPath = AstrayPaths.InWritable("Resources/AutoDatabase/");

        /// <summary>
        /// 包安装模式下包内已经带了预生成的类型；同名再生成会落到 Assembly-CSharp，
        /// 与包内类型重复（CS0433）。返回 true 表示本次应跳过。
        /// </summary>
        private static bool SkipPackageGenerated(string typeName)
        {
            if (!AstrayPaths.IsInstalledAsPackage) return false;
            if (!IsPreGeneratedInPackage(typeName)) return false;

            Log.Error($"[Excel] 包安装模式下不能重新生成已存在的类型 {typeName}；" +
                      "请换一个新的表名，或把框架以源码/嵌入式包方式安装。");
            return true;
        }

        private static bool IsPreGeneratedInPackage(string typeName)
        {
            Type type = ResolveRuntimeType($"Astray.{typeName}");
            if (type == null) return false;

            string assemblyName = type.Assembly.GetName().Name;
            return assemblyName != null && assemblyName.StartsWith("Astray.", StringComparison.Ordinal);
        }
    }
}
