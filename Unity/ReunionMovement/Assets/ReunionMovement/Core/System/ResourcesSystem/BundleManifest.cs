using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using ReunionMovement.Common.Util.Download;
using UnityEngine;

namespace ReunionMovement.Core.Resources
{
    [Serializable]
    public class BundleInfo
    {
        public string name;
        public string fileName;
        public string[] dependencies;
        public string version;
        public string url; // 可选远程url
    }

    [Serializable]
    public class BundleManifest
    {
        public List<BundleInfo> bundles = new List<BundleInfo>();

        public BundleInfo GetBundle(string name)
        {
            if (bundles == null) return null;
            return bundles.Find(b => string.Equals(b.name, name, StringComparison.OrdinalIgnoreCase) || string.Equals(b.fileName, name, StringComparison.OrdinalIgnoreCase));
        }

        public static BundleManifest FromJson(string json)
        {
            try
            {
                return JsonUtility.FromJson<BundleManifest>(json);
            }
            catch
            {
                return null;
            }
        }

        public string ToJson()
        {
            return JsonUtility.ToJson(this, true);
        }
    }

    public class BundleVersionManager
    {
        private BundleManifest manifest;
        public BundleManifest Manifest => manifest;

        public void LoadLocalManifest(string fullPath)
        {
            if (string.IsNullOrEmpty(fullPath) || !File.Exists(fullPath)) return;
            try
            {
                var json = File.ReadAllText(fullPath);
                manifest = BundleManifest.FromJson(json);
            }
            catch (Exception)
            {
                manifest = null;
            }
        }

        public async Task<bool> LoadRemoteManifestAsync(string url)
        {
            if (string.IsNullOrEmpty(url)) return false;
            try
            {
                var resp = await HTTPHelper.Get(url, null, 6);
                if (resp == null || resp.didError)
                {
                    return false;
                }
                manifest = BundleManifest.FromJson(resp.responseText);
                return manifest != null;
            }
            catch
            {
                return false;
            }
        }

        public bool NeedsUpdate(string bundleName, string localVersionFile)
        {
            if (manifest == null) return false;
            var info = manifest.GetBundle(bundleName);
            if (info == null) return false;

            // 本地版本文件是包名.version 内容为版本字符串
            if (string.IsNullOrEmpty(localVersionFile) || !File.Exists(localVersionFile))
            {
                return true; // 无本地版本，认为需要下载
            }

            try
            {
                var localVer = File.ReadAllText(localVersionFile).Trim();
                return !string.Equals(localVer, info.version, StringComparison.Ordinal);
            }
            catch
            {
                return true;
            }
        }

        public void SaveLocalVersion(string localVersionFile, string version)
        {
            try
            {
                var dir = Path.GetDirectoryName(localVersionFile);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(localVersionFile, version ?? string.Empty);
            }
            catch { }
        }
    }
}