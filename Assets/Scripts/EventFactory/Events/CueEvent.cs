using UnityEngine;

public enum CueAction
{
    Execute,  // 一次性触发
    Add,      // 持续效果开始
    Remove    // 持续效果结束
}

[System.Serializable]
public class CueEvent : TimelineEventBase, ITimelineEventRuntime
{
    [Header("Cue 配置")]
    public GameplayTagSO cueTag;
    public CueAction cueAction = CueAction.Execute;

    [Header("可选参数")]
    public Vector3 positionOffset;
    public float scale = 1f;

    public override TimelineEventType Type => TimelineEventType.Cue;

    public override string GetSummary()
    {
        string tagName = cueTag != null ? cueTag.name : "None";
        return $"Cue [{StartFrame}-{EndFrame}] {cueAction} {tagName}";
    }

    public void OnStart(GameObject owner)
    {
        if (cueTag == null) return;

        var cueManager = GameplayCueManager.Instance;
        if (cueManager == null)
        {
            Debug.LogWarning("CueEvent: 场景中未找到 GameplayCueManager");
            return;
        }

        switch (cueAction)
        {
            case CueAction.Execute:
                cueManager.ExecuteCue(cueTag, owner, null);
                break;
            case CueAction.Add:
                cueManager.AddCue(cueTag, owner, null);
                break;
            case CueAction.Remove:
                cueManager.RemoveCue(cueTag, owner);
                break;
        }
    }

    public void OnEnd(GameObject owner) { }

    public override TimelineEventBase Clone()
    {
        return new CueEvent
        {
            StartFrame = StartFrame,
            EndFrame = EndFrame,
            cueTag = cueTag,
            cueAction = cueAction,
            positionOffset = positionOffset,
            scale = scale
        };
    }
}
