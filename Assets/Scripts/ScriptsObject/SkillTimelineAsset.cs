using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Skill/Timeline Asset", fileName = "NewSkillTimeline")]
public class SkillTimelineAsset : ScriptableObject
{
    [SerializeReference]
    public AnimationClip animationClip;
    [SerializeReference] 
    public List<TimelineTrackData> tracks = new List<TimelineTrackData>();
}

[System.Serializable]
public class TimelineTrackData
{
    public string name;
    public TimelineEventType type;
    [SerializeReference]
    public List<TimelineEventBase> events = new List<TimelineEventBase>();
}