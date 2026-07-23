using System.Collections.Generic;
using System.Threading;

namespace ReunionMovement.Common.Util
{
    public sealed class NetworkMgr : SingletonMgr<NetworkMgr>
    {
        // 主通道列表（用于 Tick 迭代，List 遍历比 Dictionary 快）
        List<INetworkChannel> channelList = new List<INetworkChannel>();
        // 按名称索引（O(1) 查找，与 channelList 并行维护）
        Dictionary<string, INetworkChannel> channelIndex = new Dictionary<string, INetworkChannel>();
        List<INetworkChannel> channelDictRemove = new List<INetworkChannel>();
        private List<INetworkChannel> tickSnapshot;  // TickUpdate 复用的快照列表（零分配）
        private Thread netRun;
        private volatile bool isRunning = false;
        private readonly object syncRoot = new object();

        /// <summary>网络 tick 间隔（ms）。移动端建议 10-20ms，PC 可用 5ms。</summary>
        private const int NetworkTickIntervalMs = 10;

        public int NetworkChannelCount
        {
            get
            {
                lock (syncRoot)
                {
                    return channelList.Count;
                }
            }
        }

        protected override void Awake()
        {
            base.Awake();
            OnInit(null);
        }

        public void AddChannel(INetworkChannel channel)
        {
            if (channel == null) return;
            lock (syncRoot)
            {
                // 如果同名通道已存在，先移除旧的
                if (!string.IsNullOrEmpty(channel.ChannelName) && channelIndex.TryGetValue(channel.ChannelName, out var existing))
                {
                    channelList.Remove(existing);
                    channelIndex.Remove(channel.ChannelName);
                }
                channelList.Add(channel);
                if (!string.IsNullOrEmpty(channel.ChannelName))
                {
                    channelIndex[channel.ChannelName] = channel;
                }
            }
        }

        /// <summary>
        /// 将 Channel 加入延迟删除队列，在下次 TickUpdate 时安全移除（避免在 TickRefresh 回调中直接修改列表）。
        /// </summary>
        /// <param name="channel">待移除的网络通道</param>
        public void ScheduleRemove(INetworkChannel channel)
        {
            if (channel == null) return;
            lock (syncRoot)
            {
                channelDictRemove.Add(channel);
            }
        }

        public void RemoveChannel(string channelName)
        {
            lock (syncRoot)
            {
                if (channelIndex.TryGetValue(channelName, out var found))
                {
                    channelList.Remove(found);
                    channelIndex.Remove(channelName);
                    return;
                }
            }
            Log.Error("不存在：" + channelName);
        }

        public void RemoveChannel(INetworkChannel channel)
        {
            if (channel == null) return;
            lock (syncRoot)
            {
                if (channelList.Remove(channel))
                {
                    channelIndex.Remove(channel.ChannelName);
                    return;
                }
            }
            Log.Error("不存在：" + channel.ChannelName);
        }

        public bool CloseChannel(string channelName)
        {
            INetworkChannel toClose = null;
            lock (syncRoot)
            {
                if (channelIndex.TryGetValue(channelName, out toClose))
                {
                    channelList.Remove(toClose);
                    channelIndex.Remove(channelName);
                }
            }

            if (toClose != null)
            {
                toClose.Close();
                return true;
            }
            return false;
        }

        public bool CloseChannel(INetworkChannel channel)
        {
            if (channel == null) return false;
            INetworkChannel toClose = null;
            lock (syncRoot)
            {
                if (channelList.Remove(channel))
                {
                    channelIndex.Remove(channel.ChannelName);
                    toClose = channel;
                }
            }

            if (toClose != null)
            {
                toClose.Close();
                return true;
            }
            return false;
        }

        public INetworkChannel PeekChannel(string channelName)
        {
            lock (syncRoot)
            {
                channelIndex.TryGetValue(channelName, out var found);
                return found;
            }
        }

        public bool HasChannel(string channelName)
        {
            lock (syncRoot)
            {
                return channelIndex.ContainsKey(channelName);
            }
        }

        public List<INetworkChannel> GetAllChannels()
        {
            lock (syncRoot)
            {
                // return a shallow copy to avoid exposing internal collection
                return new List<INetworkChannel>(channelList);
            }
        }

        public void OnInit(object createParam)
        {
            lock (syncRoot)
            {
                channelList.Clear();
                channelIndex.Clear();
                channelDictRemove.Clear();
            }
        }

        /// <summary>
        /// 开启Update线程
        /// </summary>
        public void StartThread()
        {
#if UNITY_WEBGL
            Log.Error("WebGL不支持.Net多线程，跳过线程启动");
            return;
#else
            if (netRun == null)
            {
                isRunning = true;
                netRun = new Thread(new ThreadStart(ThreadOnUpdate)) { IsBackground = true };
                netRun.Start();
            }
#endif
        }

        /// <summary>
        /// Update线程 —— 以 NetworkTickIntervalMs 间隔驱动网络 Tick。
        /// 不再使用 1ms 忙等，避免移动端 CPU 无法深度睡眠导致发热。
        /// </summary>
        private void ThreadOnUpdate()
        {
            while (isRunning)
            {
                try
                {
                    TickUpdate();
                }
                catch (System.Exception ex)
                {
                    Log.Warning("NetworkMgr.ThreadOnUpdate 捕获异常：" + ex);
                }
                Thread.Sleep(NetworkTickIntervalMs);
            }
        }

        /// <summary>
        /// Update只能利用单个CPU核心
        /// </summary>
        public void OnUpdate()
        {
            if (netRun == null)
            {
                TickUpdate();
            }
        }

        private void TickUpdate()
        {
            lock (syncRoot)
            {
                // 处理延迟删除队列 —— 同步清理 List 和 Index
                for (int i = channelDictRemove.Count - 1; i >= 0; i--)
                {
                    var ch = channelDictRemove[i];
                    channelList.Remove(ch);
                    channelIndex.Remove(ch.ChannelName);
                }
                channelDictRemove.Clear();

                // 复用 tick 快照列表（仅在扩容时分配）
                if (channelList.Count > 0)
                {
                    if (tickSnapshot == null) tickSnapshot = new List<INetworkChannel>(channelList.Count);
                    tickSnapshot.Clear();
                    tickSnapshot.AddRange(channelList);
                }
            }

            if (tickSnapshot != null && tickSnapshot.Count > 0)
            {
                for (int i = 0; i < tickSnapshot.Count; i++)
                {
                    try
                    {
                        tickSnapshot[i].TickRefresh();
                    }
                    catch (System.Exception ex)
                    {
                        Log.Warning("NetworkMgr.Update TickRefresh 错误：" + ex);
                    }
                }
            }
        }

        public void OnLateUpdate()
        {

        }

        public void OnFixedUpdate()
        {

        }

        public void OnTermination()
        {
            isRunning = false;
            if (netRun != null)
            {
                netRun.Join(100);
                netRun = null;
            }

            List<INetworkChannel> toClose;
            lock (syncRoot)
            {
                toClose = new List<INetworkChannel>(channelList);
                channelList.Clear();
                channelIndex.Clear();
            }

            for (int i = 0; i < toClose.Count; i++)
            {
                try { toClose[i].Close(); }
                catch (System.Exception ex) { Log.Warning("NetworkMgr.OnTermination 关闭 channel 失败: {0}", ex.Message); }
            }
        }

        private void OnDestroy()
        {
            OnTermination();
        }
    }
}