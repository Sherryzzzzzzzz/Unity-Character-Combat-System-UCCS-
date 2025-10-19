using System.Collections.Generic;

public static class TimelineEventRegistry
{
    private static readonly Dictionary<TimelineEventType, ITimelineEventFactory> _factories 
        = new Dictionary<TimelineEventType, ITimelineEventFactory>();

    public static void Register(ITimelineEventFactory factory)
    {
        _factories[factory.Type] = factory;
    }

    public static ITimelineEventFactory Get(TimelineEventType type)
    {
        return _factories[type];
    }

    public static IEnumerable<ITimelineEventFactory> AllFactories => _factories.Values;
}