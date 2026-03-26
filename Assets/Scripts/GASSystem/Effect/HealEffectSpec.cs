using UnityEngine;

/// <summary>
/// 治疗效果 Spec
/// </summary>
public class HealEffectSpec : GameplayEffectSpec
{
    public HealEffectSpec(GameplayEffect effectData, AbilitySystemComponent instigator)
        : base(effectData, instigator) { }
}
