using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 属性修改应用方式
/// </summary>
public enum AttributeModificationType
{
    ModifyBaseValue,    // 直接修改 BaseValue（Instant 效果）
    Additive,           // 添加 Additive modifier（Duration 效果）
    Multiplicative,     // 添加 Multiplicative modifier
    Override            // 添加 Override modifier
}

/// <summary>
/// 执行计算产生的单条属性修改
/// </summary>
[System.Serializable]
public struct AttributeModification
{
    public GameplayAttribute Attribute;
    public float Magnitude;
    public AttributeModificationType Type;
}

/// <summary>
/// 执行计算输出
/// </summary>
[System.Serializable]
public struct EffectExecutionOutput
{
    public List<AttributeModification> Modifications;
}

/// <summary>
/// GameplayEffect 执行计算抽象基类。
/// 通过 ScriptableObject 资产配置，替代硬编码伤害公式。
/// </summary>
public abstract class GameplayEffectExecutionCalculation : ScriptableObject
{
    /// <summary>
    /// 执行自定义计算，将结果写入 output。
    /// </summary>
    /// <param name="instigatorASC">施加者 ASC</param>
    /// <param name="targetASC">目标 ASC</param>
    /// <param name="spec">效果 Spec（含属性快照）</param>
    /// <param name="output">计算输出</param>
    public abstract void Execute(
        AbilitySystemComponent instigatorASC,
        AbilitySystemComponent targetASC,
        GameplayEffectSpec spec,
        ref EffectExecutionOutput output);
}
