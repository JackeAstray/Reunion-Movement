using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Mirror.SimpleWeb
{
    /// <summary>
    /// 处理来自服务器的新客户端握手
    /// <para>服务器握手使用缓冲区以减少客户端连接时的分配</para>
    /// </summary>
    internal class ServerHandshake
    {
        const int GetSize = 3;
        const int ResponseLength = 129;
        const int KeyLength = 24;
        const int MergedKeyLength = 60;
        const string KeyHeaderString = "\r\nSec-WebSocket-Key: ";
        // 这不是官方的最大值，只是一个合理的 websocket 握手大小
        readonly int maxHttpHeaderSize = 3000;

        // SHA-1 是 websocket 标准：
        // https://www.rfc-editor.org/rfc/rfc6455
        // 即使 SHA1 被认为弱，我们也应遵循标准：
        // https://stackoverflow.com/questions/38038841/why-is-sha-1-considered-insecure
        readonly SHA1 sha1 = SHA1.Create();
        readonly BufferPool bufferPool;

        public ServerHandshake(BufferPool bufferPool, int handshakeMaxSize)
        {
            this.bufferPool = bufferPool;
            maxHttpHeaderSize = handshakeMaxSize;
        }

        ~ServerHandshake()
        {
            sha1.Dispose();
        }

        public bool TryHandshake(Connection conn)
        {
            Stream stream = conn.stream;

            using (ArrayBuffer getHeader = bufferPool.Take(GetSize))
            {
                if (!ReadHelper.TryRead(stream, getHeader.array, 0, GetSize))
                    return false;

                getHeader.count = GetSize;

                if (!IsGet(getHeader.array))
                {
                    Log.Warn("[SWT-ServerHandshake]: 握手的首个字节不是 'GET'，而是 {0}", Log.BufferToString(getHeader.array, 0, GetSize));
                    return false;
                }
            }

            string msg = ReadToEndForHandshake(stream);

            if (string.IsNullOrEmpty(msg))
                return false;

            try
            {
                AcceptHandshake(stream, msg);

                conn.request = new Request(msg);
                conn.remoteAddress = conn.CalculateAddress();
                Log.Info($"[SWT-ServerHandshake]: 客户端已连接，来源 {0}", conn);

                return true;
            }
            catch (ArgumentException e)
            {
                Log.InfoException(e);
                return false;
            }
        }

        string ReadToEndForHandshake(Stream stream)
        {
            using (ArrayBuffer readBuffer = bufferPool.Take(maxHttpHeaderSize))
            {
                int? readCountOrFail = ReadHelper.SafeReadTillMatch(stream, readBuffer.array, 0, maxHttpHeaderSize, Constants.endOfHandshake);
                if (!readCountOrFail.HasValue)
                    return null;

                int readCount = readCountOrFail.Value;

                string msg = Encoding.ASCII.GetString(readBuffer.array, 0, readCount);
                // GET 不在我们这里读取的字节内，所以需要补回
                msg = $"GET{msg}";
                Log.Verbose("[SWT-ServerHandshake]: 客户端握手消息:\r\n{0}", msg);

                return msg;
            }
        }

        static bool IsGet(byte[] getHeader)
        {
            // 在此处仅检查字节，而不是使用 Encoding.ASCII
            return getHeader[0] == 71 && // G
                   getHeader[1] == 69 && // E
                   getHeader[2] == 84;   // T
        }

        void AcceptHandshake(Stream stream, string msg)
        {
            using (ArrayBuffer keyBuffer = bufferPool.Take(KeyLength + Constants.HandshakeGUIDLength),
                               responseBuffer = bufferPool.Take(ResponseLength))
            {
                GetKey(msg, keyBuffer.array);
                AppendGuid(keyBuffer.array);
                byte[] keyHash = CreateHash(keyBuffer.array);
                CreateResponse(keyHash, responseBuffer.array);

                stream.Write(responseBuffer.array, 0, ResponseLength);
            }
        }

        static void GetKey(string msg, byte[] keyBuffer)
        {
            int start = msg.IndexOf(KeyHeaderString, StringComparison.InvariantCultureIgnoreCase) + KeyHeaderString.Length;

            Log.Verbose("[SWT-ServerHandshake]: 握手密钥: {0}", msg.Substring(start, KeyLength));
            Encoding.ASCII.GetBytes(msg, start, KeyLength, keyBuffer, 0);
        }

        static void AppendGuid(byte[] keyBuffer)
        {
            Buffer.BlockCopy(Constants.HandshakeGUIDBytes, 0, keyBuffer, KeyLength, Constants.HandshakeGUIDLength);
        }

        byte[] CreateHash(byte[] keyBuffer)
        {
            Log.Verbose("[SWT-ServerHandshake]: 握手哈希处理中 {0}", Encoding.ASCII.GetString(keyBuffer, 0, MergedKeyLength));
            return sha1.ComputeHash(keyBuffer, 0, MergedKeyLength);
        }

        static void CreateResponse(byte[] keyHash, byte[] responseBuffer)
        {
            string keyHashString = Convert.ToBase64String(keyHash);

            // 编译器应该将这些字符串合并为一个字符串，然后再格式化
            string message = string.Format(
                "HTTP/1.1 101 Switching Protocols\r\n" +
                "Connection: Upgrade\r\n" +
                "Upgrade: websocket\r\n" +
                "Sec-WebSocket-Accept: {0}\r\n\r\n",
                keyHashString);

            Log.Verbose("[SWT-ServerHandshake]: 握手响应长度 {0}, 是否符合预期 {1}", message.Length, message.Length == ResponseLength);
            Encoding.ASCII.GetBytes(message, 0, ResponseLength, responseBuffer, 0);
        }
    }
}
