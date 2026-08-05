namespace UCCS
{
    /// <summary>
    /// 可提供堆叠层数的来源 — 用于属性修改器的 StackCount 感知。
    /// ActiveGameplayEffect 实现此接口，测试中可用轻量假实现替代。
    /// </summary>
    public interface IStackCountSource
    {
        int CurrentStacks { get; }
    }
}
