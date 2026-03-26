using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 轨道管理器 — 负责轨道数据的增删改操作
/// </summary>
public class TrackManager
{
    private List<TimelineData> _timelines;
    private SkillTimelineAsset _asset;
    private AnimationClip _clip;

    public List<TimelineData> Timelines => _timelines;

    public TrackManager()
    {
        _timelines = new List<TimelineData>();
    }

    public void SetAsset(SkillTimelineAsset asset)
    {
        _asset = asset;
    }

    public void SetClip(AnimationClip clip)
    {
        _clip = clip;
    }

    public void LoadFromAsset(SkillTimelineAsset asset)
    {
        _asset = asset;
        _timelines.Clear();

        if (asset != null)
        {
            _timelines = asset.tracks.Select(t => new TimelineData(t.type, t.name)
            {
                events = t.events.Select(e => e.Clone()).ToList()
            }).ToList();
        }
    }

    public void AddTrack(TimelineEventType type, string name)
    {
        if (_asset != null) Undo.RecordObject(_asset, "添加轨道");
        _timelines.Add(new TimelineData(type, name));
    }

    public void RemoveTrack(TimelineData track)
    {
        if (_asset != null) Undo.RecordObject(_asset, "删除轨道");
        _timelines.Remove(track);
    }

    public void AddEventToTrack(TimelineData track, TimelineEventBase evt)
    {
        if (_asset != null) Undo.RecordObject(_asset, "添加事件");
        track.AddEvent(evt);
    }

    public void RemoveEventFromTrack(TimelineData track, TimelineEventBase evt)
    {
        if (_asset != null) Undo.RecordObject(_asset, "删除事件");
        track.events.Remove(evt);
    }

    public void SaveToAsset(SkillTimelineAsset asset, AnimationClip clip)
    {
        if (asset == null) return;

        asset.animationClip = clip;
        asset.tracks = _timelines.Select(t =>
        {
            if (t.events == null) t.events = new List<TimelineEventBase>();
            return new TimelineTrackData
            {
                name = t.name,
                type = t.type,
                events = t.events
            };
        }).ToList();

        EditorUtility.SetDirty(asset);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    public TimelineEventBase CreateDefaultEvent(TimelineEventType type, int frame)
    {
        if (_clip == null) return null;
        var factory = EventFactoryRegistry.GetFactory(type);
        if (factory == null) return null;
        var evt = factory.CreateEvent();
        evt.StartFrame = frame;
        int defaultDuration = _clip.frameRate > 0 ? Mathf.RoundToInt(_clip.frameRate * 0.25f) : 15;
        evt.EndFrame = frame + Mathf.Max(1, defaultDuration);
        return evt;
    }

    public TimelineData FindTrackContaining(TimelineEventBase evt)
    {
        return _timelines.FirstOrDefault(t => t.events.Contains(evt));
    }
}
