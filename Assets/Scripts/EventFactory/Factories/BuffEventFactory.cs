// 文件名: BuffEventFactory.cs
using UnityEngine;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

public class BuffEventFactory : ITimelineEventFactory
{
    public TimelineEventType Type => TimelineEventType.Buff;

    public TimelineEventBase Create() => new BuffEvent();
    
    public TimelineEventBase CreateEvent() => new BuffEvent(); 
    
    public VisualElement CreateInspector(TimelineEventBase evt)
    {
        var buffEvt = evt as BuffEvent;
        var root = new VisualElement();
        if (buffEvt == null) return root;

        // --- 1. BuffSO 资产字段 ---
        var buffDataField = new ObjectField("Buff Data")
        {
            objectType = typeof(BuffSO),
            value = buffEvt.buffData
        };
        buffDataField.RegisterValueChangedCallback(e => buffEvt.buffData = e.newValue as BuffSO);
        root.Add(buffDataField);

        // --- 2. 目标类型下拉菜单 ---
        var targetField = new EnumField("Target", buffEvt.target);
        targetField.RegisterValueChangedCallback(e => buffEvt.target = (BuffEvent.TargetType)e.newValue);
        root.Add(targetField);
        
        // --- 3. 操作类型下拉菜单 ---
        var actionField = new EnumField("Action", buffEvt.action);
        actionField.RegisterValueChangedCallback(e => buffEvt.action = (BuffEvent.ActionType)e.newValue);
        root.Add(actionField);

        return root;
    }

    public void Execute(TimelineEventBase evt, GameObject previewTarget) 
    {
        // (可选) 在这里可以实现编辑器内的预览效果
    }
}