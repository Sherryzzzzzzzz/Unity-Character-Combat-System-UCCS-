using UnityEngine;

[System.Serializable]
public class LoopEvent : TimelineEventBase, ITimelineEventRuntime
{
    // 这个事件只是一个标记，不需要额外的配置字段
    
    public override TimelineEventType Type => TimelineEventType.Loop;
    public override string GetSummary() => $"Loop Section [{StartFrame}-{EndFrame}]";
    
    public void OnStart(GameObject owner) { }
    public void OnEnd(GameObject owner) { }

    public override TimelineEventBase Clone()
    {
        var newEvent = new LoopEvent();
        newEvent.StartFrame = StartFrame;
        newEvent.EndFrame = EndFrame;
        return newEvent;
    }
}