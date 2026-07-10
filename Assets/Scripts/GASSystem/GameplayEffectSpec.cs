using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// GameplayEffect 的运行时施加规格对象。
/// 持有效果数据引用、施加者属性快照和动态 Magnitude 配置。
/// 子类可覆盖生命周期虚方法实现特定行为。
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
    /// 效果上下文 — 对应 UE5 FGameplayEffectSpec::Context
    /// </summary>
    public GameplayEffectContext Context { get; set; }

    /// <summary>
    /// 效果等级 — 对应 UE5 FGameplayEffectSpec::Level
    /// </summary>
    public float Level { get; set; } = 1f;

    /// <summary>
    /// SetByCaller Magnitudes (Tag → Value)
    /// </summary>
    private readonly Dictionary<GameplayTagSO, float> _setByCallerMagnitudes = new Dictionary<GameplayTagSO, float>();

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
    /// 设置 SetByCaller Magnitude
    /// 对应 UE5: FGameplayEffectSpec::SetSetByCallerMagnitude
    /// </summary>
    public void SetByCallerMagnitude(GameplayTagSO tag, float magnitude)
    {
        if (tag != null)
            _setByCallerMagnitudes[tag] = magnitude;
    }

    /// <summary>
    /// 获取 SetByCaller Magnitude
    /// 对应 UE5: FGameplayEffectSpec::GetSetByCallerMagnitude
    /// </summary>
    public float GetSetByCallerMagnitude(GameplayTagSO tag, float defaultIfNotFound = 0f)
    {
        if (tag != null && _setByCallerMagnitudes.TryGetValue(tag, out float val))
            return val;
        return defaultIfNotFound;
    }

    /// <summary>
    /// 解析指定修改器条目的 Magnitude 值
    /// 优先级：Override → Custom → AttributeBased → Static
    /// </summary>
    public float GetMagnitude(int modifierIndex)
    {
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
                    if (mod.customCalculation is IMagnitudeCalculation calc)
                        return SafeCalculateMagnitude(calc);
                    return mod.value;

                case MagnitudeCalculation.AttributeBased:
                    if (mod.captureSource == CaptureSource.Attacker)
                    {
                        if (CapturedAttackerAttributes.TryGetValue(mod.captureAttribute, out float capturedValue))
                            return capturedValue;
                    }
                    return mod.value;

                case MagnitudeCalculation.SetByCaller:
                    // 按 SetByCallerTag 查找值
                    if (mod.setByCallerTag != null)
                        return GetSetByCallerMagnitude(mod.setByCallerTag, mod.value);
                    return mod.value;

                case MagnitudeCalculation.Static:
                default:
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

        return GetMagnitude(modifierIndex);
    }

    // ========================
    // 生命周期虚方法（子类可覆盖）
    // ========================

    /// <summary>
    /// 效果首次施加时调用
    /// </summary>
    public virtual void OnInitialApply(AbilitySystemComponent targetASC) { }

    /// <summary>
    /// 周期 Tick 执行时调用
    /// </summary>
    public virtual void OnPeriodicExecute(AbilitySystemComponent targetASC) { }

    /// <summary>
    /// 效果正常到期完成时调用
    /// </summary>
    public virtual void OnComplete(AbilitySystemComponent targetASC) { }

    /// <summary>
    /// 效果刷新时调用（RefreshDuration 堆叠策略）
    /// </summary>
    public virtual void OnRefresh() { }

    /// <summary>
    /// 堆叠溢出时调用（达到 maxStacks 时再次施加）
    /// </summary>
    public virtual void OnOverflow(AbilitySystemComponent targetASC) { }
}
