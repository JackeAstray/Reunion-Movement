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
    public partial class SoundSystem
    {
        /// <summary>
        /// 从缓存或资源加载音频剪辑  WAV(.wav)  AIFF/AIF(.aif, .aiff)  AU(.au)  Ogg Vorbis(.ogg)  MP3(.mp3)
        /// </summary>
        private async UniTask<AudioClip> GetAudioClipAsync(string path, string name)
        {
            string fullPath = string.Concat(path, name);

            // 命中缓存：Touch 访问条目（移到链表尾部 = 最近使用），实现真正的 LRU
            if (audioClipCache.TryGetValue(fullPath, out AudioClip clip))
            {
                if (audioClipCacheNodes.TryGetValue(fullPath, out var hitNode))
                {
                    audioClipCacheOrder.Remove(hitNode);
                    audioClipCacheOrder.AddLast(hitNode);
                }
                return clip;
            }

            // 并发去重：同一路径的加载在途时复用同一个 TCS，多个调用方 await 各自的 Task
            // （SingleFlightLoader 统一管理单飞/摘除；系统 Clear 时 CancelAll 取消等待方）
            return await audioClipLoadGate.RunAsync(fullPath, async () =>
            {
                var tcs = new UniTaskCompletionSource<AudioClip>();
                _ = LoadAudioClipAndCacheAsync(fullPath, tcs);
                return await tcs.Task;
            });
        }

        /// <summary>
        /// 加载 AudioClip 并写入 LRU 缓存，完成后设置 TCS（供并发去重复用）。
        /// </summary>
        private async UniTaskVoid LoadAudioClipAndCacheAsync(string fullPath, UniTaskCompletionSource<AudioClip> tcs)
        {
            int generationAtStart = cacheGeneration;
            try
            {
                // 双轨：Addressables 优先（Remote/Sounds/...），失败自动降级 Resources
                AudioClip clip = null;
                bool fromAddressables = false;
                if (Config.AddressablesMode != AddressablesMode.Off)
                {
                    clip = await AddressableSystem.GetOrInstance().LoadAssetAsync<AudioClip>(ToSoundAddressableKey(fullPath));
                    fromAddressables = clip != null;
                }
                if (clip == null)
                {
                    clip = await Modules.Get<ResourcesSystem>().LoadAsync<AudioClip>(fullPath);
                }

                // Clear() 期间发生过：缓存已被清空并重建，不再写入（否则条目复活但 LRU 节点
                // 缺失 → 永不驱逐、句柄永久泄漏）。直接释放新加载的 clip 并丢弃。
                if (generationAtStart != cacheGeneration)
                {
                    if (clip != null)
                    {
                        if (fromAddressables)
                        {
                            AddressableSystem.GetOrInstance().ReleaseAsset(clip);
                        }
                        else
                        {
                            Modules.Get<ResourcesSystem>().DeleteAssetCache(fullPath);
                        }
                    }
                    tcs.TrySetResult(null);
                    return;
                }

                if (clip != null)
                {
                    // 记录来源，便于按来源正确释放
                    if (fromAddressables) addressableClips.Add(fullPath);
                    else addressableClips.Remove(fullPath);

                    // LRU 驱逐：缓存满时移除最久未使用的条目（链表头部），
                    // 跳过正在播放的 clip（BGM/活跃 SFX），避免卸载后播放中断
                    if (audioClipCache.Count >= MaxAudioClipCacheSize)
                    {
                        LinkedListNode<string> oldestNode = audioClipCacheOrder.First;
                        int scanned = 0;
                        while (oldestNode != null && scanned < audioClipCacheOrder.Count)
                        {
                            audioClipCache.TryGetValue(oldestNode.Value, out AudioClip oldestClip);
                            if (!IsClipInUse(oldestClip)) break;
                            oldestNode = oldestNode.Next;
                            scanned++;
                        }
                        // 全部在用则驱逐最旧条目保底（避免缓存无限增长，极端情况可接受）
                        if (oldestNode == null) oldestNode = audioClipCacheOrder.First;
                        if (oldestNode != null)
                        {
                            string evicted = oldestNode.Value;
                            AudioClip evictedClip = audioClipCache[evicted];
                            audioClipCache.Remove(evicted);
                            audioClipCacheNodes.Remove(evicted);
                            audioClipCacheOrder.Remove(oldestNode);
                            ReleaseClip(evicted, evictedClip);
                        }
                    }
                    audioClipCache[fullPath] = clip;
                    var node = audioClipCacheOrder.AddLast(fullPath);
                    audioClipCacheNodes[fullPath] = node;
                }
                else
                {
                    Log.Error("加载AudioClip失败: {0}", fullPath);
                }
                tcs.TrySetResult(clip);
            }
            catch (Exception ex)
            {
                Log.Error("加载AudioClip异常: {0} {1}", fullPath, ex.Message);
                tcs.TrySetResult(null);
            }
        }

        /// <summary>
        /// 把 SoundSystem 的 Resources 路径（如 "Sounds/BGM/xxx"，SoundConfig.Path 恒为 Sounds/ 开头且 Name 无扩展名）
        /// 转换为 Addressables 地址（"Remote/Sounds/BGM/xxx"），与迁移器生成的地址对齐。
        /// </summary>
        private static string ToSoundAddressableKey(string fullPath)
        {
            return AddressableKeys.RemoteRoot + fullPath.TrimStart('/');
        }

        /// <summary>按来源释放单个 clip：Addressables 加载的用 ReleaseAsset，Resources 加载的由 ResourcesSystem 卸载。</summary>
        private void ReleaseClip(string fullPath, AudioClip clip)
        {
            if (clip == null) return;
            if (addressableClips.Remove(fullPath))
            {
                AddressableSystem.GetOrInstance().ReleaseAsset(clip);
                return;
            }
            // 联动 ResourcesSystem 递减引用计数并卸载底层 AudioClip，真正释放内存
            Modules.Get<ResourcesSystem>().DeleteAssetCache(fullPath);
        }

        /// <summary>
        /// 判断 clip 是否正在被播放（BGM 源或任意活跃 SFX 对象引用）。
        /// 驱逐前调用：正在播放的 clip 被卸载会导致声音中断。
        /// </summary>
        private bool IsClipInUse(AudioClip clip)
        {
            if (clip == null) return false;
            if (source != null && source.clip == clip) return true;
            // 直接查平行缓存的 AudioSource（Spawn 时登记），O(1) 无 GetComponent；
            // 已销毁对象在字典中为 fake-null，src == null 自动跳过（低频清扫清理键）
            foreach (var src in sfxSources.Values)
            {
                if (src != null && src.clip == clip) return true;
            }
            return false;
        }
    }
}
