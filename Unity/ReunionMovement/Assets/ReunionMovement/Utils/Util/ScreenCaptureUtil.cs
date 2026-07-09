using Cysharp.Threading.Tasks;
using System;
using System.IO;
using UnityEngine;

namespace ReunionMovement.Common.Util
{
    /// <summary>  
    /// 截屏工具类（UniTask 零 GC）
    /// </summary>  
    public static class ScreenCaptureUtil
    {
        /// <summary>  
        /// 截取全屏并保存为Jpg，文件名自动带时间戳
        /// </summary>  
        /// <param name="saveDir">保存目录（可选，默认持久化路径）</param>  
        /// <returns>保存的文件完整路径</returns>  
        public static async UniTask<string> CaptureFullScreenAsync(string saveDir = null)
        {
            try
            {
                string dir = string.IsNullOrEmpty(saveDir) ? PathUtil.GetLocalPath(DownloadType.PersistentImage) : saveDir;
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                string fileName = $"Screenshot_{DateTime.Now:yyyyMMdd_HHmmss}.jpg";
                string filePath = Path.Combine(dir, fileName);

                await UniTask.WaitForEndOfFrame();

                Texture2D tex = null;
                try
                {
                    tex = ScreenCapture.CaptureScreenshotAsTexture();
                    byte[] jpgData = tex.EncodeToJPG();
                    await File.WriteAllBytesAsync(filePath, jpgData);
                }
                finally
                {
                    if (tex != null)
                        UnityEngine.Object.Destroy(tex);
                }

                return filePath;
            }
            catch (Exception ex)
            {
                Log.Error($"[ScreenCaptureUtil] CaptureFullScreenAsync failed: {ex}");
                return null;
            }
        }

        /// <summary>  
        /// 截取指定区域并保存为Jpg
        /// </summary>  
        /// <param name="rect">截取区域（像素）</param>  
        /// <param name="saveDir">保存目录（可选）</param>  
        /// <returns>保存的文件完整路径</returns>  
        public static async UniTask<string> CaptureAreaAsync(Rect rect, string saveDir = null)
        {
            try
            {
                // 统一使用 PathUtil 获取持久化图片目录，保持与 CaptureFullScreenAsync 一致
                string dir = string.IsNullOrEmpty(saveDir) ? PathUtil.GetLocalPath(DownloadType.PersistentImage) : saveDir;
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                string fileName = $"Screenshot_{DateTime.Now:yyyyMMdd_HHmmss}_area.jpg";
                string filePath = Path.Combine(dir, fileName);

                await UniTask.WaitForEndOfFrame();

                Texture2D tex = null;
                try
                {
                    // 边界保护：防止 rect 超出屏幕范围导致 ReadPixels 异常
                    rect.x = Mathf.Max(0, rect.x);
                    rect.y = Mathf.Max(0, rect.y);
                    rect.width = Mathf.Min(rect.width, Screen.width - rect.x);
                    rect.height = Mathf.Min(rect.height, Screen.height - rect.y);
                    if (rect.width <= 0 || rect.height <= 0)
                    {
                        Log.Error($"[ScreenCaptureUtil] 截图区域无效: {rect}");
                        return null;
                    }

                    tex = new Texture2D((int)rect.width, (int)rect.height, TextureFormat.RGB24, false);
                    tex.ReadPixels(rect, 0, 0);
                    tex.Apply();
                    byte[] jpgData = tex.EncodeToJPG();
                    await File.WriteAllBytesAsync(filePath, jpgData);
                }
                finally
                {
                    if (tex != null)
                        UnityEngine.Object.Destroy(tex);
                }

                return filePath;
            }
            catch (Exception ex)
            {
                Log.Error($"[ScreenCaptureUtil] CaptureAreaAsync failed: {ex}");
                return null;
            }
        }

        /// <summary>  
        /// 截取全屏并返回Texture2D（无需保存）  
        /// 注意：返回的Texture2D的生命周期由调用方管理，使用完毕请调用 UnityEngine.Object.Destroy(tex) 以释放内存。
        /// </summary>  
        public static async UniTask<Texture2D> CaptureFullScreenTextureAsync()
        {
            await UniTask.WaitForEndOfFrame();
            return ScreenCapture.CaptureScreenshotAsTexture();
        }
    }
}
