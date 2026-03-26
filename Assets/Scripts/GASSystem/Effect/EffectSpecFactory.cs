/// <summary>
/// 根据 GameplayEffect.effectType 自动创建对应的 Spec 子类实例
/// </summary>
public static class EffectSpecFactory
{
    public static GameplayEffectSpec CreateSpec(GameplayEffect effectData, AbilitySystemComponent instigator)
    {
        if (effectData == null) return null;

        switch (effectData.effectType)
        {
            case EffectType.Damage:
                return new DamageEffectSpec(effectData, instigator);
            case EffectType.Heal:
                return new HealEffectSpec(effectData, instigator);
            case EffectType.Buff:
                return new BuffEffectSpec(effectData, instigator);
            case EffectType.Cooldown:
                // CooldownEffectSpec 需要额外的 cooldownTag 参数，此处返回基类
                // 使用 CooldownEffectSpec 应通过 GameplayAbility.CommitAbility 专门创建
                return new GameplayEffectSpec(effectData, instigator);
            case EffectType.Cost:
                return new CostEffectSpec(effectData, instigator);
            case EffectType.Custom:
            default:
                return new GameplayEffectSpec(effectData, instigator);
        }
    }
}
