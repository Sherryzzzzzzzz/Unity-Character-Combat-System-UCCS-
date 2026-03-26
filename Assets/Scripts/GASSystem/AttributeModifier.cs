using System;
using UnityEngine;

/// <summary>
/// 属性修改器类型
/// </summary>
public enum ModifierType
{
    Additive,       // 加法修改器
    Multiplicative, // 乘法修改器
    Override        // 覆盖修改器（取最后一个或按 AggregatorMode 选择）
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
/// 属性修改器，用于动态修改 AttributeValue。
/// 增强版：支持 Source 引用（ActiveGameplayEffect）和 Override 类型。
/// </summary>
[Serializable]
public struct AttributeModifier
{
    public ModifierType type;
    public float value;

    /// <summary>
    /// 修改器来源的 ActiveGameplayEffect 引用（用于 StackCount 感知）。
    /// 对于 Instant 效果或手动创建的修改器可为 null。
    /// </summary>
    [NonSerialized]
    public ActiveGameplayEffect Source;

    public AttributeModifier(ModifierType type, float value)
    {
        this.type = type;
        this.value = value;
        this.Source = null;
    }

    public AttributeModifier(ModifierType type, float value, ActiveGameplayEffect source)
    {
        this.type = type;
        this.value = value;
        this.Source = source;
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
