using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Aggregator 聚合模式
/// </summary>
public enum AggregatorMode
{
    /// <summary>默认：所有修改器累加 — (Base + ΣAdd) × (1 + ΣMult)，Override 取最后一个</summary>
    Default,
    /// <summary>取最大正向 Additive 修改器</summary>
    MostPositive,
    /// <summary>取最大负向 Additive 修改器</summary>
    MostNegative
}

/// <summary>
/// 带 Aggregator 支持的属性值容器。
/// 支持 AggregatorMode、StackCount 感知、Dirty 标记按需重算、Pre/Post 变更回调。
/// </summary>
[Serializable]
public class AttributeValue
{
    [SerializeField] private float _baseValue;

    [NonSerialized] private readonly List<AttributeModifier> _modifiers = new List<AttributeModifier>();

    [NonSerialized] private float _cachedCurrentValue;
    [NonSerialized] private bool _dirty = true;

    /// <summary>
    /// Aggregator 聚合模式（默认 Default 保持原有行为）
    /// </summary>
    [NonSerialized] public AggregatorMode Mode = AggregatorMode.Default;

    /// <summary>
    /// 值变更回调（旧值, 新值）
    /// </summary>
    [NonSerialized] public Action<float, float> OnValueChanged;

    /// <summary>
    /// 属性变更前钳制回调。用于钳制新值（如 Health 不低于 0）。
    /// 返回钳制后的值。
    /// </summary>
    [NonSerialized] public Func<float, float> OnPreAttributeChange;

    public float BaseValue
    {
        get => _baseValue;
        set
        {
            if (Mathf.Approximately(_baseValue, value)) return;
            float oldValue = GetCurrentValue();
            _baseValue = value;
            _dirty = true;
            float newValue = GetCurrentValue();
            if (!Mathf.Approximately(oldValue, newValue))
                OnValueChanged?.Invoke(oldValue, newValue);
        }
    }

    public AttributeValue() { }

    public AttributeValue(float baseValue)
    {
        _baseValue = baseValue;
    }

    /// <summary>
    /// 获取当前聚合值。使用 Dirty 标记按需重算。
    /// </summary>
    public float GetCurrentValue()
    {
        if (_dirty)
        {
            _cachedCurrentValue = Recalculate();
            _dirty = false;
        }
        return _cachedCurrentValue;
    }

    /// <summary>
    /// 添加一个修改器
    /// </summary>
    public void AddModifier(AttributeModifier modifier)
    {
        float oldValue = GetCurrentValue();
        _modifiers.Add(modifier);
        _dirty = true;
        NotifyIfChanged(oldValue);
    }

    /// <summary>
    /// 移除一个修改器
    /// </summary>
    public void RemoveModifier(AttributeModifier modifier)
    {
        try
        {
            float oldValue = GetCurrentValue();
            _modifiers.Remove(modifier);
            _dirty = true;
            NotifyIfChanged(oldValue);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"AttributeValue.RemoveModifier: exception while removing modifier: {e}");
        }
    }

    /// <summary>
    /// 获取当前所有活跃修改器（只读访问）
    /// </summary>
    public IReadOnlyList<AttributeModifier> GetModifiers()
    {
        return _modifiers;
    }

    /// <summary>
    /// 标记为需要重算（当外部 StackCount 变化时调用）
    /// </summary>
    public void SetDirty()
    {
        float oldValue = GetCurrentValue();
        _dirty = true;
        NotifyIfChanged(oldValue);
    }

    /// <summary>
    /// 核心聚合计算
    /// </summary>
    private float Recalculate()
    {
        float result;

        switch (Mode)
        {
            case AggregatorMode.MostPositive:
                result = RecalculateMostPositive();
                break;
            case AggregatorMode.MostNegative:
                result = RecalculateMostNegative();
                break;
            case AggregatorMode.Default:
            default:
                result = RecalculateDefault();
                break;
        }

        // 钳制回调
        if (OnPreAttributeChange != null)
            result = OnPreAttributeChange(result);

        return result;
    }

    /// <summary>
    /// Default 模式：(BaseValue + ΣAdditive) × (1 + ΣMultiplicative)，Override 取最后一个
    /// StackCount 感知：modifier 的有效幅度 = value × source.StackCount（若有 source）
    /// </summary>
    private float RecalculateDefault()
    {
        float additive = 0f;
        float multiplicative = 0f;
        float? overrideValue = null;

        for (int i = 0; i < _modifiers.Count; i++)
        {
            var mod = _modifiers[i];
            float effectiveValue = GetEffectiveValue(mod);

            switch (mod.type)
            {
                case ModifierType.Additive:
                    additive += effectiveValue;
                    break;
                case ModifierType.Multiplicative:
                    multiplicative += effectiveValue;
                    break;
                case ModifierType.Override:
                    overrideValue = effectiveValue;
                    break;
            }
        }

        if (overrideValue.HasValue)
            return overrideValue.Value;

        return (_baseValue + additive) * (1f + multiplicative);
    }

    /// <summary>
    /// MostPositive 模式：只取最大正向 Additive modifier
    /// </summary>
    private float RecalculateMostPositive()
    {
        float bestAdditive = 0f;
        float multiplicative = 0f;
        float? overrideValue = null;
        bool hasAdditive = false;

        for (int i = 0; i < _modifiers.Count; i++)
        {
            var mod = _modifiers[i];
            float effectiveValue = GetEffectiveValue(mod);

            switch (mod.type)
            {
                case ModifierType.Additive:
                    if (!hasAdditive || effectiveValue > bestAdditive)
                    {
                        bestAdditive = effectiveValue;
                        hasAdditive = true;
                    }
                    break;
                case ModifierType.Multiplicative:
                    multiplicative += effectiveValue;
                    break;
                case ModifierType.Override:
                    overrideValue = effectiveValue;
                    break;
            }
        }

        if (overrideValue.HasValue)
            return overrideValue.Value;

        float additive = hasAdditive ? Mathf.Max(0f, bestAdditive) : 0f;
        return (_baseValue + additive) * (1f + multiplicative);
    }

    /// <summary>
    /// MostNegative 模式：只取最大负向 Additive modifier
    /// </summary>
    private float RecalculateMostNegative()
    {
        float bestAdditive = 0f;
        float multiplicative = 0f;
        float? overrideValue = null;
        bool hasAdditive = false;

        for (int i = 0; i < _modifiers.Count; i++)
        {
            var mod = _modifiers[i];
            float effectiveValue = GetEffectiveValue(mod);

            switch (mod.type)
            {
                case ModifierType.Additive:
                    if (!hasAdditive || effectiveValue < bestAdditive)
                    {
                        bestAdditive = effectiveValue;
                        hasAdditive = true;
                    }
                    break;
                case ModifierType.Multiplicative:
                    multiplicative += effectiveValue;
                    break;
                case ModifierType.Override:
                    overrideValue = effectiveValue;
                    break;
            }
        }

        if (overrideValue.HasValue)
            return overrideValue.Value;

        float additive = hasAdditive ? Mathf.Min(0f, bestAdditive) : 0f;
        return (_baseValue + additive) * (1f + multiplicative);
    }

    /// <summary>
    /// 获取修改器的有效值，考虑 StackCount
    /// </summary>
    private float GetEffectiveValue(AttributeModifier mod)
    {
        if (mod.Source != null)
            return mod.value * mod.Source.CurrentStacks;
        return mod.value;
    }

    private void NotifyIfChanged(float oldValue)
    {
        float newValue = GetCurrentValue();
        if (!Mathf.Approximately(oldValue, newValue))
            OnValueChanged?.Invoke(oldValue, newValue);
    }
}
