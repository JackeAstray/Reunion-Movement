using System;
using System.IO;
using System.Runtime.Serialization;

namespace Mirror.SimpleWeb
{
    public static class ReadHelper
    {
        /// <summary>
        /// 从流中精确读取指定长度
        /// </summary>
        /// <returns>outOffset + length</returns>
        /// <exception cref="ReadHelperException"></exception>
        public static int Read(Stream stream, byte[] outBuffer, int outOffset, int length)
        {
            int received = 0;
            try
            {
                while (received < length)
                {
                    int read = stream.Read(outBuffer, outOffset + received, length - received);
                    if (read == 0)
                        throw new ReadHelperException("[SWT-ReadHelper]: Read returned 0");

                    received += read;
                }
            }
            catch (AggregateException ae)
            {
                // 如果被中断，我们不关心异常
                Utils.CheckForInterupt();

                // 重新抛出
                ae.Handle(e => false);
            }

            if (received != length)
                throw new ReadHelperException("[SWT-ReadHelper]: received not equal to length");

            return outOffset + received;
        }

        /// <summary>
        /// 读取并返回结果。此方法不应抛出异常
        /// </summary>
        public static bool TryRead(Stream stream, byte[] outBuffer, int outOffset, int length)
        {
            try
            {
                Read(stream, outBuffer, outOffset, length);
                return true;
            }
            catch (ReadHelperException)
            {
                return false;
            }
            catch (IOException)
            {
                return false;
            }
            catch (Exception e)
            {
                Log.Exception(e);
                return false;
            }
        }

        public static int? SafeReadTillMatch(Stream stream, byte[] outBuffer, int outOffset, int maxLength, byte[] endOfHeader)
        {
            try
            {
                int read = 0;
                int endIndex = 0;
                int endLength = endOfHeader.Length;
                while (true)
                {
                    int next = stream.ReadByte();
                    if (next == -1) // 关闭
                        return null;

                    if (read >= maxLength)
                    {
                        Log.Error("[SWT-ReadHelper]: SafeReadTillMatch 超出最大长度");
                        return null;
                    }

                    outBuffer[outOffset + read] = (byte)next;
                    read++;

                    // 如果匹配，检查下一个字节
                    if (endOfHeader[endIndex] == next)
                    {
                        endIndex++;
                        // 如果全部匹配则返回已读取长度
                        if (endIndex >= endLength)
                            return read;
                    }
                    // 如果不匹配则重置索引
                    else
                        endIndex = 0;
                }
            }
            catch (IOException e)
            {
                Log.InfoException(e);
                return null;
            }
            catch (Exception e)
            {
                Log.Exception(e);
                return null;
            }
        }
    }

    [Serializable]
    public class ReadHelperException : Exception
    {
        public ReadHelperException(string message) : base(message) { }

        protected ReadHelperException(SerializationInfo info, StreamingContext context) : base(info, context) { }
    }
}
