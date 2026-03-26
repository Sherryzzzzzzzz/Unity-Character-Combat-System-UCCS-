using UnityEngine;

/// <summary>
/// Buff 效果 Spec — 管理属性修改器的添加和移除
/// </summary>
public class BuffEffectSpec : GameplayEffectSpec
{
    public BuffEffectSpec(GameplayEffect effectData, AbilitySystemComponent instigator)
        : base(effectData, instigator) { }
}
