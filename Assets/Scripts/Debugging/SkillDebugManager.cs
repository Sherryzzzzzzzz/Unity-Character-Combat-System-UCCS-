using System;
using UnityEngine;

// 这是传递的消息内容
public class SkillDebugInfo
{
    public GameObject SourceObject { get; }
    public SkillTimelineAsset SkillAsset { get; }
    public int CurrentFrame { get; }
    public int MaxFrame { get; }

    public SkillDebugInfo(GameObject source, SkillTimelineAsset asset, int currentFrame, int maxFrame)
    {
        SourceObject = source;
        SkillAsset = asset;
        CurrentFrame = currentFrame;
        MaxFrame = maxFrame;
    }
}

// 全局静态广播站
public static class SkillDebugManager
{
    // 事件：当技能帧更新时广播
    public static event Action<SkillDebugInfo> OnSkillFrameUpdate;

    // 事件：当技能停止或角色不再播放技能时广播
    public static event Action<GameObject> OnSkillStop;
    
    // 供运行时组件调用，用于广播更新
    public static void ReportSkillFrameUpdate(GameObject source, SkillTimelineAsset asset, int currentFrame, int maxFrame)
    {
        // 只在编辑器环境下广播，避免在发布版本中产生开销
#if UNITY_EDITOR
        OnSkillFrameUpdate?.Invoke(new SkillDebugInfo(source, asset, currentFrame, maxFrame));
#endif
    }

    // 供运行时组件调用，用于广播停止
    public static void ReportSkillStop(GameObject source)
    {
#if UNITY_EDITOR
        OnSkillStop?.Invoke(source);
#endif
    }
}