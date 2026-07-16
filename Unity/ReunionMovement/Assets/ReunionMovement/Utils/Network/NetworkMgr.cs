using System.Collections.Generic;
using System.Threading;

namespace ReunionMovement.Common.Util
{
    public sealed class NetworkMgr : SingletonMgr<NetworkMgr>
    {
        List<INetworkChannel> channelList = new List<INetworkChannel>();
        List<INetworkChannel> channelDictRemove = new List<INetworkChannel>();
        private List<INetworkChannel> tickSnapshot;  // TickUpdate 复用的快照列表（零分配）
        private Thread netRun;
        private volatile bool isRunning = false;
        private readonly object syncRoot = new object();

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
                channelList.Add(channel);
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
                for (int i = channelList.Count - 1; i >= 0; i--)
                {
                    if (channelList[i].ChannelName == channelName)
                    {
                        channelList.RemoveAt(i);
                        return;
                    }
                }
            }
            Log.Error("不存在：" + channelName);
        }

        public void RemoveChannel(INetworkChannel channel)
        {
            if (channel == null) return;
            lock (syncRoot)
            {
                for (int i = channelList.Count - 1; i >= 0; i--)
                {
                    if (channelList[i] == channel)
                    {
                        channelList.RemoveAt(i);
                        return;
                    }
                }
            }
            Log.Error("不存在：" + channel.ChannelName);
        }

        public bool CloseChannel(string channelName)
        {
            INetworkChannel toClose = null;
            lock (syncRoot)
            {
                for (int i = 0; i < channelList.Count; i++)
                {
                    if (channelList[i].ChannelName == channelName)
                    {
                        toClose = channelList[i];
                        channelList.RemoveAt(i);
                        break;
                    }
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
                for (int i = 0; i < channelList.Count; i++)
                {
                    if (channelList[i] == channel)
                    {
                        toClose = channelList[i];
                        channelList.RemoveAt(i);
                        break;
                    }
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
                for (int i = 0; i < channelList.Count; i++)
                {
                    if (channelList[i].ChannelName == channelName)
                    {
                        return channelList[i];
                    }
                }
            }

            return null;
        }

        public bool HasChannel(string channelName)
        {
            lock (syncRoot)
            {
                for (int i = 0; i < channelList.Count; i++)
                {
                    if (channelList[i].ChannelName == channelName)
                    {
                        return true;
                    }
                }
            }

            return false;
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
        /// Update线程
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
                Thread.Sleep(1);
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
            // 零分配方案：直接处理待删除列表，锁定内拷贝引用到复用列表再解锁 tick
            lock (syncRoot)
            {
                // 直接在锁内处理待删除（Remove 是 O(n)，但 channelDictRemove 通常很小）
                for (int i = channelDictRemove.Count - 1; i >= 0; i--)
                {
                    channelList.Remove(channelDictRemove[i]);
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