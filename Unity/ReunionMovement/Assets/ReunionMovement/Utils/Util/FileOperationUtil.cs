using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using UnityEngine.Networking;
using UnityEngine;
using Newtonsoft.Json;

namespace ReunionMovement.Common.Util
{
    /// <summary>
    /// 文件操作工具类
    /// </summary>
    public static class FileOperationUtil
    {
        /// <summary>
        /// 获取文件名（包含后缀）或不包含后缀
        /// </summary>
        /// <param name="path"></param>
        /// <param name="withSuffix"></param>
        /// <returns></returns>
        public static string GetFileName(string path, bool withSuffix = true) => withSuffix ? Path.GetFileName(path) : Path.GetFileNameWithoutExtension(path);

        /// <summary>
        /// 获取文件夹下所有文件大小（单位KB，向上取整）
        /// 保留原方法签名但改为向上取整以减少精度损失
        /// </summary>
        /// <param name="path">路径</param>
        /// <returns></returns>
        public static int GetAllFileSize(string path)
        {
            if (!Directory.Exists(path)) return 0;
            long totalBytes = 0;
            foreach (var file in Directory.GetFiles(path, "*", SearchOption.AllDirectories))
            {
                try
                {
                    totalBytes += new FileInfo(file).Length;
                }
                catch (Exception e)
                {
                    Log.Debug($"GetAllFileSize: 无法读取文件大小 {file} -> {e.Message}");
                }
            }
            // 向上取整到KB
            return Convert.ToInt32(Math.Ceiling(totalBytes / 1024.0));
        }

        /// <summary>
        /// 获取文件夹下所有文件大小（字节，精确）
        /// 新增方法，返回精确字节数
        /// </summary>
        public static long GetAllFileSizeBytes(string path)
        {
            if (!Directory.Exists(path)) return 0;
            long totalBytes = 0;
            foreach (var file in Directory.GetFiles(path, "*", SearchOption.AllDirectories))
            {
                try
                {
                    totalBytes += new FileInfo(file).Length;
                }
                catch (Exception e)
                {
                    Log.Debug($"GetAllFileSizeBytes: 无法读取文件大小 {file} -> {e.Message}");
                }
            }
            return totalBytes;
        }

        /// <summary>
        /// 获取指定文件大小（字节）
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public static long GetFileSize(string path) => File.Exists(path) ? new FileInfo(path).Length : 0;

        /// <summary>
        /// 获取文件夹下所有文件名（不含.meta）
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public static List<string> GetAllFilesName(string path)
        {
            List<string> fileList = new List<string>();

            if (!Directory.Exists(path))
            {
                return fileList;
            }

            foreach (var file in Directory.GetFiles(path, "*", SearchOption.AllDirectories))
            {
                if (!file.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
                {
                    fileList.Add(Path.GetFileName(file));
                }
            }

            return fileList;
        }

        /// <summary>
        /// 无视锁文件，直接读bytes，增加存在性检查和异常处理
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public static byte[] ReadAllBytes(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                Log.Debug($"ReadAllBytes: 文件不存在 {path}");
                return Array.Empty<byte>();
            }

            try
            {
                using var fs = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                var length = fs.Length;
                if (length == 0) return Array.Empty<byte>();
                var bytes = new byte[length];
                int offset = 0;
                while (offset < bytes.Length)
                {
                    int read = fs.Read(bytes, offset, bytes.Length - offset);
                    if (read == 0) break;
                    offset += read;
                }
                return bytes;
            }
            catch (Exception e)
            {
                Log.Error($"ReadAllBytes() 路径:{path}, 错误:{e.Message}");
                return Array.Empty<byte>();
            }
        }

        /// <summary>
        /// 保存文件
        /// </summary>
        /// <param name="fullpath">完整路径</param>
        /// <param name="content">内容</param>
        /// <returns></returns>
        public static async Task SaveFile(string fullpath, string content) => await SaveFileAsync(fullpath, Encoding.UTF8.GetBytes(content));

        /// <summary>
        /// 保存文件
        /// </summary>
        /// <param name="fullpath"></param>
        /// <param name="content"></param>
        /// <returns>写入字节数，失败返回 -1</returns>
        public static async Task<int> SaveFileAsync(string fullpath, byte[] content)
        {
            try
            {
                content ??= Array.Empty<byte>();
                var dir = Path.GetDirectoryName(fullpath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                await File.WriteAllBytesAsync(fullpath, content);
                return content.Length;
            }
            catch (Exception e)
            {
                Log.Error($"SaveFile() 路径:{fullpath}, 错误:{e.Message}");
                return -1;
            }
        }

        /// <summary>
        /// 加载Json，增加异常捕获并记录错误
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="fileName"></param>
        /// <returns></returns>
        public static T LoadJson<T>(string fileName)
        {
            var fileAbslutePath = Path.Combine(Application.persistentDataPath, "Json", fileName + ".json");
            if (!File.Exists(fileAbslutePath))
            {
                return default;
            }

            try
            {
                var tempStr = File.ReadAllText(fileAbslutePath);
                return JsonConvert.DeserializeObject<T>(tempStr);
            }
            catch (Exception e)
            {
                Log.Error($"LoadJson() 路径:{fileAbslutePath}, 错误:{e.Message}");
                return default;
            }
        }

        /// <summary>
        /// 保存Json，新增返回写入是否成功的状态（true 成功，false 失败）
        /// </summary>
        /// <param name="jsonStr"></param>
        /// <param name="fileName"></param>
        /// <returns></returns>
        public static async Task<bool> SaveJson(string jsonStr, string fileName)
        {
            var filePath = Path.Combine(Application.persistentDataPath, "Json");
            try
            {
                if (!Directory.Exists(filePath))
                {
                    Directory.CreateDirectory(filePath);
                }
                var fileAbslutePath = Path.Combine(filePath, fileName + ".json");
                await File.WriteAllTextAsync(fileAbslutePath, jsonStr);
                return true;
            }
            catch (Exception e)
            {
                Log.Error($"SaveJson() 路径:{filePath}, 文件:{fileName}, 错误:{e.Message}");
                return false;
            }
        }

        /// <summary>
        /// 游戏开始把StreamingAssets文件复制到持久化目录
        /// </summary>
        /// <param name="filePath"></param>
        /// <param name="fileName"></param>
        /// <returns></returns>
        public static IEnumerator CopyFileToTarget(string filePath, string fileName)
        {
            var originalPath = $"{Application.streamingAssetsPath}/{filePath}/{fileName}";
            var targetDir = $"{Application.persistentDataPath}/{filePath}";
            var targetPath = $"{targetDir}/{fileName}";

            if (!Directory.Exists(targetDir))
            {
                Directory.CreateDirectory(targetDir);
            }

            switch (Application.platform)
            {
                case RuntimePlatform.Android:
                    using (var www = UnityWebRequest.Get(originalPath))
                    {
                        yield return www.SendWebRequest();
                        if (www.result != UnityWebRequest.Result.Success)
                        {
                            Log.Debug($"复制文件失败：{www.error}");
                        }
                        else
                        {
                            try
                            {
                                File.WriteAllBytes(targetPath, www.downloadHandler.data);
                            }
                            catch (Exception e)
                            {
                                Log.Error($"CopyFileToTarget 写入失败:{targetPath} -> {e.Message}");
                            }
                        }
                    }
                    break;
                case RuntimePlatform.IPhonePlayer:
                    originalPath = $"{Application.dataPath}/Raw/{filePath}/{fileName}";
                    if (!File.Exists(targetPath))
                    {
                        try
                        {
                            File.Copy(originalPath, targetPath);
                        }
                        catch (Exception e)
                        {
                            Log.Error($"CopyFileToTarget 复制失败:{originalPath} -> {e.Message}");
                        }
                    }
                    break;
                case RuntimePlatform.WindowsEditor:
                case RuntimePlatform.WindowsPlayer:
                case RuntimePlatform.OSXEditor:
                case RuntimePlatform.OSXPlayer:
                    if (!File.Exists(targetPath))
                    {
                        try
                        {
                            File.Copy(originalPath, targetPath);
                        }
                        catch (Exception e)
                        {
                            Log.Error($"CopyFileToTarget 复制失败:{originalPath} -> {e.Message}");
                        }
                    }
                    break;
            }
            yield return null;
        }
    }
}
