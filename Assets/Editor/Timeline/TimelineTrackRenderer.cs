using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// 轨道渲染器 — 负责轨道行和事件条的渲染
/// </summary>
public class TimelineTrackRenderer
{
    private const float TRACK_HEIGHT = 28f;

    private AnimationClip _clip;
    private int _totalFrames;

    public void SetClipData(AnimationClip clip, int totalFrames)
    {
        _clip = clip;
        _totalFrames = totalFrames;
    }

    public void RefreshTrackContent(TimelineData data, TimelineEventBase selectedEvent,
        System.Action<TimelineEventBase, VisualElement> onEventClicked,
        System.Action<TimelineEventBase, TimelineData> onEventDeleted,
        SkillEditorTimelineWindow window)
    {
        var row = data.trackRow;
        if (row == null) return;
        row.Clear();
        if (_clip == null || _totalFrames <= 0) return;

        if (row.resolvedStyle.width <= 1)
        {
            row.schedule.Execute(() => RefreshTrackContent(data, selectedEvent, onEventClicked, onEventDeleted, window)).StartingIn(10);
            return;
        }

        foreach (var evt in data.events)
        {
            var clipElement = CreateEventClipUI(evt, data, onEventClicked, onEventDeleted, window);
            row.Add(clipElement);
            if (selectedEvent == evt)
            {
                clipElement.AddToClassList("event-clip--selected");
            }
        }
    }

    private VisualElement CreateEventClipUI(TimelineEventBase evt, TimelineData track,
        System.Action<TimelineEventBase, VisualElement> onEventClicked,
        System.Action<TimelineEventBase, TimelineData> onEventDeleted,
        SkillEditorTimelineWindow window)
    {
        if (_clip == null || _totalFrames <= 0 || track.trackRow.resolvedStyle.width <= 1)
            return new VisualElement();

        float pixelsPerFrame = track.trackRow.resolvedStyle.width / _totalFrames;
        float startX = evt.StartFrame * pixelsPerFrame;
        float endX = evt.EndFrame * pixelsPerFrame;

        var clipContainer = new VisualElement();
        clipContainer.userData = evt;
        clipContainer.AddToClassList("event-clip");
        clipContainer.style.position = Position.Absolute;
        clipContainer.style.left = startX;
        clipContainer.style.width = Mathf.Max(pixelsPerFrame > 0 ? pixelsPerFrame : 2, endX - startX);
        clipContainer.style.height = TRACK_HEIGHT - 6;
        clipContainer.style.top = 3;

        clipContainer.AddManipulator(new EventClipManipulator(window, track, evt));

        var color = GetColorByType(evt.Type);
        clipContainer.style.backgroundColor = new Color(color.r, color.g, color.b, 0.8f);

        var header = new VisualElement { name = "header", pickingMode = PickingMode.Position };
        header.AddToClassList("event-clip__header");
        header.style.backgroundColor = new Color(color.r, color.g, color.b, 1f);

        var label = new Label(evt.GetSummary())
        {
            style = { paddingLeft = 6, color = Color.white },
            pickingMode = PickingMode.Ignore
        };

        clipContainer.Add(header);
        header.Add(label);

        clipContainer.AddManipulator(new ContextualMenuManipulator(menuEvt =>
            menuEvt.menu.AppendAction("删除事件", a => onEventDeleted?.Invoke(evt, track))));

        return clipContainer;
    }

    public static Color GetColorByType(TimelineEventType type)
    {
        switch (type)
        {
            case TimelineEventType.Attack: return new Color(0.85f, 0.35f, 0.35f);
            case TimelineEventType.HitBox: return new Color(0.28f, 0.6f, 1f);
            case TimelineEventType.Combo: return new Color(0.25f, 0.9f, 0.35f);
            case TimelineEventType.Effect: return new Color(0.9f, 0.75f, 0.2f);
            case TimelineEventType.Sound: return new Color(0.8f, 0.5f, 1f);
            case TimelineEventType.Buff: return new Color(0.5f, 0.5f, 0.5f);
            case TimelineEventType.Loop: return new Color(0.3f, 0.8f, 0.8f);
            case TimelineEventType.Cancel: return new Color(0.85f, 0.85f, 0.8f);
            case TimelineEventType.GASEffect: return new Color(0.2f, 0.8f, 0.4f);
            case TimelineEventType.TargetSearch: return new Color(0.3f, 0.5f, 1f);
            case TimelineEventType.Cue: return new Color(1f, 0.6f, 0.2f);
            case TimelineEventType.CooldownTrigger: return new Color(0.6f, 0.3f, 0.8f);
            default: return Color.gray;
        }
    }
}
