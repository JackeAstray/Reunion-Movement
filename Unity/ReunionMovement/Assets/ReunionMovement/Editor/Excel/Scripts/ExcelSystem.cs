using ReunionMovement.Common;
using ReunionMovement.Common.Util;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace ReunionMovement.EditorTools
{
    public class ExcelSystem
    {
        static readonly string toDirSO = "Assets/ReunionMovement/Editor/Excel/Resources";                                           // 源文件路径
        static readonly string scriptOutPutPath = "Assets/ReunionMovement/GenerateScript/AutoScripts/";                             // 脚本输出路径
        static readonly string scriptableOutPutPath = "Assets/ReunionMovement/Resources/ScriptableObjects/";                                        // 脚本对象输出路径

        static int tableRows_Max = 3;                                           // 最大行数
        static int tableRows_1 = 0;                                             // 第一行中文名称
        static int tableRows_2 = 1;                                             // 第二行数据类型
        static int tableRows_3 = 2;                                             // 第三行英文名称

        #region 数据
        private static readonly Dictionary<string, FieldTypes> typeMapping = new Dictionary<string, FieldTypes>(StringComparer.OrdinalIgnoreCase)
        {
            { "bool", FieldTypes.Bool },
            { "int", FieldTypes.Int },
            { "int32", FieldTypes.Int },
            { "ints", FieldTypes.Ints },
            { "int[]", FieldTypes.Ints },
            { "[int]", FieldTypes.Ints },
            { "int32s", FieldTypes.Ints },
            { "int32[]", FieldTypes.Ints },
            { "[int32]", FieldTypes.Ints },
            { "float", FieldTypes.Float },
            { "floats", FieldTypes.Floats },
            { "float[]", FieldTypes.Floats },
            { "[float]", FieldTypes.Floats },
            { "double", FieldTypes.Double },
            { "doubles", FieldTypes.Doubles },
            { "double[]", FieldTypes.Doubles },
            { "[double]", FieldTypes.Doubles },
            { "long", FieldTypes.Long },
            { "int64", FieldTypes.Long },
            { "longs", FieldTypes.Longs },
            { "long[]", FieldTypes.Longs },
            { "[long]", FieldTypes.Longs },
            { "int64s", FieldTypes.Longs },
            { "int64[]", FieldTypes.Longs },
            { "[int64]", FieldTypes.Longs },
            { "vector2", FieldTypes.Vector2 },
            { "vector3", FieldTypes.Vector3 },
            { "vector4", FieldTypes.Vector4 },
            { "rect", FieldTypes.Rect },
            { "rectangle", FieldTypes.Rect },
            { "color", FieldTypes.Color },
            { "colour", FieldTypes.Color },
            { "string", FieldTypes.String },
            { "strings", FieldTypes.Strings },
            { "string[]", FieldTypes.Strings },
            { "[string]", FieldTypes.Strings },
            { "object", FieldTypes.Object }
        };

        private static readonly Dictionary<FieldTypes, string> fieldTypeStringMapping = new Dictionary<FieldTypes, string>
        {
            { FieldTypes.Bool, "bool" },
            { FieldTypes.Int, "int" },
            { FieldTypes.Ints, "List<int>" },
            { FieldTypes.Float, "float" },
            { FieldTypes.Floats, "List<float>" },
            { FieldTypes.Double, "double" },
            { FieldTypes.Doubles, "List<double>" },
            { FieldTypes.Long, "long" },
            { FieldTypes.Longs, "List<long>" },
            { FieldTypes.Vector2, "Vector2" },
            { FieldTypes.Vector3, "Vector3" },
            { FieldTypes.Vector4, "Vector4" },
            { FieldTypes.Rect, "Rect" },
            { FieldTypes.Color, "Color" },
            { FieldTypes.String, "string" },
            { FieldTypes.Strings, "List<string>" },
            { FieldTypes.Object, "object" }
        };
        #endregion



        #region 表格 -> 脚本
        [MenuItem("工具箱/表格处理/表格 -> 脚本", false, 1)]
        public static void ExcelToScripts()
        {
            List<string> xlsxFiles = GetAllConfigFiles(toDirSO);

            foreach (var path in xlsxFiles)
            {
                ExcelToScripts(path);
            }
            Log.Debug("表格转为脚本完成！");
        }

        /// <summary>
        /// 将Excel表格转换为脚本
        /// </summary>
        /// <param name="path"></param>
        /// <param name="createScriptableObjects"></param>
        /// <returns></returns>
        static List<SheetData> ExcelToScripts(string path)
        {
            //构造Excel工具类
            ExcelUtility excel = new ExcelUtility(path);

            if (excel.ResultSet == null)
            {
                string msg = string.Format("无法读取“{0}”。似乎这不是一个xlsx文件!", path);
                EditorUtility.DisplayDialog("ExcelTools", msg, "OK");
                return null;
            }

            List<SheetData> sheets = new List<SheetData>();
            //处理表数据
            foreach (DataTable table in excel.ResultSet.Tables)
            {
                string tableName = table.TableName.Trim();
                //判断表名称前面是否有#  有则忽略
                if (tableName.StartsWith("#"))
                {
                    continue;
                }

                SheetData sheet = new SheetData();
                sheet.table = table;

                if (table.Rows.Count < tableRows_Max)
                {
                    EditorUtility.ClearProgressBar();
                    string msg = string.Format("无法分析“{0}”。1、检查行数：Excel文件应至少包含三行（第一行：中文名称，第二行：数据类型，第三行：英文名称）!\n2、检查Sheet是否存在多个！", path);
                    EditorUtility.DisplayDialog("ExcelTools", msg, "OK");
                    return null;
                }
                //设置类名
                sheet.itemClassName = tableName;

                if (!StringUtil.CheckClassName(sheet.itemClassName))
                {
                    EditorUtility.ClearProgressBar();
                    string msg = string.Format("工作表名称“{0}”无效，因为该工作表的名称应为类名!", sheet.itemClassName);
                    EditorUtility.DisplayDialog("ExcelTools", msg, "OK");
                    return null;
                }
                //字段名称
                object[] fieldNames;
                fieldNames = table.Rows[tableRows_3].ItemArray;
                //字段注释
                object[] fieldNotes;
                fieldNotes = table.Rows[tableRows_1].ItemArray;
                //字段类型
                object[] fieldTypes;
                fieldTypes = table.Rows[tableRows_2].ItemArray;

                for (int i = 0, imax = fieldNames.Length; i < imax; i++)
                {
                    string fieldNameStr = fieldNames[i].ToString().Trim();
                    string fieldNoteStr = fieldNotes[i].ToString().Trim();
                    string fieldTypeStr = fieldTypes[i].ToString().Trim();
                    //检查字段名
                    if (string.IsNullOrEmpty(fieldNameStr))
                    {
                        break;
                    }
                    if (!StringUtil.CheckFieldName(fieldNameStr))
                    {
                        EditorUtility.ClearProgressBar();
                        string msg = string.Format("无法分析“{0}”，因为字段名“{1}”无效!", path, fieldNameStr);
                        EditorUtility.DisplayDialog("ExcelTools", msg, "OK");
                        return null;
                    }

                    //解析类型
                    FieldTypes fieldType = GetFieldType(fieldTypeStr);

                    FieldData field = new FieldData();
                    field.fieldName = fieldNameStr;
                    field.fieldNotes = fieldNoteStr;
                    field.fieldIndex = i;
                    field.fieldType = fieldType;
                    field.fieldTypeName = fieldTypeStr;

                    if (fieldType == FieldTypes.Unknown)
                    {
                        fieldType = FieldTypes.UnknownList;
                        if (fieldTypeStr.StartsWith("[") && fieldTypeStr.EndsWith("]"))
                        {
                            fieldTypeStr = fieldTypeStr.Substring(1, fieldTypeStr.Length - 2).Trim();
                        }
                        else if (fieldTypeStr.EndsWith("[]"))
                        {
                            fieldTypeStr = fieldTypeStr.Substring(0, fieldTypeStr.Length - 2).Trim();
                        }
                        else
                        {
                            fieldType = FieldTypes.Unknown;
                        }

                        field.fieldType = field.fieldType == FieldTypes.UnknownList ? FieldTypes.CustomTypeList : FieldTypes.CustomType;
                    }

                    sheet.fields.Add(field);
                }

                sheets.Add(sheet);
            }

            for (int i = 0; i < sheets.Count; i++)
            {
                GenerateScript(sheets[i]);
                GenerateScriptList(sheets[i], i);
            }

            return sheets;
        }

        /// <summary>
        /// 生成脚本
        /// </summary>
        /// <param name="sheet"></param>
        static async void GenerateScript(SheetData sheet)
        {
            string scriptTemplate = @"//此脚本为工具生成，请勿手动创建 {_CREATE_TIME_} <ExcelTo>
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting;

namespace ReunionMovement
{
    [Serializable]
    public class {_0_}
    {
        {_1_}
        public override string ToString()
        {
            return string.Format(
                {_2_},
                {_3_}
            );
        }

        /// <summary>
        /// 将实例转换为实体
        /// </summary>
        public {_0_} ToEntity()
        {
            return new {_0_}
            {
                {_4_}
            };
        }

        /// <summary>
        /// 解析实体为实例
        /// </summary>
        public static {_0_} FromEntity({_0_} entity)
        {
            return new {_0_}
            {
                {_5_}
            };
        }
    }
}
";
            var dataName = sheet.itemClassName;
            var str = GenerateDataScript(scriptTemplate, dataName, sheet.fields);
            await FileOperationUtil.SaveFile(scriptOutPutPath + dataName + ".cs", str);

            AssetDatabase.Refresh();
        }

        /// <summary>
        /// 生成ScriptableObject用脚本
        /// </summary>
        /// <param name="template"></param>
        /// <param name="scriptName"></param>
        /// <param name="fieldDatas"></param>
        /// <returns></returns>
        static string GenerateDataScript(string template, string scriptName, List<FieldData> fieldDatas)
        {
            StringBuilder privateType = new StringBuilder();
            privateType.AppendLine();

            StringBuilder toEntityAssignments = new StringBuilder();
            StringBuilder fromEntityAssignments = new StringBuilder();

            string toString_1 = "";
            string toString_2 = "";

            // 附加
            string additional = ";";

            // 计算字段名的最大长度，用于对齐
            int maxFieldNameLength = fieldDatas.Max(f => f.fieldName.Length);

            for (int i = 0; i < fieldDatas.Count; i++)
            {
                var typeName = GetFieldTypeString(fieldDatas[i].fieldType, fieldDatas[i].fieldTypeName);

                // 属性
                string attribute = string.Format("        public {0} {1}{2}    //{3}", typeName, fieldDatas[i].fieldName, additional, fieldDatas[i].fieldNotes);
                privateType.AppendFormat(attribute);
                privateType.AppendLine();

                string space = "                ";
                if (i == 0)
                {
                    space = "";
                }

                // 将实例转换为实体
                toEntityAssignments.AppendLine($"{space}{fieldDatas[i].fieldName.PadRight(maxFieldNameLength)} = this.{fieldDatas[i].fieldName},");

                // 解析实体为实例
                fromEntityAssignments.AppendLine($"{space}{fieldDatas[i].fieldName.PadRight(maxFieldNameLength)} = entity.{fieldDatas[i].fieldName},");

                int value = i + 1;
                toString_1 += fieldDatas[i].fieldName + "={" + value + "}";
                if (i < fieldDatas.Count - 1)
                {
                    toString_1 += ",";
                }

                toString_2 += "this." + fieldDatas[i].fieldName;
                if (i < fieldDatas.Count - 1)
                {
                    toString_2 += ",\r\n                ";
                }
            }

            string str = template;
            str = str.Replace("{_0_}", scriptName);
            str = str.Replace("{_1_}", privateType.ToString());
            str = str.Replace("{_2_}", "\"[" + toString_1 + "]\"");
            str = str.Replace("{_3_}", toString_2);
            str = str.Replace("{_4_}", toEntityAssignments.ToString().TrimEnd(','));
            str = str.Replace("{_5_}", fromEntityAssignments.ToString().TrimEnd(','));
            str = str.Replace("{_CREATE_TIME_}", DateTime.UtcNow.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff"));
            return str;
        }

        /// <summary>
        /// 生成ScriptableObjectList脚本
        /// </summary>
        /// <param name="sheet"></param>
        /// <param name="order"></param>
        static async void GenerateScriptList(SheetData sheet, int order)
        {
            string ScriptTemplate = @"//此脚本为工具生成，请勿手动创建 {_CREATE_TIME_} <ExcelTo>
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting;

namespace ReunionMovement
{
    [CreateAssetMenu(fileName = ""{_0_}Container"", menuName = ""ScriptableObjects/{_1_}Container"", order = {_2_})]
    public class {_3_}Container : ScriptableObject
    {
        {_4_}
    }
}
";
            var dataName = sheet.itemClassName;
            var str = GenerateDataScriptList(ScriptTemplate, dataName, sheet.fields, order);
            await FileOperationUtil.SaveFile(scriptOutPutPath + dataName + "Container.cs", str);

            AssetDatabase.Refresh();
        }

        /// <summary>
        /// 创建ScriptableObjectList脚本
        /// </summary>
        /// <param name="template"></param>
        /// <param name="scriptName"></param>
        /// <param name="fieldDatas"></param>
        /// <returns></returns>
        static string GenerateDataScriptList(string template, string scriptName, List<FieldData> fieldDatas, int order)
        {
            StringBuilder privateType = new StringBuilder();
            privateType.AppendLine();

            string additional = ";";

            var typeName = scriptName;

            string attribute = string.Format("        public List<{0}> {1}{2}", scriptName, "configs", additional);
            privateType.AppendFormat(attribute);

            string str = template;
            str = str.Replace("{_0_}", scriptName);
            str = str.Replace("{_1_}", scriptName);
            str = str.Replace("{_2_}", order.ToString());
            str = str.Replace("{_3_}", scriptName);
            str = str.Replace("{_4_}", privateType.ToString());
            str = str.Replace("{_CREATE_TIME_}", DateTime.UtcNow.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff"));
            return str;
        }
        #endregion

        #region 表格 -> ScriptableObject
        [MenuItem("工具箱/表格处理/表格 -> ScriptableObject", false, 2)]
        public static void ExcelToScriptableObject()
        {
            List<string> xlsxFiles = GetAllConfigFiles(toDirSO);

            foreach (var path in xlsxFiles)
            {
                ExcelToScriptableObject(path);
            }

            Log.Debug("表格转为ScriptableObject完成！");
        }

        /// <summary>
        /// Excel 转 ScriptableObject
        /// </summary>
        /// <param name="path"></param>
        public static void ExcelToScriptableObject(string path)
        {
            // 等待编译结束
            if (EditorApplication.isCompiling)
            {
                EditorUtility.DisplayDialog("警告", "等待编译结束。", "OK");
                return;
            }

            // 查看路径是否存在
            if (Directory.Exists(scriptableOutPutPath) == false)
            {
                Directory.CreateDirectory(scriptableOutPutPath);
            }

            // 构造 Excel 工具类
            ExcelUtility excel = new ExcelUtility(path);

            if (excel.ResultSet == null)
            {
                string msg = string.Format("文件“{0}”不是表格！", path);
                Log.Warning(msg);
                return;
            }

            foreach (DataTable table in excel.ResultSet.Tables)
            {
                string tableName = table.TableName.Trim();
                if (tableName.StartsWith("#"))
                {
                    continue; // 忽略以 # 开头的表
                }

                if (table.Rows.Count < tableRows_Max)
                {
                    EditorUtility.ClearProgressBar();
                    string msg = string.Format("无法分析“{0}”。1、检查行数：Excel文件应至少包含三行（第一行：中文名称，第二行：数据类型，第三行：英文名称）!\n2、检查Sheet是否存在多个！", path);
                    EditorUtility.DisplayDialog("ExcelTools", msg, "OK");
                    return;
                }

                // 动态生成 ScriptableObject 文件路径
                string assetPath = scriptableOutPutPath + tableName + "Container.asset";

                // 动态获取容器类类型
                Type containerType = Type.GetType($"ReunionMovement.{tableName}Container, Assembly-CSharp");
                if (containerType == null)
                {
                    Log.Error($"无法获取类型：ReunionMovement.{tableName}Container, Assembly-CSharp");
                    continue;
                }

                // 动态创建容器实例
                ScriptableObject asset = ScriptableObject.CreateInstance(containerType);

                Type configType = Type.GetType($"ReunionMovement.{tableName}, Assembly-CSharp");
                if (configType == null)
                {
                    Log.Error($"无法获取类型：ReunionMovement.{tableName}, Assembly-CSharp");
                    continue;
                }

                // 创建列表
                IList configs = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(configType));

                // 遍历表数据并填充列表
                for (int i = tableRows_Max; i < table.Rows.Count; i++)
                {
                    DataRow row = table.Rows[i];
                    var config = Activator.CreateInstance(configType);
                    if (config == null)
                    {
                        Log.Error($"无法创建实例：{tableName}");
                        continue;
                    }

                    foreach (DataColumn column in table.Columns)
                    {
                        string fieldName = table.Rows[tableRows_3][column].ToString().Trim();
                        if (string.IsNullOrEmpty(fieldName))
                        {
                            continue;
                        }
                        string fieldValue = row[column].ToString().Trim();

                        // 获取字段信息
                        FieldInfo field = config.GetType().GetField(fieldName, BindingFlags.Public | BindingFlags.Instance);
                        if (field != null)
                        {
                            try
                            {
                                object value = ParseValue(fieldValue, field.FieldType);
                                field.SetValue(config, value);
                            }
                            catch (Exception ex)
                            {
                                Log.Error($"字段赋值失败：{fieldName}, 值：{fieldValue}, 错误：{ex.Message}");
                            }
                        }
                        else
                        {
                            Log.Warning($"在类型 '{configType.Name}' 中未找到公共字段：{fieldName}");
                        }
                    }

                    configs.Add(config);
                }

                // 将列表赋值给容器
                FieldInfo configsField = containerType.GetField("configs", BindingFlags.Public | BindingFlags.Instance);
                if (configsField != null)
                {
                    configsField.SetValue(asset, configs);
                }
                else
                {
                    Log.Error($"字段 'configs' 未找到：{tableName}Container");
                }

                // 保存 ScriptableObject
                AssetDatabase.CreateAsset(asset, assetPath);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
        #endregion

        #region 工具
        /// <summary>
        /// 解析单元格字符串到目标类型
        /// </summary>
        private static object ParseValue(string value, Type type)
        {
            if (string.IsNullOrEmpty(value))
            {
                return type.IsValueType ? Activator.CreateInstance(type) : null;
            }

            // 列表类型
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
            {
                var list = (IList)Activator.CreateInstance(type);
                var itemType = type.GetGenericArguments()[0];
                var items = value.Split(new[] { ';', '；' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var item in items)
                {
                    list.Add(ParseValue(item, itemType));
                }
                return list;
            }

            // Vector类型
            if (type == typeof(Vector2) || type == typeof(Vector3) || type == typeof(Vector4))
            {
                string[] parts = value.Trim('(', ')').Split(',');
                if (type == typeof(Vector2) && parts.Length == 2)
                    return new Vector2(float.Parse(parts[0]), float.Parse(parts[1]));
                if (type == typeof(Vector3) && parts.Length == 3)
                    return new Vector3(float.Parse(parts[0]), float.Parse(parts[1]), float.Parse(parts[2]));
                if (type == typeof(Vector4) && parts.Length == 4)
                    return new Vector4(float.Parse(parts[0]), float.Parse(parts[1]), float.Parse(parts[2]), float.Parse(parts[3]));
            }
            
            // Rect类型
            if (type == typeof(Rect))
            {
                string[] parts = value.Trim('(', ')').Split(',');
                 if (parts.Length == 4)
                    return new Rect(float.Parse(parts[0]), float.Parse(parts[1]), float.Parse(parts[2]), float.Parse(parts[3]));
            }

            // Color类型
            if (type == typeof(Color))
            {
                if (ColorUtility.TryParseHtmlString(value, out Color color))
                {
                    return color;
                }
            }

            // 枚举类型
            if (type.IsEnum)
            {
                return Enum.Parse(type, value, true);
            }

            // 基础类型
            return Convert.ChangeType(value, type);
        }

        /// <summary>
        /// 获取所有的xlsx文件路径
        /// </summary>
        /// <returns></returns>
        public static List<string> GetAllConfigFiles(string toDir, string filetype = "*.xlsx")
        {
            List<string> tableList = new List<string>();
            //等待编译结束
            if (EditorApplication.isCompiling)
            {
                EditorUtility.DisplayDialog("警告", "等待编译结束。", "OK");
                return null;
            }
            //查看路径是否存在
            if (Directory.Exists(toDir) == false)
            {
                Directory.CreateDirectory(toDir);
                return null;
            }
            //查找文件目录
            foreach (var path in Directory.GetFiles(toDir, "*", SearchOption.AllDirectories))
            {
                var suffix = Path.GetExtension(path);
                if (suffix != ".xlsx" && suffix != ".xls")
                {
                    string msg = string.Format("文件“{0}”不是表格！", path);
                    Log.Warning(msg);
                    continue;
                }
                tableList.Add(path);
            }

            if (tableList.Count <= 0)
            {
                Log.Error("没有找到表格！");
            }

            return tableList;
        }

        /// <summary>
        /// 获取字段类型
        /// </summary>
        /// <param name="typename"></param>
        /// <returns></returns>
        static FieldTypes GetFieldType(string typeName)
        {
            if (string.IsNullOrEmpty(typeName))
            {
                return FieldTypes.Unknown;
            }

            if (typeMapping.TryGetValue(typeName.Trim(), out FieldTypes type))
            {
                return type;
            }

            return FieldTypes.Unknown;
        }

        /// <summary>
        /// 获取字段类型字符串
        /// </summary>
        /// <param name="fieldTypes"></param>
        /// <param name="fieldTypeName"></param>
        /// <returns></returns>
        static string GetFieldTypeString(FieldTypes fieldTypes, string fieldTypeName)
        {
            // 优先处理需要动态构建名称的自定义类型，并修复返回字面量的BUG
            switch (fieldTypes)
            {
                case FieldTypes.CustomType:
                    return fieldTypeName;
                case FieldTypes.CustomTypeList:
                    return $"List<{fieldTypeName}>";
            }

            // 对于标准类型，使用字典查找以提高性能和可维护性
            if (fieldTypeStringMapping.TryGetValue(fieldTypes, out string typeString))
            {
                return typeString;
            }

            return string.Empty;
        }
        #endregion

        #region 数据结构
        /// <summary>
        /// 单张表数据
        /// </summary>
        public class SheetData
        {
            public DataTable table;
            public string itemClassName;
            public bool keyToMultiValues;
            public bool internalData;
            public List<FieldData> fields = new List<FieldData>();
        }

        /// <summary>
        /// 字段数据
        /// </summary>
        public class FieldData
        {
            public string fieldName;        //字段名称
            public string fieldNotes;       //字段注释
            public int fieldIndex;          //字段索引
            public FieldTypes fieldType;    //字段类型
            public string fieldTypeName;    //字段类型名称
        }
        #endregion
    }
}