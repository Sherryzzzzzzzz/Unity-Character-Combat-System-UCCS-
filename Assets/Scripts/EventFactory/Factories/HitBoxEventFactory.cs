using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor.UIElements;

public class HitBoxEventFactory : ITimelineEventFactory
{
    public TimelineEventType Type => TimelineEventType.HitBox;

    public TimelineEventBase Create() => new HitBoxEvent();

    public VisualElement CreateInspector(TimelineEventBase evt)
    {
        var hb = evt as HitBoxEvent;
        var root = new VisualElement();

        // 受击盒 prefab
        var prefabField = new ObjectField("HurtBox Prefab")
        {
            objectType = typeof(GameObject),
            value = hb.hurtBoxPrefab
        };
        prefabField.RegisterValueChangedCallback(e => hb.hurtBoxPrefab = e.newValue as GameObject);
        root.Add(prefabField);

        // 是否无敌
        var invincibleField = new Toggle("Invincible") { value = hb.invincible };
        invincibleField.RegisterValueChangedCallback(e => hb.invincible = e.newValue);
        root.Add(invincibleField);

        return root;
    }

    public void Execute(TimelineEventBase evt, GameObject previewTarget)
    {
        var hb = evt as HitBoxEvent;
        
    }
}