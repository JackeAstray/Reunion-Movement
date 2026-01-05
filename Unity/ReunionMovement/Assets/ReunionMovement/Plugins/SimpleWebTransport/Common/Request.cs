using System;
using System.Collections.Generic;
using System.Linq;

namespace Mirror.SimpleWeb
{
    /// <summary>
    /// 表示客户端发送到 WebSocket 服务器的请求，这是客户端发送的第一条消息。
    /// </summary>
    public class Request
    {
        static readonly char[] lineSplitChars = new char[] { '\r', '\n' };
        static readonly char[] headerSplitChars = new char[] { ':' };
        public string RequestLine;
        public Dictionary<string, string> Headers = new Dictionary<string, string>();

        public Request(string message)
        {
            string[] all = message.Split(lineSplitChars, StringSplitOptions.RemoveEmptyEntries);
            RequestLine = all.First();
            Headers = all.Skip(1)
                         .Select(header => header.Split(headerSplitChars, 2, StringSplitOptions.RemoveEmptyEntries))
                         .ToDictionary(split => split[0].Trim(), split => split[1].Trim());
        }
    }
}
