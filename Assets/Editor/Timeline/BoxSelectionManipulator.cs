using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// 框选操纵器 — 在时间轴区域拖拽创建选择矩形
/// </summary>
public class BoxSelectionManipulator : PointerManipulator
{
    private readonly SkillEditorTimelineWindow _window;
    private VisualElement _selectionBox;
    private Vector2 _startPos;
    private bool _isSelecting;

    public BoxSelectionManipulator(SkillEditorTimelineWindow window)
    {
        _window = window;
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
        if (evt.button != 0) return;
        // Only start box select if clicking on the background (not on an event clip)
        if (evt.target != target) return;

        _startPos = evt.localPosition;
        _isSelecting = true;
        target.CapturePointer(evt.pointerId);

        _selectionBox = new VisualElement();
        _selectionBox.style.position = Position.Absolute;
        _selectionBox.style.backgroundColor = new Color(0.3f, 0.6f, 1f, 0.15f);
        _selectionBox.style.borderTopWidth = 1;
        _selectionBox.style.borderBottomWidth = 1;
        _selectionBox.style.borderLeftWidth = 1;
        _selectionBox.style.borderRightWidth = 1;
        _selectionBox.style.borderTopColor = new Color(0.3f, 0.6f, 1f, 0.6f);
        _selectionBox.style.borderBottomColor = new Color(0.3f, 0.6f, 1f, 0.6f);
        _selectionBox.style.borderLeftColor = new Color(0.3f, 0.6f, 1f, 0.6f);
        _selectionBox.style.borderRightColor = new Color(0.3f, 0.6f, 1f, 0.6f);
        _selectionBox.pickingMode = PickingMode.Ignore;
        target.Add(_selectionBox);

        evt.StopPropagation();
    }

    private void OnPointerMove(PointerMoveEvent evt)
    {
        if (!_isSelecting || !target.HasPointerCapture(evt.pointerId)) return;

        Vector2 currentPos = evt.localPosition;
        float x = Mathf.Min(_startPos.x, currentPos.x);
        float y = Mathf.Min(_startPos.y, currentPos.y);
        float w = Mathf.Abs(currentPos.x - _startPos.x);
        float h = Mathf.Abs(currentPos.y - _startPos.y);

        _selectionBox.style.left = x;
        _selectionBox.style.top = y;
        _selectionBox.style.width = w;
        _selectionBox.style.height = h;

        evt.StopPropagation();
    }

    private void OnPointerUp(PointerUpEvent evt)
    {
        if (!_isSelecting || !target.HasPointerCapture(evt.pointerId)) return;

        _isSelecting = false;
        target.ReleasePointer(evt.pointerId);

        // Calculate selection rect in local coords
        Vector2 currentPos = evt.localPosition;
        Rect selectionRect = new Rect(
            Mathf.Min(_startPos.x, currentPos.x),
            Mathf.Min(_startPos.y, currentPos.y),
            Mathf.Abs(currentPos.x - _startPos.x),
            Mathf.Abs(currentPos.y - _startPos.y)
        );

        // Remove selection box visual
        if (_selectionBox != null)
        {
            _selectionBox.RemoveFromHierarchy();
            _selectionBox = null;
        }

        // Only process if dragged a meaningful distance
        if (selectionRect.width < 4 && selectionRect.height < 4)
        {
            _window.SelectEvent(null, null);
            return;
        }

        // Find all event clips that overlap the selection rect
        _window.BoxSelectEvents(selectionRect, target);

        evt.StopPropagation();
    }
}
