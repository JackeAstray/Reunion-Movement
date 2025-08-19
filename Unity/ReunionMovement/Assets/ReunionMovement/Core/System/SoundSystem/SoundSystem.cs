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

        // 启动时预设对象池
        public List<StartupPool> startupPools = new List<StartupPool>();
        //临时列表
        static List<GameObject> tempList = new List<GameObject>();
        //预设对象池
        Dictionary<GameObject, List<GameObject>> pooledObjects = new Dictionary<GameObject, List<GameObject>>();
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
            if (soundConfigContainer != null && soundConfigContainer.configs != null)
            {
                SoundConfig soundConfig = soundConfigContainer.configs.Find(l => l.Number == index);

                if (soundConfig != null)
                {
                    currentMusicIndex = index;
                    AudioClip audioClip = ResourcesSystem.Instance.Load<AudioClip>(soundConfig.Path + soundConfig.Name);
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
        public async void PlaySwitch(int index)
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
                float t = Mathf.Clamp01(elapsedTime / fadeDuration);

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
                float t = Mathf.Clamp01(elapsedTime / fadeDuration);

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
            if (soundConfigContainer != null && soundConfigContainer.configs != null)
            {
                SoundConfig soundConfig = soundConfigContainer.configs.Find(l => l.Number == index);

                if (soundConfig != null)
                {
                    AudioClip clip = ResourcesSystem.Instance.Load<AudioClip>(soundConfig.Path + soundConfig.Name);
                    if (clip != null)
                    {
                        GameObject obj = startupPools[0].prefab;
                        GameObject go = Spawn(obj);
                        if (go != null)
                        {
                            SoundItem soundObj = go.GetComponent<SoundItem>();
                            soundObj.clip = clip;
                            soundObj.Processing(index, emitter, loop, GameOption.currentOption.musicVolume, GameOption.currentOption.musicMuted);
                        }
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
                List<GameObject> list = new List<GameObject>();
                pooledObjects.Add(prefab, list);
                // 创建对象池
                Transform parentVoice = parent;
                for (int i = 0; i < size; i++)
                {
                    var objVoice = GameObject.Instantiate(prefab, parentVoice);
                    objVoice.SetActive(false);
                    list.Add(objVoice);
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

            if (!pooledObjects.TryGetValue(prefab, out var list))
            {
                list = new List<GameObject>();
                pooledObjects.Add(prefab, list);
            }

            GameObject obj = list.Find(o => !o.activeSelf);
            if (obj != null)
            {
                obj.transform.SetParent(parent);
                obj.transform.localPosition = position;
                obj.transform.localRotation = rotation;
                obj.SetActive(true);
            }
            else
            {
                obj = GameObject.Instantiate(prefab, position, rotation, parent);
                list.Add(obj);
            }

            var spawnedObjects = sfxObjects;
            spawnedObjects[obj] = prefab;

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
            GameObject prefab;
            if (sfxObjects.TryGetValue(obj, out prefab))
            {
                Recycle(obj, prefab);
            }
            else
            {
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
            pooledObjects[prefab].Add(obj);

            sfxObjects.Remove(obj);
            obj.transform.parent = sfxRoot.transform;

            obj.SetActive(false);
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
            tempList.Clear();
        }

        /// <summary>
        /// 回收所有实例
        /// </summary>
        public void RecycleAll()
        {
            tempList.AddRange(sfxObjects.Keys);
            for (int i = 0; i < tempList.Count; ++i)
            {
                Recycle(tempList[i]);
            }
            tempList.Clear();
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
            List<GameObject> list;
            if (pooledObjects.TryGetValue(prefab, out list))
            {
                return list.Count;
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
            foreach (var list in pooledObjects.Values)
            {
                count += list.Count;
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
            List<GameObject> pooled;
            if (pooledObjects.TryGetValue(prefab, out pooled))
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
            List<GameObject> pooled;
            if (pooledObjects.TryGetValue(prefab.gameObject, out pooled))
            {
                for (int i = 0; i < pooled.Count; ++i)
                {
                    list.Add(pooled[i].GetComponent<T>());
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
            List<GameObject> pooled;
            if (pooledObjects.TryGetValue(prefab, out pooled))
            {
                for (int i = 0; i < pooled.Count; ++i)
                {
                    GameObject.Destroy(pooled[i]);
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