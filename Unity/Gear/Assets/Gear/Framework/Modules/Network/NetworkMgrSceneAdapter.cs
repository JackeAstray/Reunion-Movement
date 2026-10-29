using UnityEngine;

namespace Gear.Framework.Modules
{
    /// <summary>
    /// 旧示例场景兼容壳：NetworkMgr 已改为纯 C# 单例，场景里原来的组件改为指向本类型，
    /// 避免出现 Missing Script。
    /// <para>
    /// 本组件不参与网络逻辑，只用于两个旧示例场景的平滑迁移；确认场景不再需要后可直接删除。
    /// </para>
    /// </summary>
    public sealed class NetworkMgrSceneAdapter : MonoBehaviour
    {
    }
}
