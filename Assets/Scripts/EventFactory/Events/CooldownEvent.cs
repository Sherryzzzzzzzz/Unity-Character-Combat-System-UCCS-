using UnityEngine;

[System.Serializable]
public class CooldownEvent : TimelineEventBase, ITimelineEventRuntime
{
    [Header("冷却配置")]
    [Tooltip("冷却效果 (Duration 类型的 GameplayEffect)")]
    public GameplayEffect cooldownEffect;
    [Tooltip("冷却标签 (与 GameplayAbilitySO 中的 cooldownTag 对应)")]
    public GameplayTagSO cooldownTag;

    public override TimelineEventType Type => TimelineEventType.CooldownTrigger;

    public override string GetSummary()
    {
        string effectName = cooldownEffect != null ? cooldownEffect.name : "None";
        string tagName = cooldownTag != null ? cooldownTag.name : "None";
        return $"CD [{StartFrame}] {effectName} Tag:{tagName}";
    }

    public void OnStart(GameObject owner)
    {
        if (cooldownEffect == null) return;

        var ownerASC = owner.GetComponent<AbilitySystemComponent>();
        if (ownerASC == null)
        {
            Debug.LogWarning("CooldownEvent: Owner 缺少 AbilitySystemComponent");
            return;
        }

        ownerASC.ApplyGameplayEffect(cooldownEffect, ownerASC);
    }

    public void OnEnd(GameObject owner) { }

    public override TimelineEventBase Clone()
    {
        return new CooldownEvent
        {
            StartFrame = StartFrame,
            EndFrame = EndFrame,
            cooldownEffect = cooldownEffect,
            cooldownTag = cooldownTag
        };
    }
}
