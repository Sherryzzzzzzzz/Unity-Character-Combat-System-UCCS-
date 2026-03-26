using UnityEngine;
using UnityEngine.UIElements;

public class TargetSearchEventFactory : ITimelineEventFactory
{
    public TimelineEventType Type => TimelineEventType.TargetSearch;

    public TimelineEventBase Create() => new TargetSearchEvent();
    public TimelineEventBase CreateEvent() => new TargetSearchEvent();

    public VisualElement CreateInspector(TimelineEventBase evt)
    {
        var searchEvt = evt as TargetSearchEvent;
        var root = new VisualElement();
        if (searchEvt == null) return root;

        var header = new Label("搜索参数");
        header.style.unityFontStyleAndWeight = FontStyle.Bold;
        root.Add(header);

        GameplayEffectEventFactory.BuildSearchParametersUI(root, searchEvt.searchParameters);

        return root;
    }
}
