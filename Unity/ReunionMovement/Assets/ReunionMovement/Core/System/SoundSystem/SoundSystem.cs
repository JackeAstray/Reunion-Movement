using ReunionMovement.Common;
using ReunionMovement.Core.Base;
using ReunionMovement.Core.Resources;
using ReunionMovement.Core.Scene;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.Rendering;

namespace ReunionMovement.Core.Sound
{
    /// <summary>
    /// 声音系统
    /// </summary>
    public class SoundSystem : ICustommSystem
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
        // 淡入淡出时间
        public float fadeDuration = 3.0f;
        // 目标音量
        public float targetVolume = 1.0f;

        string poolPath = "Prefabs/Sound/SoundItem";
        int currentMusicIndex;
        private SoundConfigContainer soundConfigContainer;
        // 将声音配置列表转换为字典以加快查找速度
        private Dictionary<int, SoundConfig> soundConfigDict;
        // 缓存已加载的AudioClip
        private Dictionary<string, AudioClip> audioClipCache;

        // 启动时预设对象池
        public List<StartupPool> startupPools = new List<StartupPool>();
        //预设对象池
        private Dictionary<GameObject, IObjectPool<GameObject>> pooledObjects = new Dictionary<GameObject, IObjectPool<GameObject>>();
        //生成对象池 特效
        private Dictionary<GameObject, GameObject> sfxObjects = new Dictionary<GameObject, GameObject>();
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

        public Task Init()
        {
            initProgress = 0;

            CreateAudioRoot();

            soundConfigContainer = ResourcesSystem.Instance.Load<SoundConfigContainer>("ScriptableObjects/SoundConfigContainer");
            if (soundConfigContainer == null || soundConfigContainer.configs == null)
            {
                Log.Error("SoundConfigContainer或其configs为空, 语言系统初始化失败!");
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

            initProgress = 100;
            isInited = true;
            Log.Debug("SoundSystem 初始化完成");

            return Task.CompletedTask;
        }

        public void Update(float logicTime, float realTime)
        {

        }

        public void Clear()
        {
            Log.Debug("SoundSystem 清除数据");
            isInited = false;
            startupPools.Clear();
            foreach (var pool in pooledObjects.Values)
            {
                pool.Clear();
            }
            pooledObjects.Clear();
            sfxObjects.Clear();
        }

        /// <summary>
        /// 播放音乐
        /// </summary>
        /// <param name="name"></param>
        public async Task PlayMusic(int index, float volume = -1)
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
                    source.volume = volume == -1 ? GameOption.currentOption.musicVolume : volume;
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
        /// 音乐切换-带渐入渐出效果
        /// </summary>
        /// <param name="index"></param>
        public async Task PlaySwitch(int index)
        {
            // 渐出音频
            await FadeOut();
            //播放音乐
            await PlayMusic(index, 0);
            // 渐入音频
            await FadeIn();
        }

        /// <summary>
        /// 渐入
        /// </summary>
        /// <returns></returns>
        private async Task FadeIn()
        {
            // 如果 AudioSource 被销毁或不存在，则直接退出
            EnsureAudioSource();
            if (source == null) return;

            float startVolume = 0;
            float currentFadeTime = 0f;
            targetVolume = GameOption.currentOption.musicVolume;

            while (currentFadeTime < fadeDuration)
            {
                currentFadeTime += Time.deltaTime;
                float t = fadeDuration > 0 ? Mathf.Clamp01(currentFadeTime / fadeDuration) : 1;
                source.volume = Mathf.Lerp(startVolume, targetVolume, t);
                await Task.Yield();
            }
            source.volume = targetVolume; // 确保达到目标音量
        }

        /// <summary>
        /// 渐出
        /// </summary>
        /// <returns></returns>
        private async Task FadeOut()
        {
            EnsureAudioSource();
            if (source == null) return;

            float startVolume = source.volume;
            float currentFadeTime = 0f;

            while (currentFadeTime < fadeDuration)
            {
                currentFadeTime += Time.deltaTime;
                float t = fadeDuration > 0 ? Mathf.Clamp01(currentFadeTime / fadeDuration) : 1;
                source.volume = Mathf.Lerp(startVolume, 0.0f, t);
                await Task.Yield();
            }

            source.volume = 0f; // 确保音量为0
            source.Stop();
        }

        #region 播放声音
        /// <summary>
        /// 播放声音
        /// </summary>
        /// <param name="index">声音配置索引</param>
        /// <param name="emitter">声音发射器</param>
        /// <param name="loop">是否循环</param>
        public async void PlaySfx(int index, Transform emitter = null, bool loop = false, float volume = -1f, float pitch = 1f)
        {
            if (soundConfigDict != null && soundConfigDict.TryGetValue(index, out SoundConfig soundConfig))
            {
                // 获取音频剪辑
                AudioClip clip = await GetAudioClipAsync(soundConfig.Path, soundConfig.Name);
                if (clip != null)
                {
                    // 假设第一个池是所有音效的池，如果需要多种音效池，这里需要修改
                    GameObject obj = startupPools[0].prefab;
                    GameObject go = Spawn(obj);
                    if (go != null)
                    {
                        if (emitter != null)
                        {
                            go.transform.SetParent(emitter);
                            go.transform.localPosition = Vector3.zero;
                        }
                        SoundItem soundObj = go.GetComponent<SoundItem>();
                        float effectiveVolume = volume == -1f ? GameOption.currentOption.sfxVolume : volume;
                        soundObj.Processing(clip, emitter, loop, effectiveVolume, GameOption.currentOption.sfxMuted, pitch);
                    }
                }
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
        /// 创建音频根节点
        /// </summary>
        public void CreateAudioRoot()
        {
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

            startupPools.Add(pool);

            if (startupPools != null && startupPools.Count > 0)
            {
                for (int i = 0; i < startupPools.Count; i++)
                {
                    CreatePool(startupPools[i].prefab, startupPools[i].size, startupPools[i].parent);
                }
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
                    Log.Warning($"没有找到预制件 {prefab.name} 对应的对象池，直接销毁对象。");
                    UnityEngine.Object.Destroy(obj);
                }
            }
            else
            {
                // 如果对象不在生成池中，则直接销毁
                Log.Warning($"对象 {obj.name} 不在生成池中，直接销毁。");
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
            // 使用 ToList() 创建一个副本，以避免在迭代时修改集合
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
        private async Task<AudioClip> GetAudioClipAsync(string path, string name)
        {
            string fullPath = string.Concat(path, name);
            if (audioClipCache.TryGetValue(fullPath, out AudioClip clip))
            {
                return clip;
            }

            clip = await ResourcesSystem.Instance.LoadAsync<AudioClip>(fullPath);
            if (clip != null)
            {
                audioClipCache[fullPath] = clip;
            }
            else
            {
                Log.Error($"加载AudioClip失败: {fullPath}");
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