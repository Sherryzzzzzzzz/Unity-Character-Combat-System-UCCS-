using UnityEngine;

[System.Serializable]
public class HitBoxEvent : TimelineEventBase, ITimelineEventRuntime
{
    public enum ActionType { Activate, Deactivate }

    [Tooltip("拥有受击盒的角色 (必须带有 HurtBoxManager 组件)")]
    public HurtBoxManager targetManager;

    // ✅ 用 GameplayTagSO 替代 GameBodyPart
    [Tooltip("要操作的身体部位 Tag")]
    public GameplayTagSO bodyPartTag;

    [Tooltip("是激活还是关闭该部位的受击盒")]
    public ActionType action = ActionType.Activate;

    [Tooltip("如果激活，该部位是否处于无敌状态")]
    public bool isInvincible;

    public override TimelineEventType Type => TimelineEventType.HitBox;

    private HurtBoxManager managerToUse;

    public override string GetSummary()
    {
        string managerName = targetManager != null ? targetManager.gameObject.name : "None";
        string tagName = bodyPartTag != null ? bodyPartTag.name : "None";

        return $"HurtBox: {action} [{managerName}'s {tagName}] (Invincible: {isInvincible})";
    }

    public void OnStart(GameObject owner)
    {
        managerToUse = targetManager;

        if (managerToUse == null)
            managerToUse = owner.GetComponent<HurtBoxManager>();

        if (managerToUse == null)
        {
            Debug.LogError($"HitBoxEvent 在帧 [{StartFrame}] 找不到 HurtBoxManager！", owner);
            return;
        }

        managerToUse.isInvincible = isInvincible;

        if (bodyPartTag == null)
        {
            Debug.LogWarning("HitBoxEvent 未设置 BodyPartTag");
            return;
        }

        if (action == ActionType.Activate)
            managerToUse.ActivateHurtBox(bodyPartTag);
        else
            managerToUse.DeactivateHurtBox(bodyPartTag);
    }

    public override TimelineEventBase Clone()
    {
        return new HitBoxEvent
        {
            StartFrame = StartFrame,
            EndFrame = EndFrame,
            targetManager = targetManager,
            bodyPartTag = bodyPartTag,
            action = action,
            isInvincible = isInvincible
        };
    }

    public void OnEnd(GameObject owner)
    {
        managerToUse = targetManager;

        if (managerToUse == null)
            managerToUse = owner.GetComponent<HurtBoxManager>();

        if (managerToUse != null)
            managerToUse.isInvincible = false;
    }
}
