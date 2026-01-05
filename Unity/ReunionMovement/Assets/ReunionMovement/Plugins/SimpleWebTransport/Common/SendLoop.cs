using System;
using System.IO;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Threading;
using UnityEngine.Profiling;

namespace Mirror.SimpleWeb
{
    public static class SendLoopConfig
    {
        public static volatile bool batchSend = false;
        public static volatile bool sleepBeforeSend = false;
    }
    internal static class SendLoop
    {
        public struct Config
        {
            public readonly Connection conn;
            public readonly int bufferSize;
            public readonly bool setMask;

            public Config(Connection conn, int bufferSize, bool setMask)
            {
                this.conn = conn ?? throw new ArgumentNullException(nameof(conn));
                this.bufferSize = bufferSize;
                this.setMask = setMask;
            }

            public void Deconstruct(out Connection conn, out int bufferSize, out bool setMask)
            {
                conn = this.conn;
                bufferSize = this.bufferSize;
                setMask = this.setMask;
            }
        }

        public static void Loop(Config config)
        {
            (Connection conn, int bufferSize, bool setMask) = config;

            Profiler.BeginThreadProfiling("SimpleWeb", $"SendLoop {conn.connId}");

            // 为此线程创建写入缓冲区
            byte[] writeBuffer = new byte[bufferSize];
            MaskHelper maskHelper = setMask ? new MaskHelper() : null;
            try
            {
                TcpClient client = conn.client;
                Stream stream = conn.stream;

                // 在发送线程启动时进行空检查，以防在此期间断开连接
                if (client == null)
                    return;

                while (client.Connected)
                {
                    // 等待消息
                    conn.sendPending.Wait();
                    // 等待 1ms，以便 mirror 发送其他消息
                    if (SendLoopConfig.sleepBeforeSend)
                        Thread.Sleep(1);

                    conn.sendPending.Reset();

                    if (SendLoopConfig.batchSend)
                    {
                        int offset = 0;
                        while (conn.sendQueue.TryDequeue(out ArrayBuffer msg))
                        {
                            // 在发送消息之前检查是否连接
                            if (!client.Connected)
                            {
                                Log.Verbose("[SWT-SendLoop]: SendLoop {0} not connected", conn);
                                msg.Release();
                                return;
                            }

                            int maxLength = msg.count + Constants.HeaderSize + Constants.MaskSize;

                            // 如果下一个写入可能会溢出，则先写入流并清除缓冲区
                            if (offset + maxLength > bufferSize)
                            {
                                stream.Write(writeBuffer, 0, offset);
                                offset = 0;
                            }

                            offset = SendMessage(writeBuffer, offset, msg, setMask, maskHelper);
                            msg.Release();
                        }

                        // 在队列为空后发送剩余消息
                        // 不需要检查 offset > 0，因为队列中的最后一条消息将在这里发送

                        stream.Write(writeBuffer, 0, offset);
                    }
                    else
                    {
                        while (conn.sendQueue.TryDequeue(out ArrayBuffer msg))
                        {
                            // 在发送消息之前检查是否连接
                            if (!client.Connected)
                            {
                                Log.Verbose("[SWT-SendLoop]: SendLoop {0} not connected", conn);
                                msg.Release();
                                return;
                            }

                            int length = SendMessage(writeBuffer, 0, msg, setMask, maskHelper);
                            stream.Write(writeBuffer, 0, length);
                            msg.Release();
                        }
                    }
                }

                Log.Verbose("[SWT-SendLoop]: {0} Not Connected", conn);
            }
            catch (ThreadInterruptedException e) { Log.InfoException(e); }
            catch (ThreadAbortException) { Log.Error("[SWT-SendLoop]: Thread Abort Exception"); }
            catch (Exception e) { Log.Exception(e); }
            finally
            {
                Profiler.EndThreadProfiling();
                conn.Dispose();
                maskHelper?.Dispose();
            }
        }

        /// <returns>new offset in buffer</returns>
        static int SendMessage(byte[] buffer, int startOffset, ArrayBuffer msg, bool setMask, MaskHelper maskHelper)
        {
            int msgLength = msg.count;
            int offset = WriteHeader(buffer, startOffset, msgLength, setMask);

            if (setMask)
            {
                offset = maskHelper.WriteMask(buffer, offset);
            }

            msg.CopyTo(buffer, offset);
            offset += msgLength;

            // 在加掩码之前打印
            Log.DumpBuffer("[SWT-SendLoop]: Send", buffer, startOffset, offset);

            if (setMask)
            {
                int messageOffset = offset - msgLength;
                MessageProcessor.ToggleMask(buffer, messageOffset, msgLength, buffer, messageOffset - Constants.MaskSize);
            }

            return offset;
        }

        public static int WriteHeader(byte[] buffer, int startOffset, int msgLength, bool setMask)
        {
            int sendLength = 0;
            const byte finished = 128;
            const byte byteOpCode = 2;

            buffer[startOffset + 0] = finished | byteOpCode;
            sendLength++;

            if (msgLength <= Constants.BytePayloadLength)
            {
                buffer[startOffset + 1] = (byte)msgLength;
                sendLength++;
            }
            else if (msgLength <= ushort.MaxValue)
            {
                buffer[startOffset + 1] = 126;
                buffer[startOffset + 2] = (byte)(msgLength >> 8);
                buffer[startOffset + 3] = (byte)msgLength;
                sendLength += 3;
            }
            else
            {
                buffer[startOffset + 1] = 127;
                // 必须为 64 字节，但我们只有 32 位长度，因此前 4 位为 0
                buffer[startOffset + 2] = 0;
                buffer[startOffset + 3] = 0;
                buffer[startOffset + 4] = 0;
                buffer[startOffset + 5] = 0;
                buffer[startOffset + 6] = (byte)(msgLength >> 24);
                buffer[startOffset + 7] = (byte)(msgLength >> 16);
                buffer[startOffset + 8] = (byte)(msgLength >> 8);
                buffer[startOffset + 9] = (byte)msgLength;

                sendLength += 9;
            }

            if (setMask)
                buffer[startOffset + 1] |= 0b1000_0000;

            return sendLength + startOffset;
        }

    }
    sealed class MaskHelper : IDisposable
    {
        readonly byte[] maskBuffer;
        readonly RNGCryptoServiceProvider random;

        public MaskHelper()
        {
            maskBuffer = new byte[4];
            random = new RNGCryptoServiceProvider();
        }
        public void Dispose()
        {
            random.Dispose();
        }

        public int WriteMask(byte[] buffer, int offset)
        {
            random.GetBytes(maskBuffer);
            Buffer.BlockCopy(maskBuffer, 0, buffer, offset, 4);

            return offset + 4;
        }
    }
}
