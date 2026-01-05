using System.Collections.Generic;
using System.Threading;

namespace ReunionMovement.Common.Util
{
    public sealed class NetworkMgr : SingletonMgr<NetworkMgr>
    {
        List<INetworkChannel> channelDict;
        List<INetworkChannel> channelDictRemove;
        private Thread netRun;
        private readonly object syncRoot = new object();

        public int NetworkChannelCount
        {
            get
            {
                lock (syncRoot)
                {
                    return channelDict.Count;
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
                channelDict.Add(channel);
            }
        }

        public void RemoveChannel(string channelName)
        {
            lock (syncRoot)
            {
                for (int i = 0; i < channelDict.Count; i++)
                {
                    if (channelDict[i].ChannelName == channelName)
                    {
                        channelDictRemove.Add(channelDict[i]);
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
                for (int i = 0; i < channelDict.Count; i++)
                {
                    if (channelDict[i] == channel)
                    {
                        channelDictRemove.Add(channelDict[i]);
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
                for (int i = 0; i < channelDict.Count; i++)
                {
                    if (channelDict[i].ChannelName == channelName)
                    {
                        toClose = channelDict[i];
                        break;
                    }
                }
            }

            if (toClose != null)
            {
                try { toClose.Close(); } catch { }
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
                for (int i = 0; i < channelDict.Count; i++)
                {
                    if (channelDict[i] == channel)
                    {
                        toClose = channelDict[i];
                        break;
                    }
                }
            }

            if (toClose != null)
            {
                try { toClose.Close(); } catch { }
                return true;
            }

            return false;
        }

        public INetworkChannel PeekChannel(string channelName)
        {
            lock (syncRoot)
            {
                for (int i = 0; i < channelDict.Count; i++)
                {
                    if (channelDict[i].ChannelName == channelName)
                    {
                        return channelDict[i];
                    }
                }
            }

            return null;
        }

        public bool HasChannel(string channelName)
        {
            lock (syncRoot)
            {
                for (int i = 0; i < channelDict.Count; i++)
                {
                    if (channelDict[i].ChannelName == channelName)
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
                return new List<INetworkChannel>(channelDict);
            }
        }

        public void OnInit(object createParam)
        {
            channelDict = new List<INetworkChannel>();
            channelDictRemove = new List<INetworkChannel>();
        }

        /// <summary>
        /// 开启Update线程
        /// </summary>
        public void StartThread()
        {
#if UNITY_WEBGL
            Log.Error("WebGL不支持.Net多线程");
#endif
            if (netRun == null)
            {
                netRun = new Thread(new ThreadStart(ThreadOnUpdate)) { IsBackground = true };
                netRun.Start();
            }
        }

        /// <summary>
        /// Update线程
        /// </summary>
        private void ThreadOnUpdate()
        {
            while (true)
            {
                try
                {
                    Update();
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
                Update();
            }
        }

        private void Update()
        {
            // perform removals and prepare tick list under lock, but execute ticks outside lock
            List<INetworkChannel> removals = null;
            List<INetworkChannel> tickList = null;

            lock (syncRoot)
            {
                if (channelDictRemove.Count > 0)
                {
                    removals = new List<INetworkChannel>(channelDictRemove);
                    for (int i = 0; i < removals.Count; i++)
                    {
                        channelDict.Remove(removals[i]);
                    }
                    channelDictRemove.Clear();
                }

                if (channelDict.Count > 0)
                {
                    tickList = new List<INetworkChannel>(channelDict);
                }
            }

            if (tickList != null)
            {
                for (int i = 0; i < tickList.Count; i++)
                {
                    try
                    {
                        tickList[i].TickRefresh();
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
            List<INetworkChannel> toClose;
            lock (syncRoot)
            {
                toClose = new List<INetworkChannel>(channelDict);
                channelDict.Clear();
            }

            for (int i = 0; i < toClose.Count; i++)
            {
                try { toClose[i].Close(); } catch { }
            }
        }
    }
}