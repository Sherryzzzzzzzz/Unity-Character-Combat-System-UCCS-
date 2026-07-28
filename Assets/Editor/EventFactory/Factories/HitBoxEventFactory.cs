// HitBoxEventFactory.cs
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;
using UnityEditor.UIElements;
using System.Collections.Generic;
using System.Linq;

public class HitBoxEventFactory : ITimelineEventFactory
{
    public TimelineEventType Type => TimelineEventType.HitBox;

    public TimelineEventBase Create() => new HitBoxEvent();
    
    public TimelineEventBase CreateEvent() => new HitBoxEvent(); 

    public VisualElement CreateInspector(TimelineEventBase evt)
    {
        var hb = evt as HitBoxEvent;
        var root = new VisualElement();
        if (hb == null) return root;

        root.style.paddingTop = 4;

        // Target Manager
        var managerField = new ObjectField("目标管理器")
        {
            objectType = typeof(HurtBoxManager),
            allowSceneObjects = true,
            value = hb.targetManager
        };

        managerField.RegisterValueChangedCallback(e =>
        {
            hb.targetManager = e.newValue as HurtBoxManager;
        });

        root.Add(managerField);

        // ✅ Body Part Tag 改成 ObjectField
        var bodyPartField = new ObjectField("身体部位标签")
        {
            objectType = typeof(GameplayTagSO),
            allowSceneObjects = false,
            value = hb.bodyPartTag
        };

        bodyPartField.RegisterValueChangedCallback(e =>
        {
            hb.bodyPartTag = e.newValue as GameplayTagSO;
        });

        root.Add(bodyPartField);

        // Action
        var actionField = new EnumField("动作", hb.action);
        actionField.RegisterValueChangedCallback(e =>
            hb.action = (HitBoxEvent.ActionType)e.newValue);

        root.Add(actionField);

        // Invincible
        var invincibleField = new Toggle("是否无敌")
        {
            value = hb.isInvincible
        };

        invincibleField.RegisterValueChangedCallback(e =>
            hb.isInvincible = e.newValue);

        root.Add(invincibleField);

        return root;
    }


    public void Execute(TimelineEventBase evt, GameObject previewTarget) { }
}