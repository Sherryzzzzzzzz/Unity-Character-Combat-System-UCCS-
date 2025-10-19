using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using System.Linq;

public enum TimelineEventType
{
    Attack,
    HitBox,
    Combo,
    Effect,
    Sound
}

[Serializable]
public abstract class TimelineEventBase
{
    [SerializeField] public int startFrame;
    [SerializeField] public int endFrame;

    public int StartFrame
    {
        get => Mathf.Min(startFrame, endFrame);
        set => startFrame = value;
    }

    public int EndFrame
    {
        get => Mathf.Max(startFrame, endFrame);
        set => endFrame = value;
    }

    public abstract TimelineEventType Type { get; }
    public abstract string GetSummary();
}

public interface ITimelineEventRuntime
{
    void OnStart(GameObject owner);
    void OnEnd(GameObject owner);
}


public interface ITimelineEventFactory
{
    TimelineEventType Type { get; }
    TimelineEventBase Create();
    VisualElement CreateInspector(TimelineEventBase evt);
}

public static class EventFactoryRegistry
{
    private static Dictionary<TimelineEventType, ITimelineEventFactory> _factories = new();

    public static void Register(ITimelineEventFactory factory)
    {
        _factories[factory.Type] = factory;
    }

    public static ITimelineEventFactory GetFactory(TimelineEventType type)
    {
        return _factories.TryGetValue(type, out var f) ? f : null;
    }

    public static IEnumerable<ITimelineEventFactory> GetAllFactories()
    {
        return _factories.Values;
    }
}