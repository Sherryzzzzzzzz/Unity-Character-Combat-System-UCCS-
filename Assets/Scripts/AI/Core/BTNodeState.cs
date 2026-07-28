/// <summary>行为树节点执行状态</summary>
public enum BTNodeState
{
    Inactive,  // 尚未进入
    Running,   // 执行中，下一帧继续 Tick
    Success,   // 成功
    Failure    // 失败
}
