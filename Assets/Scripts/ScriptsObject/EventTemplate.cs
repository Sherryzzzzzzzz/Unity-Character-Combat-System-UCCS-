using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 事件模板 — 保存可复用的事件组合
/// </summary>
[CreateAssetMenu(fileName = "NewEventTemplate", menuName = "Skill/Event Template")]
public class EventTemplate : ScriptableObject
{
    [Tooltip("模板名称")]
    public string templateName;

    [Tooltip("模板描述")]
    [TextArea(2, 4)]
    public string description;

    [Tooltip("模板中包含的事件列表")]
    [SerializeReference]
    public List<TimelineEventBase> events = new List<TimelineEventBase>();
}
