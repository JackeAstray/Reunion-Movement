using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine.Profiling;

namespace Mirror.SimpleWeb
{
    internal static class ReceiveLoop
    {
        public struct Config
        {
            public readonly Connection conn;
            public readonly int maxMessageSize;
            public readonly bool expectMask;
            public readonly ConcurrentQueue<Message> queue;
            public readonly BufferPool bufferPool;

            public Config(Connection conn, int maxMessageSize, bool expectMask, ConcurrentQueue<Message> queue, BufferPool bufferPool)
            {
                this.conn = conn ?? throw new ArgumentNullException(nameof(conn));
                this.maxMessageSize = maxMessageSize;
                this.expectMask = expectMask;
                this.queue = queue ?? throw new ArgumentNullException(nameof(queue));
                this.bufferPool = bufferPool ?? throw new ArgumentNullException(nameof(bufferPool));
            }

            public void Deconstruct(out Connection conn, out int maxMessageSize, out bool expectMask, out ConcurrentQueue<Message> queue, out BufferPool bufferPool)
            {
                conn = this.conn;
                maxMessageSize = this.maxMessageSize;
                expectMask = this.expectMask;
                queue = this.queue;
                bufferPool = this.bufferPool;
            }
        }

        struct Header
        {
            public int payloadLength;
            public int offset;
            public int opcode;
            public bool finished;
        }

        public static void Loop(Config config)
        {
            (Connection conn, int maxMessageSize, bool expectMask, ConcurrentQueue<Message> queue, BufferPool _) = config;

            Profiler.BeginThreadProfiling("SimpleWeb", $"ReceiveLoop {conn.connId}");

            byte[] readBuffer = new byte[Constants.HeaderSize + (expectMask ? Constants.MaskSize : 0) + maxMessageSize];
            try
            {
                try
                {
                    TcpClient client = conn.client;

                    while (client.Connected)
                        ReadOneMessage(config, readBuffer);

                    Log.Verbose("[SWT-ReceiveLoop]: {0} Not Connected", conn);
                }
                catch (Exception)
                {
                    // 如果被中断我们不关心其他异常
                    Utils.CheckForInterupt();
                    throw;
                }
            }
            catch (ThreadInterruptedException e) { Log.InfoException(e); }
            catch (ThreadAbortException) { Log.Error("[SWT-ReceiveLoop]: Thread Abort Exception"); }
            catch (ObjectDisposedException e) { Log.InfoException(e); }
            catch (ReadHelperException e) { Log.InfoException(e); }
            catch (SocketException e)
            {
                // 如果 wss 客户端关闭流，可能发生此情况
                Log.Warn("[SWT-ReceiveLoop]: ReceiveLoop SocketException\n{0}", e.Message);
                queue.Enqueue(new Message(conn.connId, e));
            }
            catch (IOException e)
            {
                // 如果客户端断开连接，可能发生此情况
                Log.Warn("[SWT-ReceiveLoop]: ReceiveLoop IOException\n{0}", e.Message);
                queue.Enqueue(new Message(conn.connId, e));
            }
            catch (InvalidDataException e)
            {
                Log.Error("[SWT-ReceiveLoop]: 从 {0} 接收到无效数据\n{1}\n{2}\n\n", conn, e.Message, e.StackTrace);
                queue.Enqueue(new Message(conn.connId, e));
            }
            catch (Exception e)
            {
                Log.Exception(e);
                queue.Enqueue(new Message(conn.connId, e));
            }
            finally
            {
                Profiler.EndThreadProfiling();
                conn.Dispose();
            }
        }

        static void ReadOneMessage(Config config, byte[] buffer)
        {
            (Connection conn, int maxMessageSize, bool expectMask, ConcurrentQueue<Message> queue, BufferPool bufferPool) = config;
            Stream stream = conn.stream;

            Header header = ReadHeader(config, buffer);

            int msgOffset = header.offset;
            header.offset = ReadHelper.Read(stream, buffer, header.offset, header.payloadLength);

            if (header.finished)
            {
                switch (header.opcode)
                {
                    case 2:
                        HandleArrayMessage(config, buffer, msgOffset, header.payloadLength);
                        break;
                    case 8:
                        HandleCloseMessage(config, buffer, msgOffset, header.payloadLength);
                        break;
                }
            }
            else
            {
                // todo 缓存此以避免分配
                Queue<ArrayBuffer> fragments = new Queue<ArrayBuffer>();
                fragments.Enqueue(CopyMessageToBuffer(bufferPool, expectMask, buffer, msgOffset, header.payloadLength));
                int totalSize = header.payloadLength;

                while (!header.finished)
                {
                    header = ReadHeader(config, buffer, opCodeContinuation: true);

                    msgOffset = header.offset;
                    header.offset = ReadHelper.Read(stream, buffer, header.offset, header.payloadLength);
                    fragments.Enqueue(CopyMessageToBuffer(bufferPool, expectMask, buffer, msgOffset, header.payloadLength));

                    totalSize += header.payloadLength;
                    MessageProcessor.ThrowIfMsgLengthTooLong(totalSize, maxMessageSize);
                }

                ArrayBuffer msg = bufferPool.Take(totalSize);
                msg.count = 0;
                while (fragments.Count > 0)
                {
                    ArrayBuffer part = fragments.Dequeue();

                    part.CopyTo(msg.array, msg.count);
                    msg.count += part.count;

                    part.Release();
                }

                // 在去掩码后打印
                Log.DumpBuffer($"[SWT-ReceiveLoop]: Message", msg);

                queue.Enqueue(new Message(conn.connId, msg));
            }
        }

        static Header ReadHeader(Config config, byte[] buffer, bool opCodeContinuation = false)
        {
            (Connection conn, int maxMessageSize, bool expectMask, ConcurrentQueue<Message> queue, BufferPool bufferPool) = config;
            Stream stream = conn.stream;
            Header header = new Header();

            // 读取 2 字节
            header.offset = ReadHelper.Read(stream, buffer, header.offset, Constants.HeaderMinSize);
            // 在第一次阻塞调用后记录日志
            Log.Flood($"[SWT-ReceiveLoop]: Message From {conn}");

            if (MessageProcessor.NeedToReadShortLength(buffer))
                header.offset = ReadHelper.Read(stream, buffer, header.offset, Constants.ShortLength);
            if (MessageProcessor.NeedToReadLongLength(buffer))
                header.offset = ReadHelper.Read(stream, buffer, header.offset, Constants.LongLength);

            Log.DumpBuffer($"[SWT-ReceiveLoop]: Raw Header", buffer, 0, header.offset);

            MessageProcessor.ValidateHeader(buffer, maxMessageSize, expectMask, opCodeContinuation);

            if (expectMask)
                header.offset = ReadHelper.Read(stream, buffer, header.offset, Constants.MaskSize);

            header.opcode = MessageProcessor.GetOpcode(buffer);
            header.payloadLength = MessageProcessor.GetPayloadLength(buffer);
            header.finished = MessageProcessor.Finished(buffer);

            Log.Flood($"[SWT-ReceiveLoop]: Header ln:{header.payloadLength} op:{header.opcode} mask:{expectMask}");

            return header;
        }

        static void HandleArrayMessage(Config config, byte[] buffer, int msgOffset, int payloadLength)
        {
            (Connection conn, int _, bool expectMask, ConcurrentQueue<Message> queue, BufferPool bufferPool) = config;

            ArrayBuffer arrayBuffer = CopyMessageToBuffer(bufferPool, expectMask, buffer, msgOffset, payloadLength);

            // 在去掩码后打印
            Log.DumpBuffer($"[SWT-ReceiveLoop]: Message", arrayBuffer);

            queue.Enqueue(new Message(conn.connId, arrayBuffer));
        }

        static ArrayBuffer CopyMessageToBuffer(BufferPool bufferPool, bool expectMask, byte[] buffer, int msgOffset, int payloadLength)
        {
            ArrayBuffer arrayBuffer = bufferPool.Take(payloadLength);

            if (expectMask)
            {
                int maskOffset = msgOffset - Constants.MaskSize;
                // 直接将取消掩码后的结果写入 arrayBuffer，避免第二次复制
                MessageProcessor.ToggleMask(buffer, msgOffset, arrayBuffer, payloadLength, buffer, maskOffset);
            }
            else
                arrayBuffer.CopyFrom(buffer, msgOffset, payloadLength);

            return arrayBuffer;
        }

        static void HandleCloseMessage(Config config, byte[] buffer, int msgOffset, int payloadLength)
        {
            (Connection conn, int _, bool expectMask, ConcurrentQueue<Message> _, BufferPool _) = config;

            if (expectMask)
            {
                int maskOffset = msgOffset - Constants.MaskSize;
                MessageProcessor.ToggleMask(buffer, msgOffset, payloadLength, buffer, maskOffset);
            }

            // 在去掩码后打印
            Log.DumpBuffer($"[SWT-ReceiveLoop]: Message", buffer, msgOffset, payloadLength);
            Log.Verbose("[SWT-ReceiveLoop]: Close: {0} message:{1}", GetCloseCode(buffer, msgOffset), GetCloseMessage(buffer, msgOffset, payloadLength));

            conn.Dispose();
        }

        static string GetCloseMessage(byte[] buffer, int msgOffset, int payloadLength)
            => Encoding.UTF8.GetString(buffer, msgOffset + 2, payloadLength - 2);

        static int GetCloseCode(byte[] buffer, int msgOffset)
            => buffer[msgOffset + 0] << 8 | buffer[msgOffset + 1];
    }
}
