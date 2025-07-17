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

    public RawImage rawImage;

    void Start()
    {
        DownloadMgr.Instance.DownloadImage_Http(url, OnProgress, OnTexture2D);

        string loaclPath = PathUtil.GetLocalPath(DownloadType.PersistentImage);

        DownloadMgr.Instance.DownloadFiles(urlList, loaclPath, OnProgress);
    }

    // Update is called once per frame
    void Update()
    {

    }

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

    public void OnTexture2D(Texture2D arg)
    {
        rawImage.texture = arg;
    }
}
