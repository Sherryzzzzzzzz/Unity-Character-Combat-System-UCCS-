using UnityEngine;
using System.Collections.Generic;
using UnityEngine.InputSystem;

// --- 需要先定义条件和动作的类型 ---

public enum ConditionType
{
    IsGrounded,
    IsFalling,
    InputWasPressed,      // 输入在当前帧被按下
    InputIsPressed,       // 输入正被按住
    InputWasReleased,     // 输入在当前帧被松开
    HasInputTag,
    // 你可以继续添加更多...
}
public enum BranchActionType
{
    JumpToFrame,      // 跳转到指定帧
    JumpToEvent,      // 跳转到另一个事件的起始帧 (更高级)
    EndSkill,         // 直接结束技能
}

[System.Serializable]
public class BranchCondition
{
    public ConditionType type;
    
    // 我们不再需要 expectedValue，因为 IsPressed/WasReleased 已经隐含了期望值
    // public bool expectedValue = true; 
    
    [Tooltip("当条件类型为 HasInputTag 时，指定要消耗的 Tag")]
    public GameplayTagSO requiredTag;
    
    [Tooltip("当条件类型与输入相关时，指定要检查的输入动作")]
    public InputActionReference inputAction; // *** 新增字段 ***
}

[System.Serializable]
public class BranchEvent : TimelineEventBase, ITimelineEventRuntime
{
    [Tooltip("需要满足的所有条件（AND关系）")]
    public List<BranchCondition> conditions = new List<BranchCondition>();
    
    [Tooltip("满足条件后执行的动作")]
    public BranchActionType action = BranchActionType.JumpToFrame;
    
    [Tooltip("要跳转到的目标帧")]
    public int targetFrame;

    public override TimelineEventType Type => TimelineEventType.Branch; // 假设你有这个类型
    public override string GetSummary()
    {
        if (conditions.Count > 0)
        {
            var firstCondition = conditions[0];
            string conditionStr = firstCondition.type.ToString();
            if (firstCondition.inputAction != null)
                conditionStr += $" ({firstCondition.inputAction.action?.name})";
            else if (firstCondition.requiredTag != null)
                conditionStr += $" ({firstCondition.requiredTag.name})";
            
            return $"Branch If ({conditionStr}) -> {action} to {targetFrame}";
        }
        return "Branch Event (No conditions)";
    }

    // OnStart 和 OnEnd 都是空的，因为逻辑由 PlayerAttackComponent 在 Update 中处理
    public void OnStart(GameObject owner) { }
    public void OnEnd(GameObject owner) { }

    public bool ConditionsMet(GameObject owner)
    {
        var controller = owner.GetComponent<PlayerController>();
        var tagComponent = owner.GetComponent<TagComponent>();

        // 如果任何一个必要的组件不存在，则条件永远不满足
        if (controller == null) return false;

        foreach (var condition in conditions)
        {
            bool conditionResult = false;
            switch (condition.type)
            {
                case ConditionType.IsGrounded:
                    conditionResult = controller.isGround;
                    break;
                case ConditionType.IsFalling:
                    conditionResult = owner.GetComponent<PlayerModel>().gravityVector.y < -0.1f;
                    break;
                
                // --- *** 核心修改：处理所有输入条件 *** ---
                case ConditionType.InputWasPressed:
                    // 检查指定的输入动作是否在本帧被按下
                    if (condition.inputAction != null && condition.inputAction.action != null)
                    {
                        conditionResult = condition.inputAction.action.WasPressedThisFrame();
                    }
                    break;
                case ConditionType.InputIsPressed:
                    // 检查指定的输入动作是否正被按住
                    if (condition.inputAction != null && condition.inputAction.action != null)
                    {
                        conditionResult = condition.inputAction.action.IsPressed();
                    }
                    break;
                case ConditionType.InputWasReleased:
                    // 检查指定的输入动作是否在本帧被松开
                    if (condition.inputAction != null && condition.inputAction.action != null)
                    {
                        conditionResult = condition.inputAction.action.WasReleasedThisFrame();
                    }
                    break;
                
                case ConditionType.HasInputTag:
                    if (tagComponent != null && condition.requiredTag != null)
                    {
                        conditionResult = tagComponent.ConsumeTag(condition.requiredTag);
                    }
                    break;
            }
            
            // 如果任何一个条件不满足，则整个 BranchEvent 失败
            // (我们假设所有条件的期望值都是 true)
            if (!conditionResult)
            {
                return false;
            }
        }
        
        // 所有条件都满足
        return true;
    }

    public override TimelineEventBase Clone()
    {
        var newEvent = new BranchEvent();
        newEvent.startFrame = this.startFrame;
        newEvent.endFrame = this.endFrame;
        newEvent.conditions = new List<BranchCondition>(this.conditions);
        newEvent.action = this.action;
        newEvent.targetFrame = this.targetFrame;
        return newEvent;
    }
}