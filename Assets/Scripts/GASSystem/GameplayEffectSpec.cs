using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// GameplayEffect 的运行时施加规格对象
/// 持有效果数据引用、施加者属性快照和动态 Magnitude 配置
/// </summary>
public class GameplayEffectSpec
{
    /// <summary>
    /// 源 GameplayEffect ScriptableObject
    /// </summary>
    public GameplayEffect EffectData { get; }

    /// <summary>
    /// 施加者 ASC 引用（可能为 null，如施加者已被销毁）
    /// </summary>
    public AbilitySystemComponent InstigatorASC { get; }

    /// <summary>
    /// 施加时捕获的施加者属性快照
    /// </summary>
    public Dictionary<GameplayAttribute, float> CapturedAttackerAttributes { get; } =
        new Dictionary<GameplayAttribute, float>();

    /// <summary>
    /// 动态 Magnitude 覆盖（modifierIndex → value）
    /// </summary>
    private readonly Dictionary<int, float> _magnitudeOverrides = new Dictionary<int, float>();

    public GameplayEffectSpec(GameplayEffect effectData, AbilitySystemComponent instigator)
    {
        EffectData = effectData;
        InstigatorASC = instigator;

        // 自动捕获施加者的所有属性聚合值
        if (instigator != null && instigator.Attributes != null)
        {
            var attrs = instigator.Attributes;
            CapturedAttackerAttributes[GameplayAttribute.AttackPower] = attrs.AttackPower;
            CapturedAttackerAttributes[GameplayAttribute.Defense] = attrs.Defense;
            CapturedAttackerAttributes[GameplayAttribute.HealthMax] = attrs.HealthMax;
            CapturedAttackerAttributes[GameplayAttribute.PoiseMax] = attrs.PoiseMax;
            CapturedAttackerAttributes[GameplayAttribute.Health] = attrs.Health;
            CapturedAttackerAttributes[GameplayAttribute.Poise] = attrs.Poise;
        }
    }

    /// <summary>
    /// 覆盖指定修改器条目的 Magnitude 值（优先级最高）
    /// </summary>
    public void SetMagnitudeOverride(int modifierIndex, float value)
    {
        _magnitudeOverrides[modifierIndex] = value;
    }

    /// <summary>
    /// 解析指定修改器条目的 Magnitude 值
    /// 优先级：Override → Custom → AttributeBased → Static
    /// </summary>
    public float GetMagnitude(int modifierIndex)
    {
        // 1. Override 最高优先
        if (_magnitudeOverrides.TryGetValue(modifierIndex, out float overrideValue))
            return overrideValue;

        if (EffectData == null || modifierIndex < 0 || modifierIndex >= EffectData.modifiers.Count)
            return 0f;

        var mod = EffectData.modifiers[modifierIndex];

        try
        {
            switch (mod.magnitudeCalculation)
            {
                case MagnitudeCalculation.Custom:
                    // 2. Custom 接口计算
                    if (mod.customCalculation is IMagnitudeCalculation calc)
                        return SafeCalculateMagnitude(calc);
                    return mod.value; // fallback 到 Static

                case MagnitudeCalculation.AttributeBased:
                    // 3. AttributeBased 从快照/目标捕获（Attacker snapshot preserved）
                    if (mod.captureSource == CaptureSource.Attacker)
                    {
                        if (CapturedAttackerAttributes.TryGetValue(mod.captureAttribute, out float capturedValue))
                            return capturedValue;
                    }
                    // Target 来源在施加时从目标 ASC 实时获取（不走快照）
                    return mod.value; // fallback

                case MagnitudeCalculation.Static:
                default:
                    // 4. Static 使用固定值
                    return mod.value;
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"GetMagnitude: exception while calculating magnitude for modifier {modifierIndex} on {EffectData?.name}: {e}");
            return 0f;
        }
    }

    private float SafeCalculateMagnitude(IMagnitudeCalculation calc)
    {
        try
        {
            return calc.CalculateMagnitude(this);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"IMagnitudeCalculation threw exception: {e}");
            return 0f;
        }
    }

    /// <summary>
    /// 从目标 ASC 获取 AttributeBased(Target) 的属性值
    /// </summary>
    public float GetMagnitude(int modifierIndex, AbilitySystemComponent targetASC)
    {
        if (_magnitudeOverrides.TryGetValue(modifierIndex, out float overrideValue))
            return overrideValue;

        if (EffectData == null || modifierIndex < 0 || modifierIndex >= EffectData.modifiers.Count)
            return 0f;

        var mod = EffectData.modifiers[modifierIndex];

        if (mod.magnitudeCalculation == MagnitudeCalculation.AttributeBased
            && mod.captureSource == CaptureSource.Target
            && targetASC != null && targetASC.Attributes != null)
        {
            var targetAttrValue = targetASC.Attributes.GetAttributeValue(mod.captureAttribute);
            if (targetAttrValue != null)
                return targetAttrValue.GetCurrentValue();
        }

        // 其他情况委托给无目标版本
        return GetMagnitude(modifierIndex);
    }
}
