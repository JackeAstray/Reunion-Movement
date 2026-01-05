using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Mirror.SimpleWeb
{
    /// <summary>
    /// 处理客户端在首次连接到服务器时的握手
    /// <para>客户端握手只发生一次，因此不需要缓冲以减少分配。</para>
    /// </summary>
    internal class ClientHandshake
    {
        public bool TryHandshake(Connection conn, Uri uri)
        {
            try
            {
                Stream stream = conn.stream;

                byte[] keyBuffer = new byte[16];
                using (RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider())
                    rng.GetBytes(keyBuffer);

                string key = Convert.ToBase64String(keyBuffer);
                string keySum = key + Constants.HandshakeGUID;
                byte[] keySumBytes = Encoding.ASCII.GetBytes(keySum);
                Log.Verbose("[SWT-客户端握手]: 正在对握手进行哈希 {0}", Encoding.ASCII.GetString(keySumBytes));

                // SHA-1 是 WebSocket 标准:
                // https://www.rfc-editor.org/rfc/rfc6455
                // 我们应遵循标准，即使 SHA1 被认为不够安全:
                // https://stackoverflow.com/questions/38038841/why-is-sha-1-considered-insecure
                byte[] keySumHash = SHA1.Create().ComputeHash(keySumBytes);

                string expectedResponse = Convert.ToBase64String(keySumHash);
                string handshake =
                    $"GET {uri.PathAndQuery} HTTP/1.1\r\n" +
                    $"Host: {uri.Host}:{uri.Port}\r\n" +
                    $"Upgrade: websocket\r\n" +
                    $"Connection: Upgrade\r\n" +
                    $"Sec-WebSocket-Key: {key}\r\n" +
                    $"Sec-WebSocket-Version: 13\r\n" +
                    "\r\n";
                byte[] encoded = Encoding.ASCII.GetBytes(handshake);
                stream.Write(encoded, 0, encoded.Length);

                byte[] responseBuffer = new byte[1000];

                int? lengthOrNull = ReadHelper.SafeReadTillMatch(stream, responseBuffer, 0, responseBuffer.Length, Constants.endOfHandshake);

                if (!lengthOrNull.HasValue)
                {
                    Log.Error("[SWT-客户端握手]: 在握手完成前连接已关闭");
                    return false;
                }

                string responseString = Encoding.ASCII.GetString(responseBuffer, 0, lengthOrNull.Value);
                Log.Verbose("[SWT-客户端握手]: 握手响应 {0}", responseString);

                string acceptHeader = "Sec-WebSocket-Accept: ";
                int startIndex = responseString.IndexOf(acceptHeader, StringComparison.InvariantCultureIgnoreCase);

                if (startIndex < 0)
                {
                    Log.Error("[SWT-客户端握手]: 意外的握手响应 {0}", responseString);
                    return false;
                }

                startIndex += acceptHeader.Length;
                int endIndex = responseString.IndexOf("\r\n", startIndex);
                string responseKey = responseString.Substring(startIndex, endIndex - startIndex);

                if (responseKey != expectedResponse)
                {
                    Log.Error("[SWT-客户端握手]: 响应键不正确\n期望:{0}\n响应:{1}\n如果 Windows Server 未安装 Websocket 协议 (Server Roles)，可能会发生此问题。", expectedResponse, responseKey);
                    return false;
                }

                return true;
            }
            catch (Exception e)
            {
                Log.Exception(e);
                return false;
            }
        }
    }
}
