using System;
using UnityEngine;

/// <summary>
/// 属性修改器类型
/// </summary>
public enum ModifierType
{
    Additive,       // 加法修改器
    Multiplicative  // 乘法修改器
}

/// <summary>
/// Magnitude 计算模式
/// </summary>
public enum MagnitudeCalculation
{
    Static,         // 使用 SO 上的固定值
    AttributeBased, // 从施加者/目标捕获指定属性值
    Custom          // 通过 IMagnitudeCalculation 接口自定义
}

/// <summary>
/// 属性捕获来源
/// </summary>
public enum CaptureSource
{
    Attacker, // 从施加者捕获
    Target    // 从目标捕获
}

/// <summary>
/// 属性修改器，用于动态修改 AttributeValue
/// </summary>
[Serializable]
public struct AttributeModifier
{
    public ModifierType type;
    public float value;

    public AttributeModifier(ModifierType type, float value)
    {
        this.type = type;
        this.value = value;
    }
}

/// <summary>
/// 可被 GameplayEffect 修改的属性类型
/// </summary>
public enum GameplayAttribute
{
    AttackPower,
    Defense,
    HealthMax,
    PoiseMax,
    Health,
    Poise
}

/// <summary>
/// GameplayEffect 中的属性修改器配置条目
/// </summary>
[Serializable]
public struct EffectAttributeModifier
{
    public GameplayAttribute attribute;
    public ModifierType modifierType;
    public float value;

    [Header("Magnitude 计算")]
    public MagnitudeCalculation magnitudeCalculation;
    [Tooltip("AttributeBased 模式下捕获的属性")]
    public GameplayAttribute captureAttribute;
    [Tooltip("AttributeBased 模式下的捕获来源")]
    public CaptureSource captureSource;
    [Tooltip("Custom 模式下的自定义计算实现（需实现 IMagnitudeCalculation）")]
    public ScriptableObject customCalculation;
}
