using System;
using UnityEngine;

[Flags]
public enum CancelActionType
{
    None  = 0,
    Dodge = 1 << 0, // 翻滚/闪避
    Move  = 1 << 1, // 行走/跑步
    Jump  = 1 << 2, // 跳跃
    Guard = 1 << 3, // 防御/格挡
    All   = ~0,
}

[System.Serializable]
public class CancelEvent : TimelineEventBase, ITimelineEventRuntime
{
    [Tooltip("在这个时间窗口内，当前技能可以被哪些类型的动作打断")]
    public CancelActionType CancelableBy = CancelActionType.Dodge | CancelActionType.Move;

    public override TimelineEventType Type => TimelineEventType.Cancel;
    public override string GetSummary() => $"Cancel Window By: {CancelableBy}";

    // 这个事件是“被动”的，它只提供数据，运行时逻辑在 PlayerSkillComponent 中
    public void OnStart(GameObject owner) {}
    public void OnEnd(GameObject owner) {}

    public override TimelineEventBase Clone()
    {
        return new CancelEvent {
            StartFrame = this.StartFrame,
            EndFrame = this.EndFrame,
            CancelableBy = this.CancelableBy
        };
    }
}