using UnityEngine;
using System;

public class ComboEvent : TimelineEventBase, ITimelineEventRuntime
{
    [Tooltip("触发此连招所需的 Tag")]
    public GameplayTagSO RequiredTag;

    [Tooltip("成功触发后要播放的下一个技能")]
    public SkillTimelineAsset nextSkill;

    // 新增：明确的连招模式
    public enum ComboMode { Normal_Cacheable, Strict_Immediate }
    [Tooltip("Normal: 允许提前输入 (使用缓存)。Strict: 必须在窗口期内精确输入。")]
    public ComboMode comboMode = ComboMode.Normal_Cacheable;

    public override TimelineEventType Type => TimelineEventType.Combo;
    public override string GetSummary() => $"({comboMode}) [{RequiredTag?.name ?? "None"}] -> {nextSkill?.name ?? "None"}";
    
    public void OnStart(GameObject owner) { }
    
    public override TimelineEventBase Clone()
    {
        var newEvent = new ComboEvent();
        newEvent.StartFrame = StartFrame;
        newEvent.EndFrame = EndFrame;
        newEvent.RequiredTag = RequiredTag;
        newEvent.nextSkill = nextSkill;
        return newEvent;
    }
    
    public void OnEnd(GameObject owner) { }
}