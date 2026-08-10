using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Skill/Timeline Asset", fileName = "NewSkillTimeline")]
public class SkillTimelineAsset : ScriptableObject
{
    [SerializeReference]
    public AnimationClip animationClip;
    [SerializeReference] 
    public List<TimelineTrackData> tracks = new List<TimelineTrackData>();

    [Tooltip("★ P13: 空中技能标记。作为连招目标时，若玩家在地面会自动起跳追击（追击跳）")]
    public bool isAirSkill;
}

[System.Serializable]
public class TimelineTrackData
{
    public string name;
    public TimelineEventType type;
    [SerializeReference]
    public List<TimelineEventBase> events = new List<TimelineEventBase>();
}