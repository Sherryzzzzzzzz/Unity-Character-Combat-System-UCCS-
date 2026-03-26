using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 默认伤害计算 — 复制原有硬编码公式到可配置的 ScriptableObject
/// finalDamage = (damage * damageMultiplier) + attackPower - defense, clamped to >= 1
/// </summary>
[CreateAssetMenu(menuName = "GAS-like/ExecutionCalc/DefaultDamage")]
public class DefaultDamageCalculation : GameplayEffectExecutionCalculation
{
    public override void Execute(
        AbilitySystemComponent instigatorASC,
        AbilitySystemComponent targetASC,
        GameplayEffectSpec spec,
        ref EffectExecutionOutput output)
    {
        if (output.Modifications == null)
            output.Modifications = new List<AttributeModification>();

        var effect = spec.EffectData;
        if (effect == null) return;

        float attackPower = 0f;
        spec.CapturedAttackerAttributes.TryGetValue(GameplayAttribute.AttackPower, out attackPower);

        float defense = 0f;
        if (targetASC != null && targetASC.Attributes != null)
            defense = targetASC.Attributes.Defense;

        float finalDamage = (effect.damage * effect.damageMultiplier) + attackPower - defense;
        finalDamage = Mathf.Max(finalDamage, 1f);

        output.Modifications.Add(new AttributeModification
        {
            Attribute = GameplayAttribute.Health,
            Magnitude = -finalDamage,
            Type = AttributeModificationType.ModifyBaseValue
        });

        if (effect.poiseDamage > 0f)
        {
            output.Modifications.Add(new AttributeModification
            {
                Attribute = GameplayAttribute.Poise,
                Magnitude = -effect.poiseDamage,
                Type = AttributeModificationType.ModifyBaseValue
            });
        }
    }
}
