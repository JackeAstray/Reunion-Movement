#if UNITY_ANDROID && UNITY_EDITOR
using System;
using System.IO;
using UnityEditor.Android;
using UnityEngine;

namespace Gear.EditorTools.Build
{
    /// <summary>
    /// Android 构建后处理：把 VIBRATE 权限注入 Gradle 主清单。
    ///
    /// <para>
    /// 不使用 <c>Assets/Plugins/Android/AndroidManifest.xml</c>：那会替换 Unity 默认主清单，
    /// 需要自己维护 Activity / theme / 启动项，升级 Unity 时容易漏。这里只在生成后的
    /// Gradle 工程里追加一行 uses-permission，不动其他内容。
    /// </para>
    ///
    /// <para>只在 Android 为当前构建目标时编译（<c>#if UNITY_ANDROID</c>）。</para>
    /// </summary>
    public sealed class AndroidVibratePermission : IPostGenerateGradleAndroidProject
    {
        private const string Permission = "android.permission.VIBRATE";
        private const string PermissionTag = "<uses-permission android:name=\"android.permission.VIBRATE\" />";

        public int callbackOrder => 0;

        public void OnPostGenerateGradleAndroidProject(string path)
        {
            string manifestPath = Path.Combine(path, "src", "main", "AndroidManifest.xml");
            if (!File.Exists(manifestPath))
            {
                Debug.LogWarning($"[GearBuild] 未找到 AndroidManifest.xml: {manifestPath}");
                return;
            }

            string manifest = File.ReadAllText(manifestPath);
            if (manifest.Contains(Permission))
            {
                return;
            }

            int index = manifest.LastIndexOf("</manifest>", StringComparison.Ordinal);
            if (index < 0)
            {
                Debug.LogWarning("[GearBuild] AndroidManifest.xml 缺少 </manifest>，无法注入 VIBRATE");
                return;
            }

            manifest = manifest.Insert(index, "    " + PermissionTag + "\n");
            File.WriteAllText(manifestPath, manifest);
            Debug.Log("[GearBuild] 已注入 android.permission.VIBRATE");
        }
    }
}
#endif
