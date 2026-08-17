using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace ReunionMovement.Common.Util
{
    /// <summary>
    /// 通用存档系统 —— JSON 序列化到 persistentDataPath/Data/{saveName}.json。
    /// 特性：
    /// - 原子写入（先写 .tmp 再 File.Replace，崩溃不损坏旧存档）
    /// - AES 加密（EnableEncryption，读旧明文档自动兼容）
    /// - 多槽位（slot 0 与旧路径兼容；slot>0 写 {saveName}_slot{n}.json）
    /// - 自动保存（RegisterAutoSave 注册提供者，后台定时落盘）
    /// - 版本字段（具体存档可自行携带 version 字段并据此迁移）
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

        /// <summary>是否启用 AES 加密（关闭后回退明文 JSON；旧明文存档读取时自动兼容）</summary>
        public static bool EnableEncryption = true;

        /// <summary>
        /// 全静态操作锁：Save/TryLoad/Delete 的读写与自动保存列表变更互斥。
        /// 防止后台线程 Save 与主线程自动保存同槽共写同一 .tmp 产生撕裂文件。
        /// </summary>
        private static readonly object saveLock = new object();

        /// <summary>非法文件名字符缓存（静态初始化一次，避免每字符 Array.IndexOf）</summary>
        private static readonly HashSet<char> invalidNameChars = new HashSet<char>(Path.GetInvalidFileNameChars());

        /// <summary>
        /// 存档版本迁移回调：读取到的存档版本不等于 <see cref="DefaultVersion"/> 时触发。
        /// 签名：存档名、读取到的旧版本、负载 JSON → 迁移后的负载 JSON。
        /// 示例：SaveSystem.OnMigrate += (name, fromVer, payload) => { /* v0.9 → v1.0 字段回填 */ return payload; };
        /// </summary>
        public delegate string SaveMigrateHandler(string saveName, string fromVersion, string payloadJson);
        public static event SaveMigrateHandler OnMigrate;

        /// <summary>读取到的存档版本（TryLoad 后有效，供调用方查询）</summary>
        public static string LastLoadedVersion { get; private set; }

        /// <summary>
        /// 净化存档名：剥离目录部分（../Other → Other，防路径穿越写出 Data 目录）并移除非法文件名字符。
        /// </summary>
        private static string SanitizeSaveName(string saveName)
        {
            var sb = new StringBuilder(saveName.Length);
            foreach (var c in saveName)
            {
                if (c == '\\' || c == '/' || invalidNameChars.Contains(c)) continue;
                sb.Append(c);
            }
            return sb.ToString();
        }

        /// <summary>
        /// 保存数据为 JSON（原子写入：先写临时文件再替换，写一半崩溃不损坏旧存档）。
        /// 返回是否成功（失败已记日志，不抛异常）。
        /// </summary>
        public static bool Save<T>(string saveName, T data, bool prettyPrint = true) where T : class
        {
            return Save(saveName, data, prettyPrint, 0);
        }

        /// <summary>
        /// 保存数据到指定槽位（slot 0 与旧路径兼容；slot>0 写入 slots/{n}/ 独立目录）。
        /// 返回是否成功（磁盘满/路径无效等失败时返回 false 并记日志，不抛异常）。
        /// </summary>
        public static bool Save<T>(string saveName, T data, bool prettyPrint, int slot) where T : class
        {
            if (string.IsNullOrEmpty(saveName)) throw new ArgumentException("存档名不能为空", nameof(saveName));
            if (data == null) throw new ArgumentNullException(nameof(data));

            lock (saveLock)
            {
                string cleanName = SanitizeSaveName(saveName);
                if (cleanName.Length == 0)
                {
                    Log.Error("SaveSystem 保存失败：净化后的存档名为空 {0}", saveName);
                    return false;
                }

                string path = GetSavePath(cleanName, slot);
                EnsureDirectory(Path.GetDirectoryName(path));
                string tmpPath = path + ".tmp";
                try
                {
                    string json = JsonUtility.ToJson(data, prettyPrint);
                    // 版本信封：{version, payload}，供跨版本迁移与损坏诊断
                    json = WrapWithEnvelope(json, DefaultVersion);
                    if (EnableEncryption)
                    {
                        json = EncryptToText(json);
                    }
                    File.WriteAllText(tmpPath, json);
                    // 原子替换：File.Replace 在 Windows 等平台走操作系统原子替换 API，
                    // 替换失败时旧档保持完整（对比旧逻辑 先删后移，Move 失败会同时丢失旧档与新档）。
                    // 目标不存在时改用 Move；平台不支持 Replace 时回退到旧行为（尽力而为）。
                    try
                    {
                        if (File.Exists(path))
                        {
                            File.Replace(tmpPath, path, null);
                        }
                        else
                        {
                            File.Move(tmpPath, path);
                        }
                    }
                    catch (PlatformNotSupportedException)
                    {
                        if (File.Exists(path)) File.Delete(path);
                        File.Move(tmpPath, path);
                    }
                    return true;
                }
                catch (Exception ex)
                {
                    Log.Error("SaveSystem 保存失败 {0}: {1}", saveName, ex.Message);
                    try { if (File.Exists(tmpPath)) File.Delete(tmpPath); } catch { /* 忽略清理失败 */ }
                    return false;
                }
            }
        }

        /// <summary>把数据 JSON 包装为版本信封 {version, payload}（信封本身保持紧凑格式）</summary>
        private static string WrapWithEnvelope(string payloadJson, string version)
        {
            return JsonUtility.ToJson(new SaveEnvelope { version = version, payload = payloadJson }, prettyPrint: false);
        }

        /// <summary>
        /// 解包版本信封：旧格式存档（无信封，直接是数据 JSON）返回 false，payload 保持原样。
        /// </summary>
        private static bool TryUnwrapEnvelope(string json, out SaveEnvelope envelope)
        {
            envelope = null;
            if (string.IsNullOrEmpty(json)) return false;
            try
            {
                // 快速探测：信封以 {"version" 开头；直接 FromJson 旧格式数据也不会误判（数据无 version 字段时解析出 null）
                var candidate = JsonUtility.FromJson<SaveEnvelope>(json);
                if (candidate == null || string.IsNullOrEmpty(candidate.payload)) return false;
                envelope = candidate;
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>尝试加载存档；不存在或反序列化失败返回 false（不抛异常）</summary>
        public static bool TryLoad<T>(string saveName, out T data) where T : class
        {
            return TryLoad(saveName, out data, 0);
        }

        /// <summary>尝试加载指定槽位的存档；不存在或反序列化失败返回 false（不抛异常）</summary>
        public static bool TryLoad<T>(string saveName, out T data, int slot) where T : class
        {
            data = null;
            if (string.IsNullOrEmpty(saveName)) return false;

            lock (saveLock)
            {
                string safeName = SanitizeSaveName(saveName);
                string path = GetSavePath(safeName, slot);
                if (!File.Exists(path))
                {
                    // 兼容升级前旧格式：slot>0 曾与 slot0 同目录写 {saveName}_slot{n}.json，读取时回退
                    path = GetLegacySavePath(safeName, slot);
                    if (!File.Exists(path)) return false;
                }

                try
                {
                    string raw = File.ReadAllText(path);
                    // 解密失败（损坏/密钥不匹配）时按明文解析，保持对旧存档的向后兼容
                    string json = EnableEncryption ? (DecryptFromText(raw) ?? raw) : raw;

                    // 版本信封解包：新格式 {version, payload}；旧格式（无信封）直接按数据解析
                    string payloadJson = json;
                    string loadedVersion = null;
                    if (TryUnwrapEnvelope(json, out var envelope))
                    {
                        payloadJson = envelope.payload;
                        loadedVersion = envelope.version;
                    }
                    LastLoadedVersion = loadedVersion;

                    // 版本迁移：读取到的版本 ≠ DefaultVersion 时交由注册的迁移回调处理
                    if (!string.IsNullOrEmpty(loadedVersion)
                        && loadedVersion != DefaultVersion
                        && OnMigrate != null)
                    {
                        try
                        {
                            payloadJson = OnMigrate(saveName, loadedVersion, payloadJson) ?? payloadJson;
                        }
                        catch (Exception migEx)
                        {
                            Log.Warning("SaveSystem 存档迁移回调异常 {0}: {1}", saveName, migEx.Message);
                        }
                    }

                    data = JsonUtility.FromJson<T>(payloadJson);
                    return data != null;
                }
                catch (Exception ex)
                {
                    Log.Warning("SaveSystem 加载失败 {0}: {1}", saveName, ex.Message);
                    // 损坏隔离：把损坏文件改名备份，避免每次启动反复解析失败，方便排查
                    try { if (File.Exists(path)) File.Move(path, path + ".corrupt"); } catch { /* 忽略 */ }
                    return false;
                }
            }
        }

        /// <summary>加载存档；不存在返回 null</summary>
        public static T Load<T>(string saveName) where T : class
        {
            TryLoad(saveName, out T data);
            return data;
        }

        /// <summary>加载指定槽位存档；不存在返回 null</summary>
        public static T Load<T>(string saveName, int slot) where T : class
        {
            TryLoad(saveName, out T data, slot);
            return data;
        }

        /// <summary>删除存档（不存在则静默忽略）</summary>
        public static void Delete(string saveName)
        {
            Delete(saveName, 0);
        }

        /// <summary>删除指定槽位存档（不存在则静默忽略）</summary>
        public static void Delete(string saveName, int slot)
        {
            if (string.IsNullOrEmpty(saveName)) return;
            lock (saveLock)
            {
                string path = GetSavePath(SanitizeSaveName(saveName), slot);
                try
                {
                    if (File.Exists(path)) File.Delete(path);
                }
                catch (Exception ex)
                {
                    Log.Warning("SaveSystem 删除失败 {0}: {1}", saveName, ex.Message);
                }
            }
        }

        /// <summary>存档是否存在</summary>
        public static bool Exists(string saveName)
        {
            return Exists(saveName, 0);
        }

        /// <summary>指定槽位存档是否存在</summary>
        public static bool Exists(string saveName, int slot)
        {
            if (string.IsNullOrEmpty(saveName)) return false;
            lock (saveLock)
            {
                string safeName = SanitizeSaveName(saveName);
                string path = GetSavePath(safeName, slot);
                if (File.Exists(path)) return true;
                // 兼容旧格式路径（使用与新路径一致的净化名，避免带非法字符存档名新旧路径不一致）
                return File.Exists(GetLegacySavePath(safeName, slot));
            }
        }

        /// <summary>获取存档完整路径（slot 0，不含自动建目录）</summary>
        public static string GetSavePath(string saveName)
        {
            return GetSavePath(saveName, 0);
        }

        /// <summary>
        /// 获取指定槽位存档完整路径（不含自动建目录）。
        /// slot 0 与旧路径兼容；slot&gt;0 使用 slots/{n}/ 独立目录，
        /// 避免 “a_slot1”(slot0) 与 “a”(slot1) 文件名碰撞互相覆盖。
        /// </summary>
        public static string GetSavePath(string saveName, int slot)
        {
            string fileName = SanitizeSaveName(saveName) + FileExtension;
            if (slot > 0)
            {
                return Path.Combine(DataDirectory, "slots", slot.ToString(), fileName);
            }
            return Path.Combine(DataDirectory, fileName);
        }

        /// <summary>旧格式路径（升级前遗留存档读取回退用）</summary>
        private static string GetLegacySavePath(string saveName, int slot)
        {
            string fileName = slot > 0 ? $"{saveName}_slot{slot}{FileExtension}" : saveName + FileExtension;
            return Path.Combine(DataDirectory, fileName);
        }

        /// <summary>确保指定目录存在（Save 写槽位子目录前调用）</summary>
        private static void EnsureDirectory(string directory)
        {
            if (string.IsNullOrEmpty(directory) || Directory.Exists(directory)) return;
            try { Directory.CreateDirectory(directory); }
            catch (Exception ex) { Log.Error("SaveSystem 创建目录失败: {0}", ex.Message); }
        }

        #region 加密（AES-CBC，随机 IV 前置；带 ENC1: 魔法头，旧明文存档自动兼容）
        private const string EncryptionMagic = "ENC1:";
        private static readonly byte[] EncryptionSalt = { 0x52, 0x4D, 0x53, 0x76, 0x31, 0xA5, 0x3C, 0x9F };
        private const string EncryptionPassphrase = "ReunionMovement.SaveSystem.v1";
        private const int EncryptionIvLength = 16;
        private const int EncryptionKeyLength = 32;

        /// <summary>明文 JSON → Base64(AES 密文)。加密不可用（部分平台/裁剪）时回退返回明文。</summary>
        private static string EncryptToText(string plain)
        {
            try
            {
                using var aes = Aes.Create();
                var key = DeriveKey();
                aes.Key = key;
                aes.GenerateIV();
                using var encryptor = aes.CreateEncryptor();
                byte[] plainBytes = Encoding.UTF8.GetBytes(plain);
                byte[] cipher = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);

                byte[] payload = new byte[EncryptionIvLength + cipher.Length];
                Buffer.BlockCopy(aes.IV, 0, payload, 0, EncryptionIvLength);
                Buffer.BlockCopy(cipher, 0, payload, EncryptionIvLength, cipher.Length);
                return EncryptionMagic + Convert.ToBase64String(payload);
            }
            catch (Exception ex)
            {
                Log.Warning("SaveSystem 加密不可用（{0}），本次回退明文存储", ex.Message);
                return plain;
            }
        }

        /// <summary>Base64(AES 密文) → 明文 JSON；非加密文本原样返回，解密失败返回 null。</summary>
        private static string DecryptFromText(string text)
        {
            if (string.IsNullOrEmpty(text) || !text.StartsWith(EncryptionMagic, StringComparison.Ordinal))
            {
                return text; // 旧明文存档
            }
            try
            {
                byte[] payload = Convert.FromBase64String(text.Substring(EncryptionMagic.Length));
                if (payload.Length <= EncryptionIvLength) return null;

                using var aes = Aes.Create();
                aes.Key = DeriveKey();
                byte[] iv = new byte[EncryptionIvLength];
                Buffer.BlockCopy(payload, 0, iv, 0, EncryptionIvLength);
                aes.IV = iv;

                using var decryptor = aes.CreateDecryptor();
                byte[] cipher = new byte[payload.Length - EncryptionIvLength];
                Buffer.BlockCopy(payload, EncryptionIvLength, cipher, 0, cipher.Length);
                byte[] plain = decryptor.TransformFinalBlock(cipher, 0, cipher.Length);
                return Encoding.UTF8.GetString(plain);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>派生固定密钥（PBKDF2，确定性）。注：客户端存档加密仅防随手篡改，非安全边界。</summary>
        private static byte[] DeriveKey()
        {
            using var pdb = new Rfc2898DeriveBytes(EncryptionPassphrase, EncryptionSalt, 1000);
            return pdb.GetBytes(EncryptionKeyLength);
        }
        #endregion

        #region 自动保存（注册数据提供者，后台定时落盘）
        private sealed class AutoSaveEntry
        {
            public string Name;
            public int Slot;
            public Func<object> Provider;
            public float Interval;
            public float Elapsed;
        }

        private static readonly List<AutoSaveEntry> autoSaveEntries = new List<AutoSaveEntry>();
        private static SaveSystemAutoSaveDriver driver;

        /// <summary>Tick 到期待处理条目的复用快照（避免每帧分配）</summary>
        private static readonly List<AutoSaveEntry> autoSaveTickSnapshot = new List<AutoSaveEntry>();

        /// <summary>
        /// 注册自动保存：每 intervalSeconds 秒调用 provider 获取数据并写入存档（同槽位重复注册会替换）。
        /// </summary>
        public static void RegisterAutoSave<T>(string saveName, Func<T> provider, int slot = 0, float intervalSeconds = 30f) where T : class
        {
            if (string.IsNullOrEmpty(saveName) || provider == null)
            {
                Log.Warning("SaveSystem.RegisterAutoSave: saveName 或 provider 无效，注册被忽略");
                return;
            }

            lock (saveLock)
            {
                UnregisterAutoSave(saveName, slot);
                autoSaveEntries.Add(new AutoSaveEntry
                {
                    Name = saveName,
                    Slot = slot,
                    Provider = provider,
                    Interval = Mathf.Max(1f, intervalSeconds),
                    Elapsed = 0f,
                });
            }
            EnsureAutoSaveDriver();
        }

        /// <summary>注销自动保存</summary>
        public static void UnregisterAutoSave(string saveName, int slot = 0)
        {
            lock (saveLock)
            {
                for (int i = autoSaveEntries.Count - 1; i >= 0; i--)
                {
                    var entry = autoSaveEntries[i];
                    if (entry.Name == saveName && entry.Slot == slot)
                    {
                        autoSaveEntries.RemoveAt(i);
                    }
                }
            }
        }

        /// <summary>自动保存驱动（持久化 GameObject，主线程定时 Tick）</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void EnsureAutoSaveDriver()
        {
            if (driver != null) return;
            var go = new GameObject("SaveSystemAutoSaveDriver");
            UnityEngine.Object.DontDestroyOnLoad(go);
            driver = go.AddComponent<SaveSystemAutoSaveDriver>();
        }

        /// <summary>驱动 Tick（由 SaveSystemAutoSaveDriver.Update 调用，逐条目隔离异常）</summary>
        internal static void TickAutoSave(float deltaTime)
        {
            // 锁内仅累加时间与收集到期条目：provider（用户代码）与 Save 在锁外执行，
            // 避免持锁回调用户代码形成锁序环；快照复用列表避免每帧分配
            var due = autoSaveTickSnapshot;
            due.Clear();
            lock (saveLock)
            {
                for (int i = 0; i < autoSaveEntries.Count; i++)
                {
                    var entry = autoSaveEntries[i];
                    entry.Elapsed += deltaTime;
                    if (entry.Elapsed >= entry.Interval)
                    {
                        entry.Elapsed = 0f;
                        due.Add(entry);
                    }
                }
            }

            foreach (var entry in due)
            {
                try
                {
                    var data = entry.Provider();
                    if (data != null)
                    {
                        Save(entry.Name, data, prettyPrint: false, slot: entry.Slot);
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning("SaveSystem 自动保存 {0} 失败: {1}", entry.Name, ex.Message);
                }
            }
        }
        #endregion
    }

    /// <summary>
    /// 存档信封：{version, payload}。payload 为实际数据 JSON。
    /// 跨版本读取时先解信封取版本号，再走 OnMigrate 迁移；旧格式存档无信封直接兼容。
    /// </summary>
    [Serializable]
    public sealed class SaveEnvelope
    {
        public string version;
        public string payload;
    }

    /// <summary>
    /// 自动保存驱动 MonoBehaviour（由 SaveSystem 自动创建，勿手动挂载）
    /// </summary>
    internal sealed class SaveSystemAutoSaveDriver : MonoBehaviour
    {
        private void Update()
        {
            // unscaled：暂停（timeScale=0）期间仍保证关键数据定期落盘
            SaveSystem.TickAutoSave(Time.unscaledDeltaTime);
        }
    }
}
