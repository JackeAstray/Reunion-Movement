using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Astray.Framework.Runtime;
using UnityEngine;
using UnityEngine.UI;

using Astray.Common;
namespace Astray.Utilities
{
    public static partial class AlgorithmUtil
    {
        #region 查找
        /// <summary>
        /// 泛型二分查找 支持升序、降序查找
        /// </summary>
        /// <typeparam name="T">键的类型</typeparam>
        /// <typeparam name="K">值的类型</typeparam>
        /// <param name="array">数组</param>
        /// <param name="target">目标</param>
        /// <param name="keySelector">键选择器</param>
        /// <returns>返回对象在数组中的序号，若不存在，则返回-1</returns>
        public static int BinarySearch_TryFind<T, K>(IList<T> array, K target, Func<T, K> keySelector, bool descending = false) where K : IComparable<K>
        {
            if (array == null)
            {
                Log.Error("BinarySearch_TryFind: 输入数组不能为空");
                return -1;
            }

            if (keySelector == null)
            {
                Log.Error("BinarySearch_TryFind: 键选择器不能为空");
                return -1;
            }

            int first = 0;
            int last = array.Count - 1;
            while (first <= last)
            {
                int mid = first + ((last - first) >> 1);
                K midKey = keySelector(array[mid]);
                int cmp = midKey.CompareTo(target);

                // 反转比较结果
                if (descending)
                {
                    cmp = -cmp;
                }

                if (cmp > 0)
                {
                    last = mid - 1;
                }
                else if (cmp < 0)
                {
                    first = mid + 1;
                }
                else
                {
                    return mid;
                }
            }
            return -1;
        }

        /// <summary>
        /// 安全地在字典中查找键对应的值
        /// </summary>
        /// <typeparam name="TKey">键的类型</typeparam>
        /// <typeparam name="TValue">值的类型</typeparam>
        /// <param name="dictionary">字典</param>
        /// <param name="key">要查找的键</param>
        /// <param name="value">查找到的值</param>
        /// <returns>是否找到</returns>
        public static bool TryFindInDictionary<TKey, TValue>(Dictionary<TKey, TValue> dictionary, TKey key, out TValue value) where TKey : notnull
        {
            if (dictionary == null)
            {
                Log.Error("TryFindInDictionary: 输入字典不能为空");
                value = default;
                return false;
            }

            return dictionary.TryGetValue(key, out value);
        }
        #endregion


        #region Engine
        /// <summary>
        /// 设置屏幕分辨率。
        ///
        /// 移动端（Android/iOS）不执行并返回 false：设备显示分辨率由系统固定，
        /// <see cref="Screen.SetResolution"/> 不会切换显示模式，只会把 Unity 内部渲染目标
        /// 改成传入尺寸再由系统拉伸铺满整屏 —— 与设备宽高比不一致时画面被拉伸/压扁
        /// （AR 应用会直接破坏相机画面与识别坐标）。
        /// 移动端若要限制渲染分辨率请改用 URP Render Scale（见 Mobile_RPAsset）；
        /// 确实需要该底层语义时请直接调用 <see cref="Screen.SetResolution"/>。
        /// </summary>
        /// <param name="width">屏幕宽度</param>
        /// <param name="height">屏幕高度</param>
        /// <param name="fullScreen">是否全屏显示</param>
        /// <returns>是否真正应用了分辨率设置（移动端恒为 false）。</returns>
        public static bool SetScreen(int width, int height, bool fullScreen)
        {
            if (!PlatformUtil.IsResolutionControllable)
            {
                Log.Warning("SetScreen: 当前平台分辨率由系统控制，已忽略 {0}x{1}（全屏 {2}）", width, height, fullScreen);
                return false;
            }

            Screen.SetResolution(width, height, fullScreen);
            return true;
        }

        /// <summary>
        /// 打开一个URL链接（仅允许 http/https 协议，防止打开恶意链接或本地文件）
        /// </summary>
        /// <param name="url"></param>
        public static void OpenURL(string url)
        {
            if (string.IsNullOrEmpty(url))
            {
                Log.Error("OpenURL: URL 不能为空");
                return;
            }

            // 验证 URL 协议，仅允许 http 和 https
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                Log.Error("OpenURL: 无效的 URL 格式: {0}", url);
                return;
            }

            if (uri.Scheme != "https" && uri.Scheme != "http")
            {
                Log.Error("OpenURL: 不支持的协议 '{0}'，仅允许 http/https", uri.Scheme);
                return;
            }

            Application.OpenURL(url);
        }

        /// <summary>
        /// 退出游戏。
        ///
        /// 编辑器：停止 Play 模式。
        /// WebGL：浏览器不允许脚本关闭页面 —— <see cref="Application.Quit"/> 只会停掉 Web 播放器
        /// （canvas 停在最后一帧），页面/标签页仍然存在。这里仍调用它（这是 WebGL 上最接近"退出"的行为），
        /// 但额外记录警告并返回 false，提示调用方（退出按钮）改为提示用户手动关闭标签页，
        /// 或在 web 模板里用 JS 处理页面级退出。
        /// 其他平台：<see cref="Application.Quit"/>。
        /// </summary>
        /// <returns>是否真正退出了应用（WebGL 恒为 false，因为页面不会被关闭）。</returns>
        public static bool Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
            return true;
#elif UNITY_WEBGL
            Log.Warning("Quit: WebGL 上浏览器不允许脚本关闭页面，Application.Quit 只会停掉播放器而页面仍在；"
                        + "请提示用户手动关闭标签页，或在 web 模板中用 JS 处理页面级退出");
            Application.Quit();
            return false;
#else
            Application.Quit();
            return true;
#endif
        }

        /// <summary>
        /// 网络可用
        /// </summary>
        public static bool NetAvailable
        {
            get
            {
                return Application.internetReachability != NetworkReachability.NotReachable;
            }
        }

        /// <summary>
        /// 是否是无线
        /// </summary>
        public static bool IsWifi
        {
            get
            {
                return Application.internetReachability == NetworkReachability.ReachableViaLocalAreaNetwork;
            }
        }
        #endregion

    }
}
