using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

public class CueEventFactory : ITimelineEventFactory
{
    public TimelineEventType Type => TimelineEventType.Cue;

    public TimelineEventBase Create() => new CueEvent();
    public TimelineEventBase CreateEvent() => new CueEvent();

    public VisualElement CreateInspector(TimelineEventBase evt)
    {
        var cueEvt = evt as CueEvent;
        var root = new VisualElement();
        if (cueEvt == null) return root;

        // Cue Tag
        var tagField = new ObjectField("Cue Tag")
        {
            objectType = typeof(GameplayTagSO),
            allowSceneObjects = false,
            value = cueEvt.cueTag
        };
        tagField.RegisterValueChangedCallback(e => cueEvt.cueTag = e.newValue as GameplayTagSO);
        root.Add(tagField);

        // Cue Action
        var actionField = new EnumField("Cue Action", cueEvt.cueAction);
        actionField.RegisterValueChangedCallback(e => cueEvt.cueAction = (CueAction)e.newValue);
        root.Add(actionField);

        // Position Offset
        var offsetField = new Vector3Field("Position Offset") { value = cueEvt.positionOffset };
        offsetField.RegisterValueChangedCallback(e => cueEvt.positionOffset = e.newValue);
        root.Add(offsetField);

        // Scale
        var scaleField = new FloatField("Scale") { value = cueEvt.scale };
        scaleField.RegisterValueChangedCallback(e => cueEvt.scale = e.newValue);
        root.Add(scaleField);

        return root;
    }
}
