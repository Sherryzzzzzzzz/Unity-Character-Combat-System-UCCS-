using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 带修改器支持的属性值容器
/// 聚合公式: CurrentValue = (BaseValue + ΣAdditive) × (1 + ΣMultiplicative)
/// </summary>
[Serializable]
public class AttributeValue
{
    [SerializeField] private float _baseValue;

    [NonSerialized] private readonly List<AttributeModifier> _modifiers = new List<AttributeModifier>();

    /// <summary>
    /// 值变更回调（旧值, 新值）
    /// </summary>
    [NonSerialized] public Action<float, float> OnValueChanged;

    public float BaseValue
    {
        get => _baseValue;
        set
        {
            float oldValue = GetCurrentValue();
            _baseValue = value;
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

    public float GetCurrentValue()
    {
        float additive = 0f;
        float multiplicative = 0f;

        for (int i = 0; i < _modifiers.Count; i++)
        {
            var mod = _modifiers[i];
            if (mod.type == ModifierType.Additive)
                additive += mod.value;
            else
                multiplicative += mod.value;
        }

        return (_baseValue + additive) * (1f + multiplicative);
    }

    public void AddModifier(AttributeModifier modifier)
    {
        float oldValue = GetCurrentValue();
        _modifiers.Add(modifier);
        float newValue = GetCurrentValue();
        if (!Mathf.Approximately(oldValue, newValue))
            OnValueChanged?.Invoke(oldValue, newValue);
    }

    public void RemoveModifier(AttributeModifier modifier)
    {
        try
        {
            float oldValue = GetCurrentValue();
            _modifiers.Remove(modifier);
            float newValue = GetCurrentValue();
            if (!Mathf.Approximately(oldValue, newValue))
                OnValueChanged?.Invoke(oldValue, newValue);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"AttributeValue.RemoveModifier: exception while removing modifier: {e}");
        }
    }
}
