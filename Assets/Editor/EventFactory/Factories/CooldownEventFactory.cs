using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

public class CooldownEventFactory : ITimelineEventFactory
{
    public TimelineEventType Type => TimelineEventType.CooldownTrigger;

    public TimelineEventBase Create() => new CooldownEvent();
    public TimelineEventBase CreateEvent() => new CooldownEvent();

    public VisualElement CreateInspector(TimelineEventBase evt)
    {
        var cdEvt = evt as CooldownEvent;
        var root = new VisualElement();
        if (cdEvt == null) return root;

        // Cooldown Effect
        var effectField = new ObjectField("冷却效果")
        {
            objectType = typeof(GameplayEffect),
            allowSceneObjects = false,
            value = cdEvt.cooldownEffect,
            tooltip = "Duration 类型的 GameplayEffect，用作冷却效果"
        };
        effectField.RegisterValueChangedCallback(e => cdEvt.cooldownEffect = e.newValue as GameplayEffect);
        root.Add(effectField);

        // Cooldown Tag
        var tagField = new ObjectField("冷却标签")
        {
            objectType = typeof(GameplayTagSO),
            allowSceneObjects = false,
            value = cdEvt.cooldownTag,
            tooltip = "冷却标签，用于 IsOnCooldown 检查"
        };
        tagField.RegisterValueChangedCallback(e => cdEvt.cooldownTag = e.newValue as GameplayTagSO);
        root.Add(tagField);

        return root;
    }
}
