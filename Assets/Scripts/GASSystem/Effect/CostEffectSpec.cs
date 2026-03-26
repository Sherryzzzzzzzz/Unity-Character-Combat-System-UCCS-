using UnityEngine;

/// <summary>
/// 消耗效果 Spec — 提供 CanAfford 静态检查，执行时扣除属性
/// </summary>
public class CostEffectSpec : GameplayEffectSpec
{
    public CostEffectSpec(GameplayEffect effectData, AbilitySystemComponent instigator)
        : base(effectData, instigator) { }

    /// <summary>
    /// 检查施加者是否负担得起此消耗
    /// </summary>
    public bool CanAfford(AttributeSet attrs)
    {
        if (attrs == null || EffectData == null) return false;

        if (EffectData.damage > 0f)
        {
            if (attrs.Health < EffectData.damage) return false;
        }

        foreach (var mod in EffectData.modifiers)
        {
            var attrValue = attrs.GetAttributeValue(mod.attribute);
            if (attrValue != null)
            {
                if (attrValue.BaseValue + mod.value < 0f) return false;
            }
        }

        return true;
    }
}
