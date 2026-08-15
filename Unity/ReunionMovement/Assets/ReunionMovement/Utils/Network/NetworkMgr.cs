using Cysharp.Threading.Tasks;
using ReunionMovement.Core.Base;
using System;
using System.Collections.Generic;

namespace ReunionMovement.Common.Util
{
    /// <summary>
    /// 网络通道管理器 —— 同时作为 MonoBehaviour 单例和 GameEngine 模块（ISystemUpdatable 驱动移除队列消费）。
    /// </summary>
    public sealed class NetworkMgr : SingletonMgr<NetworkMgr>, ICustomSystem, ISystemUpdatable
    {
        /// <summary>网络通道（KCP/Telepathy 的线程与连接）跨场景保持，避免切场景断开连接</summary>
        protected override bool IsPersistentAcrossScenes => true;

        // 主通道列表（用于 Tick 迭代，List 遍历比 Dictionary 快）
        List<INetworkChannel> channelList = new List<INetworkChannel>();
        // 按名称索引（O(1) 查找，与 channelList 并行维护）
        Dictionary<string, INetworkChannel> channelIndex = new Dictionary<string, INetworkChannel>();
        List<INetworkChannel> channelDictRemove = new List<INetworkChannel>();
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
            INetworkChannel toClose = null;
            lock (syncRoot)
            {
                // 如果同名通道已存在，先移除旧的（锁外 Close，避免持锁执行网络操作）
                if (!string.IsNullOrEmpty(channel.ChannelName) && channelIndex.TryGetValue(channel.ChannelName, out var existing))
                {
                    channelList.Remove(existing);
                    channelIndex.Remove(channel.ChannelName);
                    toClose = existing;
                }
                channelList.Add(channel);
                if (!string.IsNullOrEmpty(channel.ChannelName))
                {
                    channelIndex[channel.ChannelName] = channel;
                }
            }

            // 关闭被替换的旧通道（Telepathy/KCP 的线程、socket、事件订阅），防止连接泄漏
            toClose?.Close();
        }

        /// <summary>
        /// 将 Channel 加入延迟删除队列，在下次 Update（引擎模块或 MonoBehaviour 兜底）时安全移除
        /// （避免在 TickRefresh 回调中直接修改列表）。
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

        /// <summary>
        /// 移除通道并关闭它（与 CloseChannel 行为一致：锁外 Close，避免持锁执行网络操作）。
        /// 仅从集合移除而不 Close 会导致通道的线程/socket/事件订阅泄漏。
        /// </summary>
        public void RemoveChannel(string channelName)
        {
            INetworkChannel toClose = null;
            lock (syncRoot)
            {
                if (channelIndex.TryGetValue(channelName, out var found))
                {
                    channelList.Remove(found);
                    channelIndex.Remove(channelName);
                    toClose = found;
                }
            }
            if (toClose != null)
            {
                toClose.Close();
                return;
            }
            Log.Error("不存在：" + channelName);
        }

        /// <summary>
        /// 移除通道并关闭它（锁外 Close，避免持锁执行网络操作）。
        /// </summary>
        public void RemoveChannel(INetworkChannel channel)
        {
            if (channel == null) return;
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
                return;
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

        /// <summary>ICustomSystem 初始化进度（恒为 100，NetworkMgr 由 Awake 完成初始化）</summary>
        public double InitProgress => 100;

        /// <summary>
        /// ICustomSystem 初始化（幂等）：Awake 已通过 OnInit(null) 完成，这里仅保证接口契约。
        /// 注意：不能在此重复调用 OnInit 清理通道 —— NetworkMgr 跨场景/跨引擎重建存活，
        /// 引擎重初始化时误清空会断开在途的网络连接。
        /// </summary>
        public UniTask Init()
        {
            return UniTask.CompletedTask;
        }

        /// <summary>ISystemUpdatable：GameEngine 运行时统一驱动移除队列消费</summary>
        void ISystemUpdatable.Update(float logicTime, float realTime)
        {
            DrainRemoveQueue();
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
        /// 消费延迟删除队列（ScheduleRemove 入队），同步清理 List 和 Index，
        /// 并关闭被移除的通道（锁外 Close，与 RemoveChannel/CloseChannel 行为一致）。
        /// 本项目通道由 UniversalNetworkBehaviour.Update 直接驱动 TickRefresh，
        /// 因此这里不重复 Tick。
        /// </summary>
        private void DrainRemoveQueue()
        {
            if (channelDictRemove.Count == 0) return;
            List<INetworkChannel> toClose = null;
            lock (syncRoot)
            {
                if (channelDictRemove.Count == 0) return;
                for (int i = channelDictRemove.Count - 1; i >= 0; i--)
                {
                    var ch = channelDictRemove[i];
                    channelList.Remove(ch);
                    // 仅当索引仍映射到该通道时才删除：
                    // 防止同帧内 ScheduleRemove(旧通道) 后又 AddChannel(同名新通道) 时,
                    // 延迟删除误删新通道的索引,导致 channelList 与 channelIndex 永久不一致。
                    if (channelIndex.TryGetValue(ch.ChannelName, out var mapped) && ReferenceEquals(mapped, ch))
                    {
                        channelIndex.Remove(ch.ChannelName);
                    }
                    // 收集待关闭通道：此前仅从集合移除不 Close，第三方直接注册的通道
                    // 经 ScheduleRemove 移除后其线程/socket/事件订阅永久泄漏
                    (toClose ?? (toClose = new List<INetworkChannel>())).Add(ch);
                }
                channelDictRemove.Clear();
            }

            if (toClose != null)
            {
                foreach (var ch in toClose)
                {
                    try { ch.Close(); }
                    catch (Exception ex) { Log.Warning("[NetworkMgr] 延迟移除通道 Close 异常（已隔离）: {0}", ex.Message); }
                }
            }
        }

        /// <summary>
        /// MonoBehaviour Update 兜底：仅在 GameEngine 未运行时消费移除队列，
        /// 引擎运行时会通过 ISystemUpdatable.Update 驱动，避免双重消费。
        /// 注意：通道 Tick 由 UniversalNetworkBehaviour.Update 自行驱动，此处不重复。
        /// </summary>
        void Update()
        {
            // 引擎运行中由 ISystemUpdatable.Update 驱动，避免双重消费；
            // 引擎未运行才用 MonoBehaviour Update 兜底
            if (ModuleRuntime.IsEngineRunning) return;
            DrainRemoveQueue();
        }

        public void OnTermination()
        {
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

        protected override void OnDestroy()
        {
            OnTermination();
            base.OnDestroy();
        }
    }
}