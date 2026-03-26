using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

public class EventClipManipulator : PointerManipulator
{
    private readonly SkillEditorTimelineWindow _window;
    private readonly TimelineData _track;
    private readonly TimelineEventBase _event;

    private bool _isDragging;
    private float _dragStartMouseX;
    private int _dragStartFrame;
    private int _dragStartEndFrame;
    private bool _undoRecorded;

    public EventClipManipulator(SkillEditorTimelineWindow window, TimelineData track, TimelineEventBase evt)
    {
        _window = window;
        _track = track;
        _event = evt;
    }

    protected override void RegisterCallbacksOnTarget()
    {
        target.RegisterCallback<PointerDownEvent>(OnPointerDown);
        target.RegisterCallback<PointerMoveEvent>(OnPointerMove);
        target.RegisterCallback<PointerUpEvent>(OnPointerUp);
    }

    protected override void UnregisterCallbacksFromTarget()
    {
        target.UnregisterCallback<PointerDownEvent>(OnPointerDown);
        target.UnregisterCallback<PointerMoveEvent>(OnPointerMove);
        target.UnregisterCallback<PointerUpEvent>(OnPointerUp);
    }

    private void OnPointerDown(PointerDownEvent evt)
    {
        if (evt.button == 0)
        {
            bool additive = evt.shiftKey;
            _window.SelectEvent(_event, target as VisualElement, additive);
            if (!additive)
            {
                _isDragging = true;
                _undoRecorded = false;
                _dragStartMouseX = evt.position.x;
                _dragStartFrame = _event.StartFrame;
                _dragStartEndFrame = _event.EndFrame;
                target.CapturePointer(evt.pointerId);
            }
            evt.StopPropagation();
        }
    }

    private void OnPointerMove(PointerMoveEvent evt)
    {
        if (!_isDragging || !target.HasPointerCapture(evt.pointerId)) return;

        float deltaX = evt.position.x - _dragStartMouseX;
        if (_track.trackRow == null || _track.trackRow.resolvedStyle.width <= 0) return;

        int totalFrames = _window.GetTotalFrames();
        if (totalFrames <= 0) return;

        float pixelsPerFrame = _track.trackRow.resolvedStyle.width / totalFrames;
        if (pixelsPerFrame <= 0) return;

        int frameDelta = Mathf.RoundToInt(deltaX / pixelsPerFrame);
        if (frameDelta == 0) return;

        if (!_undoRecorded)
        {
            _window.RecordUndoForDrag("拖拽事件");
            _undoRecorded = true;
        }

        int duration = _dragStartEndFrame - _dragStartFrame;
        int newStart = Mathf.Clamp(_dragStartFrame + frameDelta, 0, totalFrames - duration);
        _event.StartFrame = newStart;
        _event.EndFrame = newStart + duration;

        _window.RefreshTrackContentUI(_track);
        _window.OpenEventEditor(_event);
        evt.StopPropagation();
    }

    private void OnPointerUp(PointerUpEvent evt)
    {
        if (_isDragging && target.HasPointerCapture(evt.pointerId))
        {
            _isDragging = false;
            target.ReleasePointer(evt.pointerId);
            evt.StopPropagation();
        }
    }
}
