using UnityEngine;
using System.Collections.Generic;

public enum ConditionType
{
    IsGrounded,
    IsFalling,
    InputWasPressed,
    InputIsPressed,
    InputWasReleased,
    HasInputTag,
    // 你可以继续添加更多...
}

public enum BranchActionType
{
    None,             // 仅用于打破循环
    JumpToFrame,
    EndSkill,
    PlayNextSkill     // (可选) 为连招等准备
}

[System.Serializable]
public class BranchCondition
{
    public ConditionType type;
    
    [Tooltip("当条件类型为 HasInputTag 时，指定要消耗的 Tag")]
    public GameplayTagSO requiredTag;
    
    [Tooltip("当条件类型与输入相关时，指定要检查的输入动作")]
    public UnityEngine.InputSystem.InputActionReference inputAction;

    // Clone 方法，用于深拷贝
    public BranchCondition Clone()
    {
        return new BranchCondition
        {
            type = this.type,
            requiredTag = this.requiredTag,
            inputAction = this.inputAction
        };
    }
}


[System.Serializable]
public class LoopEvent : TimelineEventBase, ITimelineEventRuntime
{
    [Tooltip("跳出循环需要满足的所有条件（AND关系）")]
    public List<BranchCondition> breakConditions = new List<BranchCondition>();
    
    public override TimelineEventType Type => TimelineEventType.Loop;
    public override string GetSummary() => $"Loop [{StartFrame}-{EndFrame}]";
    
    // OnStart 和 OnEnd 都是空的，因为所有逻辑都在 PlayerSkillComponent 中处理
    public void OnStart(GameObject owner) { }
    public void OnEnd(GameObject owner) { }
    
    /// <summary>
    /// 检查是否满足跳出循环的条件。
    /// </summary>
    public bool BreakConditionsMet(GameObject owner)
    {
        if (breakConditions == null || breakConditions.Count == 0) return false;
        
        var controller = owner.GetComponent<PlayerController>();
        var tagComponent = owner.GetComponent<TagComponent>();
        if (controller == null) return false;

        foreach (var condition in breakConditions)
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
                case ConditionType.InputWasPressed:
                    if (condition.inputAction != null && condition.inputAction.action != null)
                        conditionResult = condition.inputAction.action.WasPressedThisFrame();
                    break;
                case ConditionType.InputIsPressed:
                    if (condition.inputAction != null && condition.inputAction.action != null)
                        conditionResult = condition.inputAction.action.IsPressed();
                    break;
                case ConditionType.InputWasReleased:
                    if (condition.inputAction != null && condition.inputAction.action != null)
                        conditionResult = condition.inputAction.action.WasReleasedThisFrame();
                    break;
                case ConditionType.HasInputTag:
                    if (tagComponent != null && condition.requiredTag != null)
                        conditionResult = tagComponent.ConsumeTag(condition.requiredTag);
                    break;
            }
            if (!conditionResult) return false;
        }
        return true;
    }

    public override TimelineEventBase Clone()
    {
        var newEvent = new LoopEvent();
        newEvent.StartFrame = this.StartFrame;
        newEvent.EndFrame = this.EndFrame;
        newEvent.breakConditions = new List<BranchCondition>();
        foreach(var c in this.breakConditions) { newEvent.breakConditions.Add(c.Clone()); }
        return newEvent;
    }
}