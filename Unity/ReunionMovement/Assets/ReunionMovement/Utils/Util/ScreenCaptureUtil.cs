using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace ReunionMovement.Common.Util
{
    /// <summary>  
    /// 截屏工具类  
    /// </summary>  
    public static class ScreenCaptureUtil
    {
        /// <summary>  
        /// 截取全屏并保存为Jpg，文件名自动带时间戳
        /// </summary>  
        /// <param name="saveDir">保存目录（可选，默认持久化路径）</param>  
        /// <returns>保存的文件完整路径</returns>  
        public static async Task<string> CaptureFullScreenAsync(string saveDir = null)
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

                await WaitForEndOfFrameAsync();

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
                Debug.LogError($"[ScreenCaptureUtil] CaptureFullScreenAsync failed: {ex}");
                return null;
            }
        }

        /// <summary>  
        /// 截取指定区域并保存为Jpg
        /// </summary>  
        /// <param name="rect">截取区域（像素）</param>  
        /// <param name="saveDir">保存目录（可选）</param>  
        /// <returns>保存的文件完整路径</returns>  
        public static async Task<string> CaptureAreaAsync(Rect rect, string saveDir = null)
        {
            try
            {
                string dir = string.IsNullOrEmpty(saveDir) ? Application.persistentDataPath : saveDir;
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                string fileName = $"Screenshot_{DateTime.Now:yyyyMMdd_HHmmss}_area.jpg";
                string filePath = Path.Combine(dir, fileName);

                await WaitForEndOfFrameAsync();

                Texture2D tex = null;
                try
                {
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
                Debug.LogError($"[ScreenCaptureUtil] CaptureAreaAsync failed: {ex}");
                return null;
            }
        }

        /// <summary>  
        /// 截取全屏并返回Texture2D（无需保存）  
        /// </summary>  
        public static async Task<Texture2D> CaptureFullScreenTextureAsync()
        {
            await WaitForEndOfFrameAsync();
            return ScreenCapture.CaptureScreenshotAsTexture();
        }

        /// <summary>  
        /// 等待帧结束的Task（协程转Task）  
        /// </summary>  
        private static Task WaitForEndOfFrameAsync()
        {
            var tcs = new TaskCompletionSource<bool>();
            void WaitForEndOfFrameCoroutine()
            {
                var waitForEndOfFrame = new WaitForEndOfFrame();
                MonoBehaviourHelper.Instance.StartCoroutine(WaitForEndOfFrameCoroutineImpl(waitForEndOfFrame, tcs));
            }

            WaitForEndOfFrameCoroutine();
            return tcs.Task;
        }

        /// <summary>
        /// 协程实现等待帧结束的逻辑
        /// </summary>
        /// <param name="waitForEndOfFrame"></param>
        /// <param name="tcs"></param>
        /// <returns></returns>
        private static IEnumerator WaitForEndOfFrameCoroutineImpl(WaitForEndOfFrame waitForEndOfFrame, TaskCompletionSource<bool> tcs)
        {
            yield return waitForEndOfFrame;
            tcs.SetResult(true);
        }
    }

    /// <summary>  
    /// MonoBehaviour辅助类，用于启动协程  
    /// </summary>  
    public class MonoBehaviourHelper : MonoBehaviour
    {
        private static MonoBehaviourHelper instance;
        public static MonoBehaviourHelper Instance
        {
            get
            {
                if (instance == null)
                {
                    var obj = new GameObject("MonoBehaviourHelper");
                    instance = obj.AddComponent<MonoBehaviourHelper>();
                    DontDestroyOnLoad(obj);
                }
                return instance;
            }
        }
    }
}
