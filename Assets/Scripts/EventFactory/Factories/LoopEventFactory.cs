using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;

public class LoopEventFactory : ITimelineEventFactory
{
    // 告诉编辑器这个工厂对应哪种事件类型
    public TimelineEventType Type => TimelineEventType.Loop;

    // 当点击 "Add Loop Event" 时，创建一个新的 LoopEvent 实例
    public TimelineEventBase Create() => new LoopEvent();
    public TimelineEventBase CreateEvent() => new LoopEvent();
    
    // 创建在 Inspector 中显示的 UI
    public VisualElement CreateInspector(TimelineEventBase evt)
    {
        // 因为 LoopEvent 没有任何自定义字段，我们只需要创建一个空的容器
        // 并在里面放一个简单的帮助提示
        var root = new VisualElement();
        
        var helpBox = new HelpBox(
            "This event marks a loop section. The animation will repeat between the Start and End frames of this event. " +
            "Use a Branch Event to exit the loop based on a condition.", 
            HelpBoxMessageType.Info);
            
        root.Add(helpBox);
        
        return root;
    }

    // LoopEvent 没有预览效果
    public void Execute(TimelineEventBase evt, GameObject previewTarget) { }
}