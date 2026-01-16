using UnityEngine;

[System.Serializable]
public class HitBoxEvent : TimelineEventBase, ITimelineEventRuntime
{
    public enum ActionType { Activate, Deactivate }
    
    // --- 核心改动 1：引用 HurtBoxManager ---
    [Tooltip("拥有受击盒的角色 (必须带有 HurtBoxManager 组件)")]
    public HurtBoxManager targetManager; 

    // --- 核心改动 2：指定身体部位 ---
    [Tooltip("要操作的身体部位")]
    public GameBodyPart bodyPart;

    [Tooltip("是对该部位的受击盒执行'激活'还是'关闭'操作")]
    public ActionType action = ActionType.Activate;
    
    [Tooltip("如果激活，该部位是否处于无敌状态")]
    public bool isInvincible;

    public override TimelineEventType Type => TimelineEventType.HitBox;
    private HurtBoxManager managerToUse;

    public override string GetSummary()
    {
        string managerName = targetManager != null ? targetManager.gameObject.name : "None";
        return $"HurtBox: {action} [{managerName}'s {bodyPart}] (Invincible: {isInvincible})";
    }

    // --- 运行时逻辑更新 ---
    public void OnStart(GameObject owner)
    {
        // 如果事件上没有指定 targetManager，我们可以默认尝试使用技能的持有者 (owner)
         managerToUse = targetManager;
        if (managerToUse == null)
        {
            managerToUse = owner.GetComponent<HurtBoxManager>();
        }

        if (managerToUse == null)
        {
            Debug.LogError($"HitBoxEvent 在帧 [{StartFrame}] 无法找到 HurtBoxManager！请在事件上指定 Target Manager，或者确保技能持有者 ({owner.name}) 身上有此组件。", owner);
            return;
        }

        if (isInvincible)
        {
            managerToUse.isInvincible = true;
        }
        else
        {
            managerToUse.isInvincible = false;
        }

        // --- 逻辑回归：通过 Manager 来操作 ---
        if (action == ActionType.Activate)
        {
            managerToUse.ActivateHurtBox(bodyPart);
            
            // 在这里可以进一步处理 isInvincible 状态
            // managerToUse.SetInvincibility(bodyPart, isInvincible);
        }
        else // Deactivate
        {
            managerToUse.DeactivateHurtBox(bodyPart);
        }
    }
    
    public override TimelineEventBase Clone()
    {
        var newEvent = new HitBoxEvent();
        newEvent.StartFrame = StartFrame;
        newEvent.EndFrame = EndFrame;
        newEvent.targetManager = targetManager;
        newEvent.bodyPart = bodyPart;
        newEvent.action = action;
        newEvent.isInvincible = isInvincible;
        return newEvent;
    }
    
    public void OnEnd(GameObject owner)
    {
        managerToUse = targetManager;
        if (managerToUse == null)
        {
            managerToUse = owner.GetComponent<HurtBoxManager>();
        }
        if (managerToUse != null)
            managerToUse.isInvincible = false;
    }
}