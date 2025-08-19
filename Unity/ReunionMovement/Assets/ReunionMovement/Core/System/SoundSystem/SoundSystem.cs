using ReunionMovement.Common;
using ReunionMovement.Core.Base;
using ReunionMovement.Core.Resources;
using ReunionMovement.Core.Scene;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
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
        public bool IsInited { get; private set; }
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
        //临时列表
        static List<GameObject> tempList = new List<GameObject>();
        //预设对象池
        Dictionary<GameObject, Queue<GameObject>> pooledObjects = new Dictionary<GameObject, Queue<GameObject>>();
        //生成对象池 特效
        Dictionary<GameObject, GameObject> sfxObjects = new Dictionary<GameObject, GameObject>();
        #endregion
        public async Task Init()
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
            IsInited = true;
            Log.Debug("SoundSystem 初始化完成");
        }

        public void Update(float logicTime, float realTime)
        {

        }

        public void Clear()
        {
            Log.Debug("SoundSystem 清除数据");
        }

        /// <summary>
        /// 播放音乐
        /// </summary>
        /// <param name="name"></param>
        public void PlayMusic(int index, float volume = -1)
        {
            if (soundConfigDict != null && soundConfigDict.TryGetValue(index, out SoundConfig soundConfig))
            {
                currentMusicIndex = index;
                AudioClip audioClip = GetAudioClip(soundConfig.Path, soundConfig.Name);
                if (audioClip != null)
                {
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
            source?.Play();
        }

        /// <summary>
        /// 暂停BGM
        /// </summary>
        public void PauseMusic()
        {
            source?.Pause();
        }

        /// <summary>
        /// 结束背景音乐
        /// </summary>
        public void StopMusic()
        {
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
            PlayMusic(index, 0);
            // 渐入音频
            await FadeIn();
        }

        /// <summary>
        /// 渐入
        /// </summary>
        /// <returns></returns>
        private async Task FadeIn()
        {
            float startVolume = 0;
            float startTime = Time.time;
            targetVolume = GameOption.currentOption.musicVolume;

            while (source.volume < targetVolume)
            {
                float elapsedTime = Time.time - startTime;
                float t = fadeDuration > 0 ? Mathf.Clamp01(elapsedTime / fadeDuration) : 1;

                source.volume = Mathf.Lerp(startVolume, targetVolume, t);

                await Task.Yield();
            }
        }

        /// <summary>
        /// 渐出
        /// </summary>
        /// <returns></returns>
        private async Task FadeOut()
        {
            float startVolume = source.volume;
            float startTime = Time.time;

            while (source.volume > 0.0f)
            {
                float elapsedTime = Time.time - startTime;
                float t = fadeDuration > 0 ? Mathf.Clamp01(elapsedTime / fadeDuration) : 1;

                source.volume = Mathf.Lerp(startVolume, 0.0f, t);

                await Task.Yield();
            }

            source.Stop();
        }

        #region 播放声音
        /// <summary>
        /// 播放声音
        /// </summary>
        /// <param name="index"></param>
        public void PlaySfx(int index)
        {
            PlaySfx(index, null, false);
        }

        /// <summary>
        /// 播放声音
        /// </summary>
        /// <param name="index"></param>
        /// <param name="loop"></param>
        public void PlaySfx(int index, bool loop)
        {
            PlaySfx(index, null, loop);
        }

        /// <summary>
        /// 播放声音
        /// </summary>
        /// <param name="index"></param>
        /// <param name="loop"></param>
        /// <param name="emitter"></param>
        public void PlaySfx(int index, bool loop, Transform emitter)
        {
            PlaySfx(index, emitter, loop);
        }

        /// <summary>
        /// 播放声音
        /// </summary>
        /// <param name="index"></param>
        /// <param name="emitter"></param>
        /// <param name="loop"></param>
        public void PlaySfx(int index, Transform emitter, bool loop)
        {
            ProcessingPlaySfx(index, emitter, loop);
        }

        /// <summary>
        /// 播放声音
        /// </summary>
        /// <param name="index"></param>
        /// <param name="emitter"></param>
        /// <param name="loop"></param>
        void ProcessingPlaySfx(int index, Transform emitter, bool loop)
        {
            if (soundConfigDict != null && soundConfigDict.TryGetValue(index, out SoundConfig soundConfig))
            {
                AudioClip clip = GetAudioClip(soundConfig.Path, soundConfig.Name);
                if (clip != null)
                {
                    // 假设第一个池是所有音效的池，如果需要多种音效池，这里需要修改
                    GameObject obj = startupPools[0].prefab;
                    GameObject go = Spawn(obj);
                    if (go != null)
                    {
                        SoundItem soundObj = go.GetComponent<SoundItem>();
                        soundObj.Processing(clip, emitter, loop, GameOption.currentOption.musicVolume, GameOption.currentOption.musicMuted);
                    }
                }
            }
        }

        /// <summary>
        /// 播放声音
        /// </summary>
        public void StopSound()
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
                Queue<GameObject> queue = new Queue<GameObject>(size);
                pooledObjects.Add(prefab, queue);
                // 创建对象池
                Transform parentVoice = parent;
                for (int i = 0; i < size; i++)
                {
                    var objVoice = GameObject.Instantiate(prefab, parentVoice);
                    objVoice.SetActive(false);
                    queue.Enqueue(objVoice);
                }
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

            if (!pooledObjects.TryGetValue(prefab, out var queue))
            {
                queue = new Queue<GameObject>();
                pooledObjects.Add(prefab, queue);
            }

            GameObject obj;
            if (queue.Count > 0)
            {
                obj = queue.Dequeue();
                obj.transform.SetParent(parent);
                obj.transform.localPosition = position;
                obj.transform.localRotation = rotation;
                obj.SetActive(true);
            }
            else
            {
                obj = GameObject.Instantiate(prefab, position, rotation, parent);
            }

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
                Recycle(obj, prefab);
            }
            else
            {
                // 如果对象不在生成池中，则直接销毁
                UnityEngine.Object.Destroy(obj);
            }
        }

        /// <summary>
        /// 回收利用
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="prefab"></param>
        void Recycle(GameObject obj, GameObject prefab)
        {
            // 确保我们有这个预制体的池
            if (pooledObjects.TryGetValue(prefab, out var queue))
            {
                queue.Enqueue(obj);
                sfxObjects.Remove(obj);
                obj.transform.SetParent(sfxRoot.transform);
                obj.SetActive(false);
            }
            else
            {
                // 如果没有对应的池，直接销毁
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
            try
            {
                foreach (var item in sfxObjects)
                {
                    if (item.Value == prefab)
                    {
                        tempList.Add(item.Key);
                    }
                }
                for (int i = 0; i < tempList.Count; ++i)
                {
                    Recycle(tempList[i]);
                }
            }
            finally
            {
                tempList.Clear();
            }
        }

        /// <summary>
        /// 回收所有实例
        /// </summary>
        public void RecycleAll()
        {
            try
            {
                tempList.AddRange(sfxObjects.Keys);
                for (int i = 0; i < tempList.Count; ++i)
                {
                    Recycle(tempList[i]);
                }
            }
            finally
            {
                tempList.Clear();
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
            if (pooledObjects.TryGetValue(prefab, out var queue))
            {
                return queue.Count;
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
            foreach (var queue in pooledObjects.Values)
            {
                count += queue.Count;
            }
            return count;
        }

        /// <summary>
        /// 从预设对象池中获得同类型对象
        /// </summary>
        /// <param name="prefab"></param>
        /// <param name="list"></param>
        /// <param name="appendList"></param>
        /// <returns></returns>
        public List<GameObject> GetPooled(GameObject prefab, List<GameObject> list, bool appendList)
        {
            if (list == null)
            {
                list = new List<GameObject>();
            }
            if (!appendList)
            {
                list.Clear();
            }
            if (pooledObjects.TryGetValue(prefab, out var pooled))
            {
                list.AddRange(pooled);
            }
            return list;
        }

        /// <summary>
        /// 从预设对象池中获得同类型对象
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="prefab"></param>
        /// <param name="list"></param>
        /// <param name="appendList"></param>
        /// <returns></returns>
        public List<T> GetPooled<T>(T prefab, List<T> list, bool appendList) where T : Component
        {
            if (list == null)
            {
                list = new List<T>();
            }
            if (!appendList)
            {
                list.Clear();
            }
            if (pooledObjects.TryGetValue(prefab.gameObject, out var pooled))
            {
                foreach (var item in pooled)
                {
                    list.Add(item.GetComponent<T>());
                }
            }
            return list;
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
            if (pooledObjects.TryGetValue(prefab, out var pooled))
            {
                foreach (var item in pooled)
                {
                    GameObject.Destroy(item);
                }
                pooled.Clear();
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
        /// 从缓存或资源加载音频剪辑
        /// </summary>
        private AudioClip GetAudioClip(string path, string name)
        {
            string fullPath = path + name;
            if (audioClipCache.TryGetValue(fullPath, out AudioClip clip))
            {
                return clip;
            }

            clip = ResourcesSystem.Instance.Load<AudioClip>(fullPath);
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