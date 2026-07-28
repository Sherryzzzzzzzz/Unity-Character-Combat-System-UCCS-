using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;
using UnityEditor.UIElements;

public class SoundEventFactory : ITimelineEventFactory
{
    public TimelineEventType Type => TimelineEventType.Sound;
    
    public TimelineEventBase Create()
    {
        var newEvent = new SoundEvent();
        newEvent.StartFrame = 0;
        newEvent.EndFrame = 1;
        newEvent.loop = false;
        newEvent.soundClip = null;
        newEvent.volume = 1.0f;
        return newEvent;
    }
    
    public TimelineEventBase CreateEvent() => new SoundEvent();
    
    public VisualElement CreateInspector(TimelineEventBase evt)
    {
        var effect = evt as SoundEvent;
        var container = new VisualElement();
        
        var clipField = new ObjectField("音频剪辑")
        {
            objectType = typeof(AudioClip),
            value = effect.soundClip,
            tooltip = "播放的音频剪辑"
        };
        clipField.RegisterValueChangedCallback(e =>
        {
            effect.soundClip = e.newValue as AudioClip;
        });
        container.Add(clipField);
        
        var volumeField = new FloatField("音量")
        {
            value = effect.volume,
            tooltip = "设置音频的播放音量"
        };
        volumeField.RegisterValueChangedCallback(e =>
        {
            effect.volume = Mathf.Clamp01(e.newValue);
        });
        container.Add(volumeField);
        
        var loopField = new Toggle("循环")
        {
            value = effect.loop,
            tooltip = "是否循环"
        };
        loopField.RegisterValueChangedCallback(e =>
        {
            effect.loop = e.newValue;
        });
        container.Add(loopField);

        return container;
    }
}
