// 文件名: BuffEvent.cs
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// 时间轴事件，用于在特定帧为目标施加或移除一个 Buff。
/// </summary>
[System.Serializable]
public class BuffEvent : TimelineEventBase, ITimelineEventRuntime
{
    public enum TargetType { Self, NearestEnemy } // 定义 Buff 的目标
    public enum ActionType { Apply, Remove }     // 定义要执行的操作

    [Header("Buff 配置")]
    [Tooltip("要施加或移除的 Buff 模板")]
    public BuffSO buffData; // 引用我们之前创建的 BuffSO

    [Header("目标与操作")]
    [Tooltip("将 Buff 施加给谁")]
    public TargetType target = TargetType.Self;
    [Tooltip("是施加 Buff 还是移除 Buff")]
    public ActionType action = ActionType.Apply;
    
    // --- 运行时数据 ---
    private GameObject _owner; // 技能的持有者
    private PlayerModel _playerModel; // 用于查找最近的敌人

    public override TimelineEventType Type => TimelineEventType.Buff; // 假设你有这个类型
    public override string GetSummary()
    {
        return $"Buff [{StartFrame}-{EndFrame}] {action} {buffData?.buffName ?? "None"} on {target}";
    }

    public void OnStart(GameObject owner)
    {
        // 缓存技能持有者信息
        _owner = owner;
        _playerModel = owner.GetComponent<PlayerModel>();

        // 根据事件的持续时间，决定是在开始时执行还是结束时执行
        // 如果 StartFrame 和 EndFrame 相同，这是一个瞬时事件
        if (StartFrame == EndFrame)
        {
            Execute();
        }
        else
        {
            // 如果有持续时间，则在 OnStart 时执行 Apply
            if (action == ActionType.Apply)
            {
                Execute();
            }
        }
    }

    public void OnEnd(GameObject owner)
    {
        // 如果有持续时间，则在 OnEnd 时执行 Remove
        if (StartFrame != EndFrame)
        {
            if (action == ActionType.Apply) // 对于 Apply 类型的事件，在结束时自动移除
            {
                // 创建一个临时的 Remove 逻辑
                Execute(ActionType.Remove);
            }
            else if (action == ActionType.Remove) // 对于 Remove 类型的事件，在结束时执行
            {
                Execute();
            }
        }
    }
    
    /// <summary>
    /// 执行 Buff 操作的核心逻辑。
    /// </summary>
    private void Execute(ActionType? overrideAction = null)
    {
        if (buffData == null || _owner == null) return;
        
        // --- 1. 确定目标 ---
        GameObject targetObject = null;
        switch (target)
        {
            case TargetType.Self:
                targetObject = _owner;
                break;
            case TargetType.NearestEnemy:
                // 假设 PlayerModel 有查找最近敌人的功能
                if (_playerModel != null && _playerModel.nearestEnemy != null)
                {
                    targetObject = _playerModel.nearestEnemy.gameObject;
                }
                break;
        }

        if (targetObject == null) return;
        
        // --- 2. 获取目标的 TagComponent ---
        var targetTagComponent = targetObject.GetComponent<TagComponent>();
        if (targetTagComponent == null)
        {
            Debug.LogWarning($"BuffEvent: 目标 '{targetObject.name}' 缺少 TagComponent。", targetObject);
            return;
        }

        // --- 3. 执行操作 ---
        ActionType finalAction = overrideAction ?? this.action;

        switch (finalAction)
        {
            case ActionType.Apply:
                // 调用 TagComponent 的 ApplyBuff 方法
                targetTagComponent.ApplyBuff(buffData, _owner);
                Debug.Log($"Applied buff '{buffData.name}' to '{targetObject.name}'");
                break;
            case ActionType.Remove:
                // 调用 TagComponent 的 RemoveBuff 方法
                targetTagComponent.RemoveBuff(buffData);
                Debug.Log($"Removed buff '{buffData.name}' from '{targetObject.name}'");
                break;
        }
    }
    
    public override TimelineEventBase Clone()
    {
        var newEvent = new BuffEvent();
        newEvent.StartFrame = StartFrame;
        newEvent.EndFrame = EndFrame;
        newEvent.buffData = buffData;
        newEvent.target = target;
        newEvent.action = action;
        return newEvent;
    }
}