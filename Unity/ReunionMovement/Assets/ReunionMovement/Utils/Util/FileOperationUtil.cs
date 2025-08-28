using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        /// 获取文件协议
        /// </summary>
        public static string GetFileProtocol => "file://";

        /// <summary>
        /// 获取文件名（包含后缀）或不包含后缀
        /// </summary>
        /// <param name="path"></param>
        /// <param name="withSuffix"></param>
        /// <returns></returns>
        public static string GetFileName(string path, bool withSuffix = true) => withSuffix ? Path.GetFileName(path) : Path.GetFileNameWithoutExtension(path);

        /// <summary>
        /// 获取文件夹下所有文件大小（单位KB）
        /// </summary>
        /// <param name="path">路径</param>
        /// <returns></returns>
        public static int GetAllFileSize(string path)
        {
            if (!Directory.Exists(path)) return 0;
            int sum = 0;
            foreach (var file in Directory.GetFiles(path, "*", SearchOption.AllDirectories))
            {
                sum += Convert.ToInt32(new FileInfo(file).Length / 1024);
            }
            return sum;
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
        /// 无视锁文件，直接读bytes
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public static byte[] ReadAllBytes(string path)
        {
            using var fs = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            var bytes = new byte[fs.Length];
            fs.Read(bytes, 0, bytes.Length);
            return bytes;
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
        /// <returns></returns>
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
        /// 加载Json
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
            var tempStr = File.ReadAllText(fileAbslutePath);
            return JsonConvert.DeserializeObject<T>(tempStr);
        }

        /// <summary>
        /// 保存Json
        /// </summary>
        /// <param name="jsonStr"></param>
        /// <param name="fileName"></param>
        /// <returns></returns>
        public static async Task SaveJson(string jsonStr, string fileName)
        {
            var filePath = Path.Combine(Application.persistentDataPath, "Json");
            if (!Directory.Exists(filePath))
            {
                Directory.CreateDirectory(filePath);
            }
            var fileAbslutePath = Path.Combine(filePath, fileName + ".json");
            await File.WriteAllTextAsync(fileAbslutePath, jsonStr);
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
                            File.WriteAllBytes(targetPath, www.downloadHandler.data);
                        }
                    }
                    break;
                case RuntimePlatform.IPhonePlayer:
                    originalPath = $"{Application.dataPath}/Raw/{filePath}/{fileName}";
                    if (!File.Exists(targetPath))
                    {
                        File.Copy(originalPath, targetPath);
                    }
                    break;
                case RuntimePlatform.WindowsEditor:
                case RuntimePlatform.WindowsPlayer:
                case RuntimePlatform.OSXEditor:
                case RuntimePlatform.OSXPlayer:
                    if (!File.Exists(targetPath))
                    {
                        File.Copy(originalPath, targetPath);
                    }
                    break;
            }
            yield return null;
        }
    }
}
