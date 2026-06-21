using ReunionMovement.Common;
using ReunionMovement.Common.Util;
using ReunionMovement.Common.Util.Download;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class DownloadExample : MonoBehaviour
{
    string url = "https://act-upload.mihoyo.com/sr-wiki/2025/05/04/276833758/26c5acea65c8b5c594ec5556e37881c6_4562746630950494855.png";

    List<string> urlList = new List<string>
    {
        "https://act-upload.mihoyo.com/sr-wiki/2025/05/04/276833758/26c5acea65c8b5c594ec5556e37881c6_4562746630950494855.png",
        "https://act-upload.mihoyo.com/sr-wiki/2025/05/26/331632599/35febe0dcde248275e78541a22a1d691_3880185522085795821.png",
        "https://act-upload.mihoyo.com/sr-wiki/2025/05/26/331632599/65d197d46bb434c561ba157123d8565e_1275620304822777564.png",
        "https://act-upload.mihoyo.com/sr-wiki/2025/05/25/197948068/3977d62ea4d0fe0255c5150e1033179a_5862782453906698066.png",
        "https://act-upload.mihoyo.com/sr-wiki/2025/03/11/331632599/5310ccf5bf3a921f2c6e98634708af26_6843303027698005130.png",
        "https://act-upload.mihoyo.com/sr-wiki/2024/04/17/279865110/4e90a7fd1a49f3feee30cac28223a75e_8933359754956760001.png",
        "https://act-upload.mihoyo.com/sr-wiki/2025/05/25/197948068/5de480c69cc78d114a7dbde6705e5e98_8454883161627555117.png",
        "https://act-upload.mihoyo.com/sr-wiki/2025/07/05/197948068/62924fc83e472e9e9b9c1c69c52f335a_8635606035917333368.png"
    };

    [Header("RawImage 方案（零 GC，无需 Sprite）")]
    public RawImage rawImage;

    [Header("Image 方案（需 Sprite）")]
    public Image image;

    // Sprite 缓存：同一张图只 Create 一次，后续零 GC
    private SpriteCache spriteCache = new SpriteCache();

    void Start()
    {
        // ===== RawImage 方案：Texture2D 直接赋给 RawImage，零 GC =====
        DownloadMgr.Instance.DownloadImage_Http(url, OnProgress, OnTexture2D_RawImage);

        // ===== Image + SpriteCache 方案：通过缓存获取 Sprite，只 Create 一次 =====
        DownloadMgr.Instance.DownloadImage_Http(url, OnProgress, OnTexture2D_SpriteCache);

        string loaclPath = PathUtil.GetLocalPath(DownloadType.PersistentImage);
        DownloadMgr.Instance.DownloadFiles(urlList, loaclPath, OnProgress);
    }

    void OnDestroy()
    {
        // 销毁时清空 Sprite 缓存
        spriteCache.Clear();
    }

    // ==================== RawImage 方案 ====================
    // 优点：不需要 Sprite，完全零 GC
    // 缺点：无法使用 SpriteAtlas、Image 的 FillMethod 等功能

    public void OnTexture2D_RawImage(Texture2D arg)
    {
        rawImage.texture = arg;
    }

    // ==================== Image + Sprite 方案 ====================
    // 方案 A：直接转换（每次下载都会调用一次 Sprite.Create，有一次 GC）

    public void OnTexture2D_DirectSprite(Texture2D arg)
    {
        // Rect 和 Vector2 是 struct，无 GC
        // 仅 Sprite.Create() 产生一次分配
        Sprite sprite = arg.ToSprite();  // 默认居中锚点
        image.sprite = sprite;
    }

    // 方案 B：使用 SpriteCache（同一 URL 只 Create 一次，后续零 GC）

    public void OnTexture2D_SpriteCache(Texture2D arg)
    {
        // 以 URL 为 key，首次调用会 Create，后续命中缓存 → 零 GC
        Sprite sprite = spriteCache.GetOrCreate(url, arg);
        if (sprite != null)
        {
            image.sprite = sprite;
        }
    }

    // 方案 C：扩展方法版（与 B 等价，语法糖）

    public void OnTexture2D_CachedExtension(Texture2D arg)
    {
        // GetCachedSprite 内部调用 spriteCache.GetOrCreate()
        Sprite sprite = arg.GetCachedSprite(spriteCache, url);
        if (sprite != null)
        {
            image.sprite = sprite;
        }
    }

    // ==================== 带自定义锚点和裁切的用法 ====================

    public void OnTexture2D_CustomPivot(Texture2D arg)
    {
        // 使用预定义的静态 pivot，避免每次 new Vector2
        Sprite sprite = arg.ToSprite(TextureToSpriteUtil.PivotTopLeft);
        image.sprite = sprite;
    }

    // ==================== 回调 ====================

    public void OnProgress(float arg)
    {
        Log.Debug($"下载进度: {arg * 100}%");
    }

    public void OnSuccess()
    {
        Log.Debug($"完成全部下载");
    }

    public void OnError(string arg)
    {
        Log.Error($"错误: {arg}");
    }

    // 保留兼容旧回调
    public void OnTexture2D(Texture2D arg)
    {
        rawImage.texture = arg;
    }
}
