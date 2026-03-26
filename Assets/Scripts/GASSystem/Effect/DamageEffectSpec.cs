using UnityEngine;

/// <summary>
/// 伤害效果 Spec — 使用 ExecutionCalculation 或默认公式计算伤害
/// </summary>
public class DamageEffectSpec : GameplayEffectSpec
{
    public DamageEffectSpec(GameplayEffect effectData, AbilitySystemComponent instigator)
        : base(effectData, instigator) { }
}
