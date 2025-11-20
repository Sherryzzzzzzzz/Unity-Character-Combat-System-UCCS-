// EventClipManipulator.cs (简化版 - 主要用于选中事件)

using UnityEngine;
using UnityEngine.UIElements;

public class EventClipManipulator : PointerManipulator
{
    private readonly SkillEditorTimelineWindow _window;
    private readonly TimelineData _track;
    private readonly TimelineEventBase _event;

    public EventClipManipulator(SkillEditorTimelineWindow window, TimelineData track, TimelineEventBase evt)
    {
        _window = window;
        _track = track;
        _event = evt;
    }

    protected override void RegisterCallbacksOnTarget()
    {
        target.RegisterCallback<PointerDownEvent>(OnPointerDown);
    }

    protected override void UnregisterCallbacksFromTarget()
    {
        target.UnregisterCallback<PointerDownEvent>(OnPointerDown);
    }

    private void OnPointerDown(PointerDownEvent evt)
    {
        // 主要功能：当鼠标左键按下时，选中这个事件。
        if (evt.button == 0)
        {
            _window.SelectEvent(_event, target as VisualElement);
            // 阻止事件冒泡，以免触发轨道或标尺的点击事件
            evt.StopPropagation();
        }
    }
}