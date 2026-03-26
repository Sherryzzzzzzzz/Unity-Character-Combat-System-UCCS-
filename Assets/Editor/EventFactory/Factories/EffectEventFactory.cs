using System.Collections;
using System.Collections.Generic;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

public class EffectEventFactory : ITimelineEventFactory
{
    public TimelineEventType Type => TimelineEventType.Effect;
    
    public TimelineEventBase Create()
    {
        var newEvent = new EffectEvent();
        newEvent.StartFrame = 0;
        newEvent.EndFrame = 1;
        newEvent.effectPrefab = null;
        newEvent.effectPosition = new Vector3(0, 0,0);
        newEvent.effectRotation = Quaternion.identity;
        return newEvent;
    }
    
    public TimelineEventBase CreateEvent() => new EffectEvent();
    
    public VisualElement CreateInspector(TimelineEventBase evt)
    {
        var effect = evt as EffectEvent;
        var container = new VisualElement();
        
        var effectField = new ObjectField("特效预制体")
        {
            objectType = typeof(GameObject),
            value = effect.effectPrefab,
            tooltip = "要在此时间点播放的特效预制体。"
        };
        effectField.RegisterValueChangedCallback(e =>
        {
            effect.effectPrefab = e.newValue as GameObject;
        });
        container.Add(effectField);
        
        var effectPosField = new Vector3Field("特效位置")
        {
            value = effect.effectPosition,
            tooltip = "特效相对于角色的位置偏移。"
        };
        effectPosField.RegisterValueChangedCallback(e =>
        {
            effect.effectPosition = e.newValue;
        });
        container.Add(effectPosField);
        
        var effectRotField = new Vector3Field("特效旋转 (欧拉角)")
        {
            value = effect.effectRotation.eulerAngles,
            tooltip = "特效的旋转角度（以欧拉角表示）。"
        };
        effectRotField.RegisterValueChangedCallback(e =>
        {
            effect.effectRotation = Quaternion.Euler(e.newValue);
        });
        container.Add(effectRotField);

        return container;
    }
}
