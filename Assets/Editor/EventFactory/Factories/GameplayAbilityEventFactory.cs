using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

public class GameplayAbilityEventFactory : ITimelineEventFactory
{
    public TimelineEventType Type => TimelineEventType.GameplayAbility;

    public TimelineEventBase Create() => new GameplayAbilityEvent();
    public TimelineEventBase CreateEvent() => new GameplayAbilityEvent();

    public VisualElement CreateInspector(TimelineEventBase evt)
    {
        var abilityEvt = evt as GameplayAbilityEvent;
        var root = new VisualElement();
        if (abilityEvt == null) return root;

        // GameplayAbilitySO reference
        var abilityField = new ObjectField("游戏技能")
        {
            objectType = typeof(GameplayAbilitySO),
            allowSceneObjects = false,
            value = abilityEvt.abilityRef
        };
        abilityField.RegisterValueChangedCallback(e =>
            abilityEvt.abilityRef = e.newValue as GameplayAbilitySO);
        root.Add(abilityField);

        // Event Tag
        var tagField = new ObjectField("事件标签")
        {
            objectType = typeof(GameplayTagSO),
            allowSceneObjects = false,
            value = abilityEvt.eventTag
        };
        tagField.RegisterValueChangedCallback(e =>
            abilityEvt.eventTag = e.newValue as GameplayTagSO);
        root.Add(tagField);

        // Wait for end toggle
        var waitToggle = new Toggle("等待结束")
        {
            value = abilityEvt.waitForEnd
        };
        waitToggle.RegisterValueChangedCallback(e =>
            abilityEvt.waitForEnd = e.newValue);
        root.Add(waitToggle);

        return root;
    }
}
