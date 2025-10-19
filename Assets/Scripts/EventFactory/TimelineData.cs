using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

[System.Serializable]
public class TimelineData
{
    public string name;
    public TimelineEventType type;
    public List<TimelineEventBase> events = new();
    public VisualElement trackRow;

    public TimelineData(TimelineEventType type, string name)
    {
        this.type = type;
        this.name = name;
    }

    public void AddEvent(TimelineEventBase evt)
    {
        events.Add(evt);
    }

    public void RemoveEvent(TimelineEventBase evt)
    {
        events.Remove(evt);
    }
}
