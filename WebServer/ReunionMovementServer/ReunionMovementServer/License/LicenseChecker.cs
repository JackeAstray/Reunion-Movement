using Newtonsoft.Json;
using Serilog;
using System.Security.Cryptography;
using System.Text;

namespace ReunionMovementServer.License
{
    /// <summary>
    /// 简单的本地许可/时间篡改检测器
    /// </summary>
    public class LicenseChecker
    {
        // 到期时间（Local）
        private static readonly DateTime ExpiryLocal = new DateTime(2099, 12, 1, 0, 0, 0, DateTimeKind.Local);

        // 两个不同位置，增加删除/修改难度
        // \<YourProject>\bin\Debug\net8.0\license.bin
        private static readonly string AppDirPath = Path.Combine(AppContext.BaseDirectory, "license.bin");
        // C:\ProgramData\VPPIOS_Server\license.bin on Windows
        // ~/.vppios_server/license.bin on Linux/macOS
        private static readonly string CommonDataPath = GetCrossPlatformCommonDataPath();

        /// <summary>
        /// 许可状态结构
        /// </summary>
        private class LicenseState
        {
            public long CreatedTicks { get; set; }
            public long LastSeenTicks { get; set; }
        }

        /// <summary>
        /// 主入口：在 Program.Main 启动早期调用
        /// </summary>
        public static void CheckOrExit()
        {
            var now = DateTime.Now;

            // 先检查到期
            if (now > ExpiryLocal)
            {
                Log.Information("程序授权已过期，正在退出。");
                Environment.Exit(1);
            }

            // 读取各位置状态，选择最新的作为可信来源
            LicenseState state = null;
            try
            {
                var s1 = LoadState(AppDirPath);
                var s2 = LoadState(CommonDataPath);
                state = ChooseNewer(s1, s2);
            }
            catch (Exception ex)
            {
                Log.Warning("读取许可状态时出错: {Message}", ex.Message);
            }

            // 如果没有任何历史记录，创建新的记录
            if (state == null)
            {
                state = new LicenseState { CreatedTicks = now.Ticks, LastSeenTicks = now.Ticks };
                TrySaveState(state);
                return;
            }

            var lastSeen = new DateTime(state.LastSeenTicks, DateTimeKind.Local);

            // 检测回退：若当前时间比上次记录小超过容差则认为被篡改，可根据需求调整
            var tolerance = TimeSpan.FromMinutes(5);
            if (now + tolerance < lastSeen)
            {
                Log.Information("检测到系统时间回退（上次: {Last}, 当前: {Now}），可能被篡改，程序退出。", lastSeen.ToString("o"), now.ToString("o"));
                Environment.Exit(1);
            }

            // 正常则更新 lastSeen（防止未来被回退绕过）
            if (now > lastSeen)
            {
                state.LastSeenTicks = now.Ticks;
                TrySaveState(state);
            }
        }

        /// <summary>
        /// 获取跨平台的通用应用程序数据路径
        /// </summary>
        /// <returns>适用于当前操作系统的路径</returns>
        private static string GetCrossPlatformCommonDataPath()
        {
            string path;
            if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
            {
                // Windows: C:\ProgramData\reunion_movement_server\license.bin
                path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "reunion_movement_server", "license.bin");
            }
            else
            {
                // Linux/macOS: ~/.reunion_movement_server/license.bin
                // CommonApplicationData on Unix-like systems might point to a read-only location (/usr/share).
                // Using a hidden directory in the user's home directory is a more reliable approach for writeable data.
                string homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                path = Path.Combine(homeDir, ".reunion_movement_server", "license.bin");
            }
            return path;
        }

        /// <summary>
        /// 获取到期日期
        /// </summary>
        /// <returns></returns>
        public static DateTime GetExpiryDate()
        {
            return ExpiryLocal;
        }

        /// <summary>
        /// 选择较新的许可状态
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <returns></returns>
        private static LicenseState ChooseNewer(LicenseState a, LicenseState b)
        {
            if (a == null) return b;
            if (b == null) return a;
            return a.LastSeenTicks >= b.LastSeenTicks ? a : b;
        }

        /// <summary>
        /// 从指定路径加载许可状态
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        private static LicenseState LoadState(string path)
        {
            try
            {
                if (!File.Exists(path)) return null;
                var encryptedBytes = File.ReadAllBytes(path);
                var plain = Decrypt(encryptedBytes);
                var json = Encoding.UTF8.GetString(plain);
                return JsonConvert.DeserializeObject<LicenseState>(json);
            }
            catch (Exception ex)
            {
                Log.Warning("LoadState({Path}) failed: {Msg}", path, ex.Message);
                return null;
            }
        }

        /// <summary>
        /// 尝试保存许可状态到两个位置
        /// </summary>
        /// <param name="state"></param>
        private static void TrySaveState(LicenseState state)
        {
            try
            {
                SaveState(AppDirPath, state);
            }
            catch (Exception ex)
            {
                Log.Warning("保存许可状态到 AppDir 失败: {Msg}", ex.Message);
            }

            try
            {
                var dir = Path.GetDirectoryName(CommonDataPath);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                SaveState(CommonDataPath, state);
            }
            catch (Exception ex)
            {
                Log.Warning("保存许可状态到 CommonData 失败: {Msg}", ex.Message);
            }
        }

        /// <summary>
        /// 保存许可状态到指定路径
        /// </summary>
        /// <param name="path"></param>
        /// <param name="state"></param>
        private static void SaveState(string path, LicenseState state)
        {
            var json = JsonConvert.SerializeObject(state);
            var bytes = Encoding.UTF8.GetBytes(json);
            var encrypted = Encrypt(bytes);
            File.WriteAllBytes(path, encrypted);
        }

        // 用于跨平台加密的密钥和盐。
        // 注意：在生产环境中，应更安全地管理这些值。
        private static readonly byte[] Salt = Encoding.UTF8.GetBytes("Vppi-Salt-Value-For-Lic-v1.0"); // 必须是唯一的
        private static readonly string Password = "Vppi-Lic-Password-For-Aes-v1.0"; // 应该更复杂

        /// <summary>
        /// 使用 AES 加密数据（跨平台）
        /// </summary>
        private static byte[] Encrypt(byte[] dataToEncrypt)
        {
            using (var aes = Aes.Create())
            {
                aes.Padding = PaddingMode.PKCS7;

                // 从密码和盐派生密钥
                var keyDerivation = new Rfc2898DeriveBytes(Password, Salt, 10000, HashAlgorithmName.SHA256);
                aes.Key = keyDerivation.GetBytes(32); // 256-bit key
                aes.IV = keyDerivation.GetBytes(16); // 128-bit IV

                using (var ms = new MemoryStream())
                {
                    using (var cs = new CryptoStream(ms, aes.CreateEncryptor(), CryptoStreamMode.Write))
                    {
                        cs.Write(dataToEncrypt, 0, dataToEncrypt.Length);
                        cs.FlushFinalBlock();
                    }
                    return ms.ToArray();
                }
            }
        }

        /// <summary>
        /// 使用 AES 解密数据（跨平台）
        /// </summary>
        private static byte[] Decrypt(byte[] dataToDecrypt)
        {
            using (var aes = Aes.Create())
            {
                aes.Padding = PaddingMode.PKCS7;

                // 从密码和盐派生相同的密钥
                var keyDerivation = new Rfc2898DeriveBytes(Password, Salt, 10000, HashAlgorithmName.SHA256);
                aes.Key = keyDerivation.GetBytes(32);
                aes.IV = keyDerivation.GetBytes(16);

                using (var ms = new MemoryStream())
                {
                    using (var cs = new CryptoStream(ms, aes.CreateDecryptor(), CryptoStreamMode.Write))
                    {
                        cs.Write(dataToDecrypt, 0, dataToDecrypt.Length);
                        cs.FlushFinalBlock();
                    }
                    return ms.ToArray();
                }
            }
        }

        /// <summary>
        /// 解析 ISO 8601 字符串为本地时间
        /// </summary>
        /// <param name="iso"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        public static DateTime ParseIso8601ToLocal(string iso)
        {
            if (string.IsNullOrWhiteSpace(iso)) throw new ArgumentNullException(nameof(iso));

            // 先尝试使用 DateTimeOffset 解析（保留偏移）
            if (DateTimeOffset.TryParse(iso, null, System.Globalization.DateTimeStyles.RoundtripKind, out var dto))
            {
                // 返回本地时间（Kind = DateTimeKind.Local）
                return dto.ToLocalTime().DateTime;
            }

            // 回退：尝试按 UTC 解析再转换为本地时间
            var dt = DateTime.Parse(iso, null, System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal);
            return dt.ToLocalTime();
        }
    }
}