using Gear.Common;
using Gear.Framework.Runtime.Base;
using Gear.Framework.Runtime.Resources;
using Gear.Framework.Runtime.Scene;
using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.Rendering;

namespace Gear.Framework.Runtime.Sound
{
    /// <summary>
    /// 声音系统
    /// </summary>
    [ModuleOrder(70, typeof(Gear.Framework.Runtime.Resources.ResourcesSystem), typeof(Gear.Framework.Runtime.Resources.AddressableSystem))]
    public partial class SoundSystem : ICustomSystem, ISystemUpdatable, ISystemDisposable
    {
        #region 单例与初始化
        private static readonly Lazy<SoundSystem> instance = new(() => new SoundSystem());
        public static SoundSystem Instance => instance.Value;

        /// <summary>
        /// 引擎已启动时取注册实例；否则回退到单例（编辑器 / 预初始化 / 独立使用）。
        /// 调用方统一用本方法，避免直接散落 <c>Xxx.Instance</c>；注册点仍必须用 <c>Instance</c>。
        /// </summary>
        public static SoundSystem GetOrInstance()
        {
            return Gear.Framework.Runtime.Modules.TryGet(out SoundSystem system) ? system : Instance;
        }

        public bool isInited { get; private set; }

        private double initProgress = 0;
        public double InitProgress { get { return initProgress; } }
        #endregion

        #region 数据
        // 背景音乐根节点
        public GameObject musicRoot { get; private set; }
        public GameObject sfxRoot { get; private set; }
        // 音效根节点
        private AudioSource source;
        // 音乐 AudioSource 公共访问器（避免外部使用反射）
        public AudioSource MusicAudioSource
        {
            get
            {
                EnsureAudioSource();
                return source;
            }
        }
        // 淡入淡出时间
        public float fadeDuration = 3.0f;
        // 目标音量
        public float targetVolume = 1.0f;

        // 启动时预热的音频索引列表（在 Inspector 中配置）
        public List<int> preloadAudioIndices = new List<int>();

        string poolPath = "Prefabs/Sound/SoundItem";
        int currentMusicIndex;
        private SoundConfigContainer soundConfigContainer;
        // 将声音配置列表转换为字典以加快查找速度
        private Dictionary<int, SoundConfig> soundConfigDict;
        // 缓存已加载的AudioClip（真正的 LRU：LinkedList + Dictionary 实现 O(1) 访问与驱逐）
        private Dictionary<string, AudioClip> audioClipCache;
        private const int MaxAudioClipCacheSize = 64;
        private readonly LinkedList<string> audioClipCacheOrder = new LinkedList<string>();
        private readonly Dictionary<string, LinkedListNode<string>> audioClipCacheNodes
            = new Dictionary<string, LinkedListNode<string>>();
        // 并发加载去重：同一路径加载在途时复用同一个 TCS（防止重复加载同一 clip）
        private readonly SingleFlightLoader<AudioClip> audioClipLoadGate = new SingleFlightLoader<AudioClip>();
        // 从 Addressables 加载的 clip 来源记录（用于按来源正确释放：Addressables.Release vs Resources 卸载）
        private readonly HashSet<string> addressableClips = new HashSet<string>();

        // 启动时预设对象池
        public List<StartupPool> startupPools = new List<StartupPool>();
        //预设对象池
        private Dictionary<GameObject, IObjectPool<GameObject>> pooledObjects = new Dictionary<GameObject, IObjectPool<GameObject>>();
        //生成对象池 特效
        private Dictionary<GameObject, GameObject> sfxObjects = new Dictionary<GameObject, GameObject>();
        // sfxObjects 的平行缓存（obj → AudioSource）：Spawn 时登记，IsClipInUse 驱逐扫描 O(1) 无需 GetComponent
        private readonly Dictionary<GameObject, AudioSource> sfxSources = new Dictionary<GameObject, AudioSource>();
        // 上次清扫已销毁对象残留的时间（低频清扫，避免逐帧遍历字典）
        private float lastSfxCleanupTime = -1f;
        private const float SfxCleanupInterval = 10f;

        /// <summary>
        /// 同一音效的最小重触发间隔（秒，0=禁用）：快速连点/高频触发时静默丢弃密集重放，
        /// 防止瞬时叠加 20+ 个同声源（音量叠加削顶、AudioSource 耗尽）。
        /// </summary>
        public float minSfxRetriggerInterval = 0.05f;

        /// <summary>音效最近播放时间（path → unscaledTime），配合 minSfxRetriggerInterval 限流</summary>
        private readonly Dictionary<string, float> lastSfxPlayTimes = new Dictionary<string, float>();
        // 淡入淡出状态机（由 Update 驱动，配合 UniTaskCompletionSource 实现零 GC）
        private enum FadeState { None, FadingIn, FadingOut }
        private FadeState fadeState = FadeState.None;
        private float fadeTimer;
        private float fadeStartVolume;
        private float fadeTargetVolume;
        private UniTaskCompletionSource<bool> fadeTcs;

        // 可复用列表（避免 SetSfxProperties / RecycleAll 等方法每次分配 List）
        private List<GameObject> reusableSpawnedList;
        private List<GameObject> GetSpawnedList()
        {
            return reusableSpawnedList ?? (reusableSpawnedList = new List<GameObject>());
        }
        #endregion

        // 确保 AudioSource 存在（如果被销毁则重建）
        private void EnsureAudioSource()
        {
            if (source != null)
            {
                // Unity 的特殊 "null"（对象已被销毁）在 == null 时也会为 true，
                // 但我们 still check to be safe using UnityEngine.Object reference.
                return;
            }

            // If musicRoot exists try to get or add AudioSource, otherwise recreate roots
            if (musicRoot == null)
            {
                CreateAudioRoot();
            }
            else
            {
                source = musicRoot.GetComponent<AudioSource>();
                if (source == null)
                {
                    source = musicRoot.AddComponent<AudioSource>();
                }
            }
        }

        // 预热取消令牌（Clear 时取消正在进行的预热任务）
        private CancellationTokenSource warmupCts;

        // 缓存代次：Clear() 时自增。在途的 LoadAudioClipAndCacheAsync 写缓存前校验代次一致，
        // 避免 Clear 后旧任务完成时把已清空的缓存条目复活（LRU 节点缺失 → 永不驱逐、句柄永久泄漏）
        private int cacheGeneration = 0;

        public UniTask Init()
        {
            initProgress = 0;

            ApplyLowLatencyAudioSettings();

            CreateAudioRoot();

            soundConfigContainer = Modules.Get<ResourcesSystem>().Load<SoundConfigContainer>("ScriptableObjects/SoundConfigContainer");
            if (soundConfigContainer == null || soundConfigContainer.configs == null)
            {
                Log.Error("SoundConfigContainer或其configs为空, 声音系统初始化失败!");
            }
            else
            {
                // 初始化字典和缓存
                soundConfigDict = new Dictionary<int, SoundConfig>(soundConfigContainer.configs.Count);
                foreach (var config in soundConfigContainer.configs)
                {
                    if (soundConfigDict.ContainsKey(config.Number))
                    {
                        // 配表重复 ID 静默覆盖会无告警换音，排查困难；与 LanguagesSystem 告警策略一致
                        Log.Error("SoundSystem: 声音配置重复 Number={0}（后者覆盖前者），请检查配表", config.Number);
                    }
                    soundConfigDict[config.Number] = config;
                }
                audioClipCache = new Dictionary<string, AudioClip>();
                // 兜底同步 LRU 结构：手工连续 Init（不经 Clear）时旧链表/节点会残留，
                // 在途任务写入新字典时产生重复 LinkedList 节点、驱逐顺序错乱
                audioClipCacheOrder?.Clear();
                audioClipCacheNodes?.Clear();
                addressableClips.Clear();
            }

            CreatePools();

            // 预热音频（后台加载，不阻塞初始化完成）
            warmupCts = new CancellationTokenSource();
            _ = WarmupAudioClipsAsync(warmupCts.Token);

            initProgress = 100;
            isInited = true;
            // 启动早期 GameOption.ApplyLightOptions 可能因 SoundSystem 未初始化而跳过音频设置，
            // 此处补齐音量/淡入淡出，避免用户已保存的选项在初始化后不生效
            try
            {
                fadeDuration = GameOption.CurrentOption.musicFadeTime;
                SetMusicProperties(GameOption.CurrentOption.musicVolume, GameOption.CurrentOption.musicMuted);
                SetSfxProperties(GameOption.CurrentOption.sfxVolume, GameOption.CurrentOption.sfxMuted);
            }
            catch (Exception ex)
            {
                Log.Warning("初始化后应用选项音量设置失败: {0}", ex.Message);
            }
            Log.Debug("SoundSystem 初始化完成");

            return UniTask.CompletedTask;
        }

        /// <summary>
        /// 预热指定的音频剪辑到缓存，消除首次播放延迟
        /// </summary>
        private async UniTask WarmupAudioClipsAsync(CancellationToken ct)
        {
            if (preloadAudioIndices == null || preloadAudioIndices.Count == 0) return;

            Log.Debug("[SoundSystem] 开始预热 {0} 个音频...", preloadAudioIndices.Count);
            int loaded = 0;
            foreach (int index in preloadAudioIndices)
            {
                if (ct.IsCancellationRequested) break;
                if (soundConfigDict != null && soundConfigDict.TryGetValue(index, out SoundConfig config))
                {
                    await GetAudioClipAsync(config.Path, config.Name);
                    loaded++;
                }
            }
            Log.Debug("[SoundSystem] 音频预热完成: {0}/{1}", loaded, preloadAudioIndices.Count);
        }

        public void Update(float logicTime, float realTime)
        {
            // 快速路径：无淡入淡出时直接返回，避免每帧无效检查
            if (fadeState == FadeState.None) return;

            // 淡入淡出状态机驱动。
            // 必须用 realTime（unscaled）而不是 logicTime：暂停（timeScale=0）时 logicTime 为 0，
            // 淡出永远走不到 100%，await FadeOut() 会永久挂起，恢复时残留半透明音量；
            // 音效淡变本身也不该随游戏倍速变快/变慢。
            fadeTimer += realTime;
            float t = fadeDuration > 0 ? Mathf.Clamp01(fadeTimer / fadeDuration) : 1f;
            if (source != null)
            {
                source.volume = Mathf.Lerp(fadeStartVolume, fadeTargetVolume, t);
            }
            if (t >= 1f)
            {
                if (fadeState == FadeState.FadingOut && source != null)
                {
                    source.volume = 0f;
                    source.Stop();
                }
                var tcs = fadeTcs;
                fadeState = FadeState.None;
                fadeTcs = null;
                tcs?.TrySetResult(true);
            }
        }

        public void Clear()
        {
            Log.Debug("SoundSystem 清除数据");
            isInited = false;
            // 代次自增：使所有在途加载任务写缓存前校验失败（见 LoadAudioClipAndCacheAsync）
            cacheGeneration++;
            // 取消正在进行的预热任务
            warmupCts?.Cancel();
            warmupCts = null;
            // 取消正在进行的淡入淡出
            fadeTcs?.TrySetCanceled();
            fadeTcs = null;
            fadeState = FadeState.None;

            foreach (var path in loadedPoolPaths)
                Modules.Get<ResourcesSystem>().DeleteAssetCache(path);
            loadedPoolPaths.Clear();
            startupPools.Clear();

            // 释放预设对象池中的所有 GameObject（Dispose 比 Clear 更彻底）
            foreach (var pool in pooledObjects.Values)
            {
                if (pool is IDisposable disposable)
                    disposable.Dispose();
                else
                    pool.Clear();
            }
            pooledObjects.Clear();

            // 销毁所有已生成的音效对象（避免 GameObject 泄漏）
            foreach (var sfxObj in sfxObjects.Values)
            {
                if (sfxObj != null)
                    UnityEngine.Object.Destroy(sfxObj);
            }
            sfxObjects.Clear();
            sfxSources.Clear();
            lastSfxCleanupTime = -1f;

            // 停止音乐并释放 AudioSource
            if (source != null)
            {
                source.Stop();
                source.clip = null;
            }

            // 卸载缓存中的 AudioClip，释放底层资源（按来源：Addressables 用 ReleaseAsset，Resources 直接卸载）
            if (audioClipCache != null)
            {
                foreach (var kv in audioClipCache)
                {
                    if (addressableClips.Contains(kv.Key))
                    {
                        if (kv.Value != null) AddressableSystem.GetOrInstance().ReleaseAsset(kv.Value);
                    }
                    else
                    {
                        Modules.Get<ResourcesSystem>().DeleteAssetCache(kv.Key);
                    }
                }
                audioClipCache.Clear();
            }
            addressableClips.Clear();
            audioClipCacheOrder?.Clear();
            audioClipCacheNodes?.Clear();
            // 取消在途加载的等待方（SingleFlightLoader 的 CancelAll 语义）
            audioClipLoadGate?.CancelAll();
            soundConfigDict?.Clear();
            if (!ReferenceEquals(soundConfigContainer, null))
                Modules.Get<ResourcesSystem>().DeleteAssetCache("ScriptableObjects/SoundConfigContainer");
            soundConfigContainer = null;
            lastSfxPlayTimes.Clear();
            // 重置音效 prefab 缓存，避免重 Init 时误用旧引用
            cachedSfxPrefab = null;

            // 销毁音频根节点
            if (musicRoot != null)
            {
                UnityEngine.Object.Destroy(musicRoot);
                musicRoot = null;
                source = null;
            }
            if (sfxRoot != null)
            {
                UnityEngine.Object.Destroy(sfxRoot);
                sfxRoot = null;
            }
        }

    }

    /// <summary>
    /// 启动池
    /// </summary>
    [System.Serializable]
    public class StartupPool
    {
        public int size;
        public Transform parent;
        public GameObject prefab;
    }
}
