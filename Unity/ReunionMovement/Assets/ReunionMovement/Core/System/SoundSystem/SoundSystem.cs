using ReunionMovement.Common;
using ReunionMovement.Core.Base;
using ReunionMovement.Core.Resources;
using ReunionMovement.Core.Scene;
using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.Threading;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.Rendering;

namespace ReunionMovement.Core.Sound
{
    /// <summary>
    /// 声音系统
    /// </summary>
    public class SoundSystem : ICustomSystem
    {
        #region 单例与初始化
        private static readonly Lazy<SoundSystem> instance = new(() => new SoundSystem());
        public static SoundSystem Instance => instance.Value;

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
        // 缓存已加载的AudioClip（LRU 限制防止内存无限增长）
        private Dictionary<string, AudioClip> audioClipCache;
        private const int MaxAudioClipCacheSize = 64;
        private readonly Queue<string> audioClipCacheOrder = new Queue<string>();

        // 启动时预设对象池
        public List<StartupPool> startupPools = new List<StartupPool>();
        //预设对象池
        private Dictionary<GameObject, IObjectPool<GameObject>> pooledObjects = new Dictionary<GameObject, IObjectPool<GameObject>>();
        //生成对象池 特效
        private Dictionary<GameObject, GameObject> sfxObjects = new Dictionary<GameObject, GameObject>();
        // 淡入淡出状态机（由 Update 驱动，配合 UniTaskCompletionSource 实现零 GC）
        private enum FadeState { None, FadingIn, FadingOut }
        private FadeState fadeState = FadeState.None;
        private float fadeTimer;
        private float fadeStartVolume;
        private float fadeTargetVolume;
        private UniTaskCompletionSource<bool> fadeTcs;        
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

        public UniTask Init()
        {
            initProgress = 0;

            ApplyLowLatencyAudioSettings();

            CreateAudioRoot();

            soundConfigContainer = ResourcesSystem.Instance.Load<SoundConfigContainer>("ScriptableObjects/SoundConfigContainer");
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
                    soundConfigDict[config.Number] = config;
                }
                audioClipCache = new Dictionary<string, AudioClip>();
            }

            CreatePools();

            // 预热音频（后台加载，不阻塞初始化完成）
            warmupCts = new CancellationTokenSource();
            _ = WarmupAudioClipsAsync(warmupCts.Token);

            initProgress = 100;
            isInited = true;
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

            // 淡入淡出状态机驱动
            fadeTimer += logicTime;
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
            // 取消正在进行的预热任务
            warmupCts?.Cancel();
            warmupCts = null;
            // 取消正在进行的淡入淡出
            fadeTcs?.TrySetCanceled();
            fadeTcs = null;
            fadeState = FadeState.None;

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

            // 停止音乐并释放 AudioSource
            if (source != null)
            {
                source.Stop();
                source.clip = null;
            }

            audioClipCache?.Clear();
            audioClipCacheOrder?.Clear();
            soundConfigDict?.Clear();
            soundConfigContainer = null;

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

        /// <summary>
        /// 设置音乐属性（供 GameOption 直接调用，避免反射）
        /// </summary>
        public void SetMusicProperties(float volume, bool muted)
        {
            EnsureAudioSource();
            if (source != null)
            {
                source.volume = volume;
                source.mute = muted;
            }
        }

        /// <summary>
        /// 设置音效属性（批量更新所有活跃音效）
        /// </summary>
        public void SetSfxProperties(float volume, bool muted)
        {
            if (sfxObjects.Count == 0) return;
            // 防御性拷贝，避免遍历时集合被修改
            var snapshot = new List<GameObject>(sfxObjects.Keys);
            foreach (var obj in snapshot)
            {
                if (obj != null)
                {
                    var item = obj.GetComponent<SoundItem>();
                    if (item != null)
                    {
                        item.SetProperties(volume, muted);
                    }
                }
            }
        }

        /// <summary>
        /// 播放音乐
        /// </summary>
        /// <param name="index">音乐配置索引</param>
        /// <param name="volume">音量（null 表示使用默认音量）</param>
        public async UniTask PlayMusic(int index, float? volume = null)
        {
            if (soundConfigDict != null && soundConfigDict.TryGetValue(index, out SoundConfig soundConfig))
            {
                currentMusicIndex = index;
                AudioClip audioClip = await GetAudioClipAsync(soundConfig.Path, soundConfig.Name);
                if (audioClip != null)
                {
                    EnsureAudioSource();
                    if (source == null) return;

                    source.clip = audioClip;
                    source.volume = volume ?? GameOption.currentOption.musicVolume;
                    source.loop = true;
                    source.mute = GameOption.currentOption.musicMuted;
                    source.Play();
                }
            }
        }

        /// <summary>
        /// 播放BGM
        /// </summary>
        public void PlayMusic()
        {
            EnsureAudioSource();
            source?.Play();
        }

        /// <summary>
        /// 暂停BGM
        /// </summary>
        public void PauseMusic()
        {
            EnsureAudioSource();
            source?.Pause();
        }

        /// <summary>
        /// 结束背景音乐
        /// </summary>
        public void StopMusic()
        {
            EnsureAudioSource();
            source?.Stop();
        }

        /// <summary>
        /// 音乐切换-带渐入渐出效果（优化版：新曲加载与淡出并行，减少等待时间）
        /// </summary>
        /// <param name="index"></param>
        public async UniTask PlaySwitch(int index)
        {
            if (soundConfigDict == null || !soundConfigDict.TryGetValue(index, out SoundConfig soundConfig))
                return;

            // 启动淡出（不等待），同时并行加载新曲目
            var fadeOutTask = FadeOut();
            AudioClip newClip = await GetAudioClipAsync(soundConfig.Path, soundConfig.Name);

            // 等待淡出完成
            await fadeOutTask;

            // 切换曲目并淡入
            EnsureAudioSource();
            if (source != null && newClip != null)
            {
                source.clip = newClip;
                source.volume = 0f;
                source.loop = true;
                source.mute = GameOption.currentOption.musicMuted;
                source.Play();
                currentMusicIndex = index;
            }
            await FadeIn();
        }

        /// <summary>
        /// 渐入（由 Update 驱动，避免每帧 async 状态机分配）
        /// </summary>
        private UniTask FadeIn()
        {
            EnsureAudioSource();
            if (source == null) return UniTask.CompletedTask;

            // 如果上一次淡入淡出尚未完成，先完成旧的 TCS 防止泄漏
            fadeTcs?.TrySetResult(false);

            fadeStartVolume = 0f;
            fadeTargetVolume = GameOption.currentOption.musicVolume;
            fadeTimer = 0f;
            fadeState = FadeState.FadingIn;
            fadeTcs = new UniTaskCompletionSource<bool>();
            return fadeTcs.Task;
        }

        /// <summary>
        /// 渐出（由 Update 驱动，配合 UniTaskCompletionSource 实现零 GC）
        /// </summary>
        private UniTask FadeOut()
        {
            EnsureAudioSource();
            if (source == null) return UniTask.CompletedTask;

            // 如果上一次淡入淡出尚未完成，先完成旧的 TCS 防止泄漏
            fadeTcs?.TrySetResult(false);

            fadeStartVolume = source.volume;
            fadeTargetVolume = 0f;
            fadeTimer = 0f;
            fadeState = FadeState.FadingOut;
            fadeTcs = new UniTaskCompletionSource<bool>();
            return fadeTcs.Task;
        }

        #region 播放声音
        /// <summary>
        /// 播放声音
        /// </summary>
        /// <param name="index">声音配置索引</param>
        /// <param name="emitter">声音发射器</param>
        /// <param name="loop">是否循环</param>
        public async UniTask PlaySfx(int index, Transform emitter = null, bool loop = false, float? volume = null, float pitch = 1f)
        {
            try
            {
                if (soundConfigDict != null && soundConfigDict.TryGetValue(index, out SoundConfig soundConfig))
                {
                    AudioClip clip = await GetAudioClipAsync(soundConfig.Path, soundConfig.Name);
                    if (clip != null && startupPools.Count > 0)
                    {
                        // 遍历所有对象池，使用第一个有效的 prefab
                        GameObject obj = null;
                        foreach (var pool in startupPools)
                        {
                            if (pool.prefab != null)
                            {
                                obj = pool.prefab;
                                break;
                            }
                        }
                        if (obj != null)
                        {
                            GameObject go = Spawn(obj);
                            if (go != null)
                            {
                                if (emitter != null)
                                {
                                    go.transform.SetParent(emitter);
                                    go.transform.localPosition = Vector3.zero;
                                }
                                SoundItem soundObj = go.GetComponent<SoundItem>();
                                if (soundObj != null)
                                {
                                    float effectiveVolume = volume ?? GameOption.currentOption.sfxVolume;
                                    soundObj.Processing(clip, emitter, loop, effectiveVolume, GameOption.currentOption.sfxMuted, pitch);
                                }
                                else
                                {
                                    Log.Warning("PlaySfx: SoundItem 组件缺失于 {0}", go.name);
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error("PlaySfx 异常: {0}", ex.Message);
            }
        }

        /// <summary>
        /// 停止所有音效
        /// </summary>
        public void StopSfx()
        {
            RecycleAll();
        }
        #endregion

        /// <summary>
        /// 应用自适应音频设置：
        /// - 有线耳机/扬声器：使用较小 DSP buffer 以降低延迟
        /// - 蓝牙耳机：使用较大 DSP buffer，因为蓝牙 A2DP 自带 150-300ms 延迟，
        ///   过小的 buffer 反而导致音频欠载（卡顿/爆音），无法降低实际延迟
        /// </summary>
        private void ApplyLowLatencyAudioSettings()
        {
            try
            {
                var config = AudioSettings.GetConfiguration();

                // 判断是否使用蓝牙音频输出
                bool isBluetooth = IsBluetoothAudioActive();

#if UNITY_ANDROID || UNITY_IOS
                if (isBluetooth)
                {
                    // 蓝牙模式：使用较大缓冲确保音频流稳定
                    // 蓝牙瓶颈在传输层，DSP buffer 大小不影响实际听到的延迟
                    config.dspBufferSize = 1024;
                    Log.Debug("[AudioSettings] 检测到蓝牙音频，使用最佳性能模式 (Buffer: 1024)");
                }
                else
                {
                    // 有线/扬声器模式：使用适中缓冲
                    // 256 对很多安卓设备过于激进，512 在有线模式下延迟可接受
                    config.dspBufferSize = 512;
                    Log.Debug("[AudioSettings] 有线/扬声器模式 (Buffer: 512)");
                }
#else
                config.dspBufferSize = 512; // 桌面端平衡延迟与性能
#endif
                config.speakerMode = AudioSpeakerMode.Stereo;
                config.sampleRate = 0;
                config.numVirtualVoices = 32;
                config.numRealVoices = 16;

                if (AudioSettings.Reset(config))
                {
                    Log.Debug("[AudioSettings] 音频配置已应用 DSP Buffer: {0} samples, Bluetooth: {1}", config.dspBufferSize, isBluetooth);
                }
                else
                {
                    Log.Debug("[AudioSettings] 音频配置重置失败，使用系统默认");
                }
            }
            catch (System.Exception ex)
            {
                Log.Warning("[AudioSettings] 音频配置异常: {0}", ex.Message);
            }
        }

        /// <summary>
        /// 检测当前是否使用蓝牙音频输出（A2DP 或 SCO）
        /// </summary>
        private bool IsBluetoothAudioActive()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var audioManager = new AndroidJavaClass("android.media.AudioManager"))
                using (var context = new AndroidJavaClass("com.unity3d.player.UnityPlayer")
                    .GetStatic<AndroidJavaObject>("currentActivity"))
                {
                    var service = context.Call<AndroidJavaObject>("getSystemService", "audio");
                    if (service == null) return false;

                    bool isBluetoothA2dpOn = service.Call<bool>("isBluetoothA2dpOn");
                    bool isBluetoothScoOn = service.Call<bool>("isBluetoothScoOn");
                    return isBluetoothA2dpOn || isBluetoothScoOn;
                }
            }
            catch
            {
                // 检测失败时保守估计，使用较大 buffer
                return true;
            }
#elif UNITY_IOS && !UNITY_EDITOR
            // iOS 上无法直接检测蓝牙，但可以用 AudioSession 的端口类型推断
            // 保守策略：iOS 蓝牙音频延迟通常更高，使用较大 buffer
            try
            {
                // 检查当前音频输出类型（通过 AudioSettings 间接判断）
                var config = AudioSettings.GetConfiguration();
                // iOS 有线耳机通常使用 44100 采样率，蓝牙设备可能不同
                // 由于无法 100% 准确检测，移动端默认使用较大 buffer
                return true; // 保守策略
            }
            catch
            {
                return true;
            }
#else
            return false;
#endif
        }

        /// <summary>
        /// 创建音频根节点
        /// </summary>
        public void CreateAudioRoot()
        {
            // 防止重复创建
            if (musicRoot != null) return;

            musicRoot = new GameObject("MusicRoot");
            musicRoot.transform.position = Vector3.zero;

            sfxRoot = new GameObject("SfxRoot");
            sfxRoot.transform.position = Vector3.zero;

            source = musicRoot.AddComponent<AudioSource>();

            GameObject.DontDestroyOnLoad(musicRoot);
            GameObject.DontDestroyOnLoad(sfxRoot);
        }

        /// <summary>
        /// 创建对象池
        /// </summary>
        public void CreatePools()
        {
            StartupPool pool = new StartupPool();
            pool.size = 20;
            pool.parent = sfxRoot.transform;
            pool.prefab = ResourcesSystem.Instance.Load<GameObject>(poolPath);

            if (pool.prefab == null)
            {
                Log.Error("SoundSystem 对象池 prefab 加载失败，路径: {0}", poolPath);
                return;
            }

            startupPools.Add(pool);

            for (int i = 0; i < startupPools.Count; i++)
            {
                CreatePool(startupPools[i].prefab, startupPools[i].size, startupPools[i].parent);
            }
        }

        /// <summary>
        /// 创建对象池
        /// </summary>
        /// <param name="prefab"></param>
        /// <param name="size"></param>
        /// <param name="parent"></param>
        public void CreatePool(GameObject prefab, int size, Transform parent)
        {
            if (prefab != null && !pooledObjects.ContainsKey(prefab))
            {
                var newPool = new UnityEngine.Pool.ObjectPool<GameObject>(
                    createFunc: () => GameObject.Instantiate(prefab, parent),
                    actionOnGet: (obj) => obj.SetActive(true),
                    actionOnRelease: (obj) =>
                    {
                        obj.transform.SetParent(sfxRoot.transform);
                        obj.SetActive(false);
                    },
                    actionOnDestroy: (obj) => GameObject.Destroy(obj),
                    collectionCheck: true, // 安全检查，防止对象被重复释放
                    defaultCapacity: size,
                    maxSize: size * 2 // 可根据需求调整
                );

                // 预热对象池
                for (int i = 0; i < size; i++)
                {
                    var obj = newPool.Get();
                    newPool.Release(obj);
                }

                pooledObjects.Add(prefab, newPool);
            }
        }

        /// <summary>
        /// 生成
        /// </summary>
        /// <param name="prefab"></param>
        /// <param name="parent"></param>
        /// <param name="position"></param>
        /// <returns></returns>
        public GameObject Spawn(GameObject prefab, Transform parent, Vector3 position)
        {
            return Spawn(prefab, parent, position, Quaternion.identity);
        }

        /// <summary>
        /// 生成
        /// </summary>
        /// <param name="prefab"></param>
        /// <param name="position"></param>
        /// <param name="rotation"></param>
        /// <returns></returns>
        public GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            return Spawn(prefab, sfxRoot.transform, position, rotation);
        }

        /// <summary>
        /// 生成
        /// </summary>
        /// <param name="prefab"></param>
        /// <param name="parent"></param>
        /// <returns></returns>
        public GameObject Spawn(GameObject prefab, Transform parent)
        {
            return Spawn(prefab, parent, Vector3.zero, Quaternion.identity);
        }

        /// <summary>
        /// 生成
        /// </summary>
        /// <param name="prefab"></param>
        /// <param name="position"></param>
        /// <returns></returns>
        public GameObject Spawn(GameObject prefab, Vector3 position)
        {
            return Spawn(prefab, sfxRoot.transform, position, Quaternion.identity);
        }

        /// <summary>
        /// 生成
        /// </summary>
        /// <param name="prefab"></param>
        /// <returns></returns>
        public GameObject Spawn(GameObject prefab)
        {
            return Spawn(prefab, sfxRoot.transform, Vector3.zero, Quaternion.identity);
        }

        /// <summary>
        /// 生成
        /// </summary>
        /// <param name="prefab"></param>
        /// <param name="parent"></param>
        /// <param name="position"></param>
        /// <param name="rotation"></param>
        /// <returns></returns>
        public GameObject Spawn(GameObject prefab, Transform parent, Vector3 position, Quaternion rotation)
        {
            if (prefab == null) return null;

            if (!pooledObjects.TryGetValue(prefab, out var pool))
            {
                // 如果没有为该预制件创建池，则动态创建一个
                CreatePool(prefab, 10, parent);
                pool = pooledObjects[prefab];
            }

            GameObject obj = pool.Get();
            obj.transform.SetParent(parent);
            obj.transform.localPosition = position;
            obj.transform.localRotation = rotation;

            sfxObjects[obj] = prefab;
            return obj;
        }

        /// <summary>
        /// 回收利用
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="obj"></param>
        public void Recycle<T>(T obj) where T : Component
        {
            Recycle(obj.gameObject);
        }

        /// <summary>
        /// 回收利用
        /// </summary>
        /// <param name="obj"></param>
        public void Recycle(GameObject obj)
        {
            if (sfxObjects.TryGetValue(obj, out GameObject prefab))
            {
                if (pooledObjects.TryGetValue(prefab, out var pool))
                {
                    pool.Release(obj);
                    sfxObjects.Remove(obj);
                }
                else
                {
                    // 如果没有对应的池，直接销毁
                    Log.Warning("没有找到预制件 {0} 对应的对象池，直接销毁对象。", prefab.name);
                    UnityEngine.Object.Destroy(obj);
                }
            }
            else
            {
                // 如果对象不在生成池中，则直接销毁
                Log.Warning("对象 {0} 不在生成池中，直接销毁。", obj.name);
                UnityEngine.Object.Destroy(obj);
            }
        }

        /// <summary>
        /// 回收所有实例
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="prefab"></param>
        public void RecycleAll<T>(T prefab) where T : Component
        {
            RecycleAll(prefab.gameObject);
        }

        /// <summary>
        /// 回收所有实例
        /// </summary>
        /// <param name="prefab"></param>
        public void RecycleAll(GameObject prefab)
        {
            // 使用 ToList() 创建副本，避免迭代时修改集合
            var spawned = sfxObjects.Where(kvp => kvp.Value == prefab).Select(kvp => kvp.Key).ToList();
            foreach (var obj in spawned)
            {
                Recycle(obj);
            }
        }

        /// <summary>
        /// 回收所有实例
        /// </summary>
        public void RecycleAll()
        {
            if (sfxObjects.Count == 0) return;
            // 创建一个键的副本进行迭代，以避免在回收时修改集合
            var allSpawnedKeys = new List<GameObject>(sfxObjects.Keys);
            foreach (var obj in allSpawnedKeys)
            {
                // Recycle会从sfxObjects中移除对象，所以我们需要检查键是否存在
                if (sfxObjects.ContainsKey(obj))
                {
                    Recycle(obj);
                }
            }
        }

        /// <summary>
        /// 对象池计数
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="prefab"></param>
        /// <returns></returns>
        public int CountPooled<T>(T prefab) where T : Component
        {
            return CountPooled(prefab.gameObject);
        }

        /// <summary>
        /// 对象池计数
        /// </summary>
        /// <param name="prefab"></param>
        /// <returns></returns>
        public int CountPooled(GameObject prefab)
        {
            if (pooledObjects.TryGetValue(prefab, out var pool))
            {
                return pool.CountInactive;
            }
            return 0;
        }


        /// <summary>
        /// 对象池计数
        /// </summary>
        /// <returns></returns>
        public int CountAllPooled()
        {
            int count = 0;
            foreach (var pool in pooledObjects.Values)
            {
                count += pool.CountInactive;
            }
            return count;
        }

        /// <summary>
        /// 对象是否在生成对象池中
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public bool IsSpawned(GameObject obj)
        {
            return sfxObjects.ContainsKey(obj);
        }

        /// <summary>
        /// 获取生成的数量
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="prefab"></param>
        /// <returns></returns>
        public int CountSpawned<T>(T prefab) where T : Component
        {
            return CountSpawned(prefab.gameObject);
        }

        /// <summary>
        /// 获取生成的数量
        /// </summary>
        /// <param name="prefab"></param>
        /// <returns></returns>
        public int CountSpawned(GameObject prefab)
        {
            int count = 0;

            foreach (var instancePrefab in sfxObjects.Values)
            {
                if (prefab == instancePrefab)
                {
                    ++count;
                }
            }
            return count;
        }

        /// <summary>
        /// 从生成对象池中获得同类型对象
        /// </summary>
        /// <param name="prefab"></param>
        /// <param name="list"></param>
        /// <param name="appendList"></param>
        /// <returns></returns>
        public List<GameObject> GetSpawned(GameObject prefab, List<GameObject> list, bool appendList)
        {
            if (list == null)
            {
                list = new List<GameObject>();
            }
            if (!appendList)
            {
                list.Clear();
            }

            foreach (var item in sfxObjects)
            {
                if (item.Value == prefab)
                {
                    list.Add(item.Key);
                }
            }
            return list;
        }

        /// <summary>
        /// 从生成对象池中获得同类型对象
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="prefab"></param>
        /// <param name="list"></param>
        /// <param name="appendList"></param>
        /// <returns></returns>
        public List<T> GetSpawned<T>(T prefab, List<T> list, bool appendList) where T : Component
        {
            if (list == null)
            {
                list = new List<T>();
            }
            if (!appendList)
            {
                list.Clear();
            }
            var prefabObj = prefab.gameObject;

            foreach (var item in sfxObjects)
            {
                if (item.Value == prefabObj)
                {
                    list.Add(item.Key.GetComponent<T>());
                }
            }

            return list;
        }

        /// <summary>
        /// 销毁集合
        /// </summary>
        /// <param name="prefab"></param>
        public void DestroyPooled(GameObject prefab)
        {
            if (pooledObjects.TryGetValue(prefab, out var pool))
            {
                pool.Clear(); // 这将销毁池中所有非活动对象
            }
        }

        /// <summary>
        /// 销毁所有实例
        /// </summary>
        /// <param name="prefab"></param>
        public void DestroyAll(GameObject prefab)
        {
            RecycleAll(prefab);
            DestroyPooled(prefab);
        }

        /// <summary>
        /// 从缓存或资源加载音频剪辑  WAV(.wav)  AIFF/AIF(.aif, .aiff)  AU(.au)  Ogg Vorbis(.ogg)  MP3(.mp3)
        /// </summary>
        private async UniTask<AudioClip> GetAudioClipAsync(string path, string name)
        {
            string fullPath = string.Concat(path, name);
            if (audioClipCache.TryGetValue(fullPath, out AudioClip clip))
            {
                return clip;
            }

            clip = await ResourcesSystem.Instance.LoadAsync<AudioClip>(fullPath);
            if (clip != null)
            {
                // LRU 驱逐：缓存满时移除最旧的条目
                if (audioClipCache.Count >= MaxAudioClipCacheSize && audioClipCacheOrder.Count > 0)
                {
                    string oldest = audioClipCacheOrder.Dequeue();
                    audioClipCache.Remove(oldest);
                }
                audioClipCache[fullPath] = clip;
                audioClipCacheOrder.Enqueue(fullPath);
            }
            else
            {
                Log.Error("加载AudioClip失败: {0}", fullPath);
            }
            return clip;
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