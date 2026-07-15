namespace ReunionMovement.Core.Base
{
    /// <summary>
    /// 标记接口：实现此接口的模块会在每帧收到 Update 调用。
    /// 不实现此接口的模块不会被遍历，避免空调用开销。
    /// </summary>
    public interface IRequiresUpdate
    {
    }
}
