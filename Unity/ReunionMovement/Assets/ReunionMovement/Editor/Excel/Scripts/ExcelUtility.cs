using ExcelDataReader;
using Newtonsoft.Json;
using ReunionMovement.Common;
using ReunionMovement.Common.Util;
using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace ReunionMovement.EditorTools
{
    /// <summary>
    /// Excel工具类
    /// </summary>
    public class ExcelUtility
    {
        /// <summary>
        /// 表格数据集合
        /// </summary>
        private DataSet mResultSet;

        /// <summary>
        /// 用于获取表格数据
        /// </summary>
        public DataSet ResultSet { get { return mResultSet; } }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="excelFile">Excel file.</param>
        public ExcelUtility(string path)
        {
            //通过文件获取类名
            string className = Path.GetFileNameWithoutExtension(path);

            //检查类名是否有问题
            if (!StringUtil.CheckClassName(className))
            {
                string msg = string.Format("Excel文件“{0}”无效，因为xlsx文件的名称应为类名！", path);
                Log.Error(msg);
                return;
            }

            //拷贝一份文件（File.Copy 需放入 try：Excel 占用文件时会抛异常，必须在 try 内捕获，
            //否则菜单命令直接崩溃且临时文件残留）
            int indexOfDot = path.LastIndexOf('.');
            string tempExcel = string.Concat(path.Substring(0, indexOfDot), "_temp_", DateTime.Now.Ticks.ToString(), path.Substring(indexOfDot, path.Length - indexOfDot));
            string extension = Path.GetExtension(path).ToLowerInvariant();

            //读取拷贝的文件（try-finally 确保临时文件一定被清理）
            Stream stream = null;
            IExcelDataReader reader = null;
            try
            {
                File.Copy(path, tempExcel);
                stream = File.OpenRead(tempExcel);
                // .xls 走二进制读取器，.xlsx 走 OpenXml 读取器（CreateOpenXmlReader 读 .xls 必然抛异常）
                if (extension == ".xls")
                    reader = ExcelReaderFactory.CreateBinaryReader(stream);
                else
                    reader = ExcelReaderFactory.CreateOpenXmlReader(stream);
                mResultSet = reader.AsDataSet();
            }
            catch (Exception ex)
            {
                string msg = string.Format("无法打开\u201C{0}\u201D。也许您应该先关闭Excel应用程序（文件被占用或格式不支持）！错误: {1}", path, ex.Message);
                Log.Error(msg);
                return;
            }
            finally
            {
                reader?.Dispose();
                stream?.Close();
                if (File.Exists(tempExcel))
                {
                    File.Delete(tempExcel);
                }
            }
        }

        /// <summary>
        /// 转换为实体类列表
        /// </summary>
        public List<T> ConvertToList<T>()
        {
            // 构造函数可能因校验失败而提前返回，此时 mResultSet 为 null
            if (mResultSet == null) return null;

            List<T> list = new List<T>();
            Type type = typeof(T);

            if (mResultSet.Tables.Count < 1) return null;
            DataTable mSheet = mResultSet.Tables[0];
            if (mSheet.Rows.Count < 1) return null;

            // 预缓存 PropertyInfo，避免每行每列重复反射
            var propertyCache = new Dictionary<string, PropertyInfo>(mSheet.Columns.Count);
            foreach (DataColumn column in mSheet.Columns)
            {
                var prop = type.GetProperty(column.ColumnName);
                if (prop != null && prop.CanWrite)
                {
                    propertyCache[column.ColumnName] = prop;
                }
            }

            foreach (DataRow row in mSheet.Rows)
            {
                T item = Activator.CreateInstance<T>();
                foreach (DataColumn column in mSheet.Columns)
                {
                    if (propertyCache.TryGetValue(column.ColumnName, out PropertyInfo property))
                    {
                        property.SetValue(item, Convert.ChangeType(row[column], property.PropertyType));
                    }
                }
                list.Add(item);
            }

            return list;
        }

        /// <summary>
        /// 转换为Json
        /// </summary>
        /// <param name="JsonPath">Json文件路径</param>
        public async UniTask ConvertToJson(string JsonPath)
        {
            var json = GetJson();
            //写入文件
            await FileOperationUtil.SaveFile(JsonPath, json);
        }

        /// <summary>
        /// 后补的接口，适配旧的调用
        /// </summary>
        /// <param name="tableName"></param>
        /// <param name="IdX"></param>
        /// <param name="IdY"></param>
        /// <returns></returns>
        public string GetJson()
        {
            int x = -1;
            int y = -1;
            var json = GetJson(ref x, ref y);
            return json;
        }

        /// <summary>
        /// 获取json
        /// </summary>
        /// <returns></returns>
        public string GetJson(ref int IdX, ref int IdY)
        {
            IdX = -1;
            IdY = -1;

            //判断Excel文件中是否存在数据表
            if (mResultSet == null || mResultSet.Tables == null)
            {
                return "";
            }

            //判断Excel文件中是否存在数据表
            if (mResultSet.Tables.Count < 1)
            {
                return "";
            }

            //默认读取第一个数据表
            DataTable mSheet = mResultSet.Tables[0];

            //判断数据表内是否存在数据
            if (mSheet.Rows.Count < 1)
            {
                return "";
            }

            //准备一个列表存储整个表的数据
            List<Dictionary<string, object>> table = new List<Dictionary<string, object>>();
            /************Keep * Mode 保留带*的行列 ********************/
            /*
             *    Id   |   xxx |
             *    1    |   xxx |
             *    2    |   xxx |
             */

            //字段名称
            List<object> fieldNameRowDatas = new List<object>();
            //字段类型
            List<object> fieldTypeRowDatas = new List<object>();
            //第一行为备注，
            //寻找到id字段行数，以下全为数据
            int skipRowCount = -1;
            int skipColCount = -1;

            //这里skip 防止有人在 备注行直接输入id
            int skipLine = 1;

            for (int i = skipLine; i < 10 && skipColCount == -1; i++)
            {
                var rows = this.GetRowDatas(i);
                //遍历rows
                for (int j = 0; j < rows.Count; j++)
                {
                    if (rows[j].Equals("Id"))
                    {
                        skipRowCount = i;
                        skipColCount = j;
                        fieldNameRowDatas = rows;
                        //获取字段类型
                        var rowtype = this.GetRowDatas(i - 1);
                        fieldTypeRowDatas = rowtype;
                        break;
                    }
                }
            }


            if (skipRowCount == -1)
            {
                Log.Error("表格数据可能有错，没发现Id字段,请检查");
                return "{}";
            }

            int count = mSheet.Rows.Count;

            IdX = skipColCount;
            IdY = skipRowCount;

            //读取数据
            for (int i = skipRowCount + 1; i < mSheet.Rows.Count; i++)
            {
                //准备一个字典存储每一行的数据
                Dictionary<string, object> row = new Dictionary<string, object>();
                //
                for (int j = skipColCount; j < mSheet.Columns.Count; j++)
                {
                    // 防止字段行/类型行比数据行列数少导致越界
                    if (j >= fieldNameRowDatas.Count || j >= fieldTypeRowDatas.Count)
                    {
                        Log.Error(string.Format("表格数据列索引越界：[{0},{1}]，字段名行有{2}列，类型行有{3}列", i, j, fieldNameRowDatas.Count, fieldTypeRowDatas.Count));
                        continue;
                    }

                    string field = fieldNameRowDatas[j].ToString();
                    //跳过空字段
                    if (string.IsNullOrEmpty(field))
                    {
                        continue;
                    }

                    //Key-Value对应
                    var rowdata = mSheet.Rows[i][j];
                    //根据null判断
                    if (rowdata == null)
                    {
                        string msg = string.Format("表格数据为空：[{0},{1}]", i, j);
                        Log.Error(msg);
                        continue;
                    }

                    var fieldType = fieldTypeRowDatas[j].ToString().ToLower();
                    if (rowdata is DBNull) //空类型判断，赋默认值
                    {
                        if (fieldType == "int" || fieldType == "float" || fieldType == "double")
                        {
                            row[field] = 0;
                        }
                        else if (fieldType == "string")
                        {
                            row[field] = "";
                        }
                        else if (fieldType == "bool")
                        {
                            row[field] = false;
                        }
                        else if (IsArrayType(fieldType)) //空数组 → 产出真正的空 JSON 数组
                        {
                            row[field] = new List<object>();
                        }
                    }
                    else
                    {
                        // 数组字段：解析为真正的 List，JsonConvert 直接输出合法的 JSON 数组，
                        // 无需任何全局字符串替换（避免破坏含引号/方括号的合法字符串）
                        if (IsArrayType(fieldType))
                        {
                            row[field] = ParseArrayField(rowdata.ToString(), fieldType);
                        }
                        else if (fieldType == "int" || fieldType == "int32")
                        {
                            if (int.TryParse(rowdata.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
                            {
                                row[field] = value;
                            }
                            else
                            {
                                row[field] = 0;
                                Log.Error(string.Format("表格数据出错：{0}-{1}，值：{2}", i, j, rowdata));
                            }
                        }
                        else if (fieldType == "float")
                        {
                            if (float.TryParse(rowdata.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
                            {
                                row[field] = value;
                            }
                            else
                            {
                                row[field] = 0f;
                                Log.Error(string.Format("表格数据出错：{0}-{1}，值：{2}", i, j, rowdata));
                            }
                        }
                        else if (fieldType == "double")
                        {
                            if (double.TryParse(rowdata.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
                            {
                                row[field] = value;
                            }
                            else
                            {
                                row[field] = 0d;
                                Log.Error(string.Format("表格数据出错：{0}-{1}，值：{2}", i, j, rowdata));
                            }
                        }
                        else if (fieldType == "string")
                        {
                            row[field] = rowdata.ToString();
                        }
                        else
                        {
                            row[field] = rowdata;
                        }
                    }
                }

                //添加到表数据中
                if (row.Count > 0)
                {
                    table.Add(row);
                }
            }
            // 直接序列化：数组字段已解析为真正的 List，输出即为合法的 JSON 数组，
            // 无需任何字符串后处理（避免全局 Replace 破坏含引号/方括号的合法字符串）
            return JsonConvert.SerializeObject(table);
        }

        /// <summary>
        /// 判断字段类型是否为数组类型（兼容 [int] / int[] / string[] 等写法）
        /// </summary>
        private static bool IsArrayType(string fieldType)
        {
            return fieldType.Contains("[") || fieldType == "string[]";
        }

        /// <summary>
        /// 将数组字段字符串解析为真正的 List（产出 JSON 数组）。
        /// 支持 [a,b,c] / a,b,c / a;b;c 分隔，元素按列类型转换；
        /// 未知数组类型保留字符串元素。
        /// </summary>
        private static object ParseArrayField(string raw, string fieldType)
        {
            var result = new List<object>();
            if (string.IsNullOrEmpty(raw)) return result;

            var value = raw.Trim();
            // 兼容外层带引号写法："[a,b]"
            if (value.Length >= 2 && value[0] == '"' && value[value.Length - 1] == '"')
                value = value.Substring(1, value.Length - 2).Trim();
            if (value.StartsWith("[") && value.EndsWith("]"))
                value = value.Substring(1, value.Length - 2);
            if (string.IsNullOrWhiteSpace(value)) return result;

            var items = value.Split(new[] { ';', '；', ',', '，' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var item in items)
            {
                var t = item.Trim().Trim('"');
                if (fieldType == "string[]" || fieldType == "[string]")
                {
                    result.Add(t);
                }
                else if (fieldType == "int[]" || fieldType == "[int]" || fieldType == "int32[]")
                {
                    result.Add(int.TryParse(t, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : 0);
                }
                else if (fieldType == "float[]" || fieldType == "[float]")
                {
                    result.Add(float.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : 0f);
                }
                else if (fieldType == "double[]" || fieldType == "[double]")
                {
                    result.Add(double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : 0d);
                }
                else if (fieldType == "long[]" || fieldType == "[long]")
                {
                    result.Add(long.TryParse(t, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : 0L);
                }
                else if (fieldType == "bool[]" || fieldType == "[bool]")
                {
                    result.Add(bool.TryParse(t, out var v) && v);
                }
                else
                {
                    // 未知数组类型：保留字符串元素
                    result.Add(t);
                }
            }
            return result;
        }

        /// <summary>
        /// 获取一行数据
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        public List<object> GetRowDatas(int index)
        {
            List<object> list = new List<object>();

            //判断Excel文件中是否存在数据表
            if (mResultSet.Tables.Count < 1)
            {
                return list;
            }

            //默认读取第一个数据表
            DataTable mSheet = mResultSet.Tables[0];
            //判断数据表内是否存在数据
            if (mSheet.Rows.Count <= index)
            {
                return list;
            }

            //读取数据
            int colCount = mSheet.Columns.Count;
            for (int j = 0; j < colCount; j++)
            {
                object item = mSheet.Rows[index][j];
                list.Add(item);
            }


            return list;
        }

        /// <summary>
        /// 转换为CSV
        /// </summary>
        /// <param name="CSVPath"></param>
        /// <param name="encoding"></param>
        public void ConvertToCSV(string CSVPath, Encoding encoding)
        {
            //判断Excel文件中是否存在数据表
            if (mResultSet.Tables.Count < 1) return;

            //默认读取第一个数据表
            DataTable mSheet = mResultSet.Tables[0];

            //判断数据表内是否存在数据
            if (mSheet.Rows.Count < 1) return;

            //读取数据表行数和列数
            int rowCount = mSheet.Rows.Count;
            int colCount = mSheet.Columns.Count;

            //创建一个StringBuilder存储数据
            StringBuilder stringBuilder = new StringBuilder();

            //读取数据
            for (int i = 0; i < rowCount; i++)
            {
                for (int j = 0; j < colCount; j++)
                {
                    //使用","分割每一个数值
                    stringBuilder.Append(mSheet.Rows[i][j] + ",");
                }

                //使用换行符分割每一行
                stringBuilder.Append("\r\n");
            }

            //写入文件
            using (FileStream fileStream = new FileStream(CSVPath, FileMode.Create, FileAccess.Write))
            {
                using (TextWriter textWriter = new StreamWriter(fileStream, encoding))
                {
                    textWriter.Write(stringBuilder.ToString());
                }
            }
        }


        /// <summary>
        /// 转换为lua
        /// </summary>
        /// <param name="luaPath"></param>
        /// <param name="encoding"></param>
        public void ConvertToLua(string luaPath, Encoding encoding)
        {
            //判断Excel文件中是否存在数据表
            if (mResultSet.Tables.Count < 1)
                return;

            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.Append("local datas = {");
            stringBuilder.Append("\r\n");

            //读取数据表
            foreach (DataTable mSheet in mResultSet.Tables)
            {
                //判断数据表内是否存在数据
                if (mSheet.Rows.Count < 1)
                    continue;

                //读取数据表行数和列数
                int rowCount = mSheet.Rows.Count;
                int colCount = mSheet.Columns.Count;

                //准备一个列表存储整个表的数据
                List<Dictionary<string, object>> table = new List<Dictionary<string, object>>();

                //读取数据
                for (int i = 1; i < rowCount; i++)
                {
                    //准备一个字典存储每一行的数据
                    Dictionary<string, object> row = new Dictionary<string, object>();
                    for (int j = 0; j < colCount; j++)
                    {
                        //读取第1行数据作为表头字段
                        string field = mSheet.Rows[0][j].ToString();
                        //Key-Value对应
                        row[field] = mSheet.Rows[i][j];
                    }
                    //添加到表数据中
                    table.Add(row);
                }
                stringBuilder.Append(string.Format("\t\"{0}\" = ", mSheet.TableName));
                stringBuilder.Append("{\r\n");
                foreach (Dictionary<string, object> dic in table)
                {
                    stringBuilder.Append("\t\t{\r\n");
                    foreach (string key in dic.Keys)
                    {
                        if (dic[key].GetType().Name == "String")
                            stringBuilder.Append(string.Format("\t\t\t\"{0}\" = \"{1}\",\r\n", key, dic[key]));
                        else
                            stringBuilder.Append(string.Format("\t\t\t\"{0}\" = {1},\r\n", key, dic[key]));
                    }
                    stringBuilder.Append("\t\t},\r\n");
                }
                stringBuilder.Append("\t}\r\n");
            }

            stringBuilder.Append("}\r\n");
            stringBuilder.Append("return datas");

            //写入文件
            using (FileStream fileStream = new FileStream(luaPath, FileMode.Create, FileAccess.Write))
            {
                using (TextWriter textWriter = new StreamWriter(fileStream, encoding))
                {
                    textWriter.Write(stringBuilder.ToString());
                }
            }
        }


        /// <summary>
        /// 导出为Xml
        /// </summary>
        /// <param name="XmlFile"></param>
        public void ConvertToXml(string XmlFile)
        {
            //判断Excel文件中是否存在数据表
            if (mResultSet.Tables.Count < 1) return;

            //默认读取第一个数据表
            DataTable mSheet = mResultSet.Tables[0];

            //判断数据表内是否存在数据
            if (mSheet.Rows.Count < 1) return;

            //读取数据表行数和列数
            int rowCount = mSheet.Rows.Count;
            int colCount = mSheet.Columns.Count;

            //创建一个StringBuilder存储数据
            StringBuilder stringBuilder = new StringBuilder();
            //创建Xml文件头
            stringBuilder.Append("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
            stringBuilder.Append("\r\n");
            //创建根节点
            stringBuilder.Append("<Table>");
            stringBuilder.Append("\r\n");
            //读取数据
            for (int i = 1; i < rowCount; i++)
            {
                //创建子节点
                stringBuilder.Append("  <Row>");
                stringBuilder.Append("\r\n");
                for (int j = 0; j < colCount; j++)
                {
                    stringBuilder.Append("   <" + mSheet.Rows[0][j].ToString() + ">");
                    stringBuilder.Append(mSheet.Rows[i][j].ToString());
                    stringBuilder.Append("</" + mSheet.Rows[0][j].ToString() + ">");
                    stringBuilder.Append("\r\n");
                }

                //使用换行符分割每一行
                stringBuilder.Append("  </Row>");
                stringBuilder.Append("\r\n");
            }

            //闭合标签
            stringBuilder.Append("</Table>");
            //写入文件
            using (FileStream fileStream = new FileStream(XmlFile, FileMode.Create, FileAccess.Write))
            {
                using (TextWriter textWriter = new StreamWriter(fileStream, Encoding.GetEncoding("utf-8")))
                {
                    textWriter.Write(stringBuilder.ToString());
                }
            }
        }

        /// <summary>
        /// 设置目标实例的属性
        /// </summary>
        private void SetTargetProperty(object target, string propertyName, object propertyValue)
        {
            //获取类型
            Type mType = target.GetType();
            //获取属性集合
            PropertyInfo[] mPropertys = mType.GetProperties();
            foreach (PropertyInfo property in mPropertys)
            {
                if (property.Name == propertyName)
                {
                    property.SetValue(target, Convert.ChangeType(propertyValue, property.PropertyType), null);
                }
            }
        }
    }
}
