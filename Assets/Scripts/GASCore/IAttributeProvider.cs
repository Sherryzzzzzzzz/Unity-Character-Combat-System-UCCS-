namespace UCCS
{
    /// <summary>
    /// 属性提供者接口 — 解耦条件节点和具体属性实现
    /// </summary>
    public interface IAttributeProvider
    {
        float Health { get; }
        float HealthMax { get; }
    }
}
