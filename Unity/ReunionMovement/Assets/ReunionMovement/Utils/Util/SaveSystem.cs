using System;
using System.IO;
using UnityEngine;

namespace ReunionMovement.Common.Util
{
    /// <summary>
    /// 通用存档系统 —— JSON 序列化到 persistentDataPath/Data/{saveName}.json。
    /// 特性：原子写入（先写 .tmp 再替换，崩溃不损坏旧存档）、版本字段（供未来迁移）、
    /// 静态零配置 API（Save / TryLoad / Load / Delete / Exists）。
    /// 说明：与 GameOption（设置，PlayerPrefs）互补 —— 本系统用于游戏数据存档。
    /// </summary>
    public static class SaveSystem
    {
        /// <summary>存档根目录（persistentDataPath/Data）</summary>
        public static readonly string DataDirectory = Path.Combine(Application.persistentDataPath, "Data");

        /// <summary>默认存档版本（具体存档可自行携带 version 字段并据此迁移）</summary>
        public const string DefaultVersion = "1.0.0";

        /// <summary>存档文件后缀</summary>
        public const string FileExtension = ".json";

        /// <summary>
        /// 保存数据为 JSON（原子写入：先写临时文件再替换，写一半崩溃不损坏旧存档）。
        /// </summary>
        public static void Save<T>(string saveName, T data, bool prettyPrint = true) where T : class
        {
            if (string.IsNullOrEmpty(saveName)) throw new ArgumentException("存档名不能为空", nameof(saveName));
            if (data == null) throw new ArgumentNullException(nameof(data));

            EnsureDirectory();
            string path = GetSavePath(saveName);
            string tmpPath = path + ".tmp";
            try
            {
                string json = JsonUtility.ToJson(data, prettyPrint);
                File.WriteAllText(tmpPath, json);
                // 原子替换：先删旧档再移动（WriteAllText 阶段已完成，崩溃时旧档/新档至少保留其一）
                if (File.Exists(path)) File.Delete(path);
                File.Move(tmpPath, path);
            }
            catch (Exception ex)
            {
                Log.Error("SaveSystem 保存失败 {0}: {1}", saveName, ex.Message);
                try { if (File.Exists(tmpPath)) File.Delete(tmpPath); } catch { /* 忽略清理失败 */ }
            }
        }

        /// <summary>尝试加载存档；不存在或反序列化失败返回 false（不抛异常）</summary>
        public static bool TryLoad<T>(string saveName, out T data) where T : class
        {
            data = null;
            if (string.IsNullOrEmpty(saveName)) return false;

            string path = GetSavePath(saveName);
            if (!File.Exists(path)) return false;

            try
            {
                string json = File.ReadAllText(path);
                data = JsonUtility.FromJson<T>(json);
                return data != null;
            }
            catch (Exception ex)
            {
                Log.Warning("SaveSystem 加载失败 {0}: {1}", saveName, ex.Message);
                return false;
            }
        }

        /// <summary>加载存档；不存在返回 null</summary>
        public static T Load<T>(string saveName) where T : class
        {
            TryLoad(saveName, out T data);
            return data;
        }

        /// <summary>删除存档（不存在则静默忽略）</summary>
        public static void Delete(string saveName)
        {
            if (string.IsNullOrEmpty(saveName)) return;
            string path = GetSavePath(saveName);
            try
            {
                if (File.Exists(path)) File.Delete(path);
            }
            catch (Exception ex)
            {
                Log.Warning("SaveSystem 删除失败 {0}: {1}", saveName, ex.Message);
            }
        }

        /// <summary>存档是否存在</summary>
        public static bool Exists(string saveName)
        {
            return !string.IsNullOrEmpty(saveName) && File.Exists(GetSavePath(saveName));
        }

        /// <summary>获取存档完整路径（不含自动建目录）</summary>
        public static string GetSavePath(string saveName)
        {
            return Path.Combine(DataDirectory, saveName + FileExtension);
        }

        /// <summary>确保存档目录存在</summary>
        private static void EnsureDirectory()
        {
            if (!Directory.Exists(DataDirectory))
            {
                try { Directory.CreateDirectory(DataDirectory); }
                catch (Exception ex) { Log.Error("SaveSystem 创建目录失败: {0}", ex.Message); }
            }
        }
    }
}
