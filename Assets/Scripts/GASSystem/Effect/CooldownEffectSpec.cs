using UnityEngine;

/// <summary>
/// 冷却效果 Spec — 继承 GameplayEffectSpec。
/// 施加时授予 CD 标签到 Owner，到期自动由 ActiveGameplayEffect 过期机制移除标签。
/// </summary>
public class CooldownEffectSpec : GameplayEffectSpec
{
    /// <summary>
    /// 冷却标签（施加时授予，移除时取消）
    /// </summary>
    public GameplayTagSO CooldownTag { get; }

    public CooldownEffectSpec(GameplayEffect effectData, AbilitySystemComponent instigator, GameplayTagSO cooldownTag)
        : base(effectData, instigator)
    {
        CooldownTag = cooldownTag;
    }

    /// <summary>
    /// 获取 CD 总时长
    /// </summary>
    public float GetTotalDuration()
    {
        return EffectData != null ? EffectData.duration : 0f;
    }
}
