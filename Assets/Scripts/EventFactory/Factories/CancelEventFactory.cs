using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor.UIElements;

public class CancelEventFactory : ITimelineEventFactory
{
    public TimelineEventType Type => TimelineEventType.Cancel;

    public TimelineEventBase Create()
    {
        return new CancelEvent();
    }
    
    public TimelineEventBase CreateEvent() => new CancelEvent(); 

    public VisualElement CreateInspector(TimelineEventBase evt)
    {
        var cancel = evt as CancelEvent;
        var root = new VisualElement();
        if (cancel == null)
        {
            root.Add(new Label("事件数据不是 CancelEvent 类型"));
            return root;
        }
        
        root.style.paddingTop = 4;
        
        return root;
    }
    
    public void Execute(TimelineEventBase evt, GameObject previewTarget) 
    {
    }
}