using UnityEngine;

/// <summary>
/// 游戏加载模式
/// </summary>
public enum LoaderMode
{
    Async,  // 异步加载
    Sync,   // 同步加载
}

/// <summary>
/// 面板类型
/// </summary>
public enum PanelType
{
    // 主界面类型，比如：摇杆，任务，聊天，活动/穿戴提示，主界面入口图标等界面
    MainUI,
    // 最常用的类型，绝大多数界面都属于此层级
    NormalUI,
    // 仅仅给头顶文字使用
    HeadInfoUI,
    // Tips层级，在最顶层显示，比如：系统飘字提示，系统公告/广播，停服维护等
    TipsUI,
}

/// <summary>
/// 面板大小
/// </summary>
public enum PanelSize
{
    // 非全屏界面，不会挡住主界面，比如穿戴提示，活动推送，领取红包
    SmallPanel,
    // 遮挡了主界面的80%，上下和两边的空隙可利用截屏一张图后，就可以隐藏Main Camera
    SinglePanel,
    // 全屏界面，可以禁用Main Camera，减少开销
    FullScreen,
}

/// <summary>
/// Unity层
/// </summary>
public enum UnityLayerDef
{
    Default = 0,
    TransparentFX = 1,
    IgnoreRaycast = 2,

    Water = 4,
    UI = 5,

    // 以下为自定义
    //Hidden = 8,
}

/// <summary>
/// 下载类型
/// </summary>
public enum DownloadType
{
    // 持久路径
    PersistentAssets,
    PersistentFile,
    PersistentImage,
    PersistentJson,
    // 流媒体路径
    StreamingAssets,
    StreamingAssetsFile,
    StreamingAssetsImage,
    StreamingAssetsJson,
    // 缓存路径
    CacheAssets,
    CacheFile,
    CacheImage,
    CacheJson,
}

/// <summary>
/// 消息类型
/// </summary>
public enum MessageTipType
{
    // 提示
    Tip,
    WaringTip,
    ErrorTip,
    // 弹框
    PopFrame,
    WaringPopFrame,
    ErrorPopFrame,
    // 通知
    Notice,
}

/// <summary>
/// 旋转方向
/// </summary>
public enum RotationDirection
{
    None,
    Right,  // 向右旋转
    Left    // 向左旋转
}

/// <summary>
/// 锚点类型
/// </summary>
public enum AnchorType
{
    TopRight,       //右上角
    TopLeft,        //左上角
    Stretch,        //四周对齐
    StretchTop,     //顶部对齐
    StretchBottom,  //底部对齐
    StretchLeft,    //左边对齐
    StretchRight,   //右边对齐
}

/// <summary>
/// 旋转顺序
/// </summary>
public enum XYZOrder
{
    XYZ,
    XZY,

    YXZ,
    YZX,

    ZXY,
    ZYX
}

/// <summary>
/// 旋转算法类型
/// </summary>
public enum XYZAlgorithmType
{
    Quaternion,
    Matrix4x4,
}