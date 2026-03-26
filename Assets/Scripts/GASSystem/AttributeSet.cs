using UnityEngine;
using System;
using System.Collections.Generic;

/// <summary>
/// Inspector 可配置的属性初始条目
/// </summary>
[Serializable]
public struct AttributeInitEntry
{
    public GameplayAttribute attribute;
    public float baseValue;
}

public class AttributeSet : MonoBehaviour
{
    [Header("Health")]
    public float Health;

    [Header("Poise (韧性)")]
    public float Poise;

    [Tooltip("多久未受击开始恢复")]
    public float PoiseRecoverDelay = 3f;

    [Tooltip("每秒恢复多少韧性")]
    public float PoiseRecoverRate = 10f;

    [Header("属性配置")]
    [Tooltip("Inspector 中配置的属性初始值列表")]
    [SerializeField] private List<AttributeInitEntry> _attributeInitList = new List<AttributeInitEntry>
    {
        new AttributeInitEntry { attribute = GameplayAttribute.AttackPower, baseValue = 10f },
        new AttributeInitEntry { attribute = GameplayAttribute.Defense, baseValue = 5f },
        new AttributeInitEntry { attribute = GameplayAttribute.HealthMax, baseValue = 100f },
        new AttributeInitEntry { attribute = GameplayAttribute.PoiseMax, baseValue = 50f },
    };

    /// <summary>
    /// 运行时属性字典
    /// </summary>
    private readonly Dictionary<GameplayAttribute, AttributeValue> _attributes = new Dictionary<GameplayAttribute, AttributeValue>();

    // 兼容性属性访问器
    public float AttackPower => GetAttributeCurrentValue(GameplayAttribute.AttackPower);
    public float Defense => GetAttributeCurrentValue(GameplayAttribute.Defense);
    public float HealthMax => GetAttributeCurrentValue(GameplayAttribute.HealthMax);
    public float PoiseMax => GetAttributeCurrentValue(GameplayAttribute.PoiseMax);

    private float _lastPoiseHitTime;
    private bool _isBroken;
    private bool _isDead;

    public bool IsDead => _isDead;
    public bool IsBroken => _isBroken;

    public event Action OnDeath;
    public event Action OnPoiseBreak;
    public event Action OnPoiseRecover;

    /// <summary>
    /// 通用属性变更事件（属性枚举, 旧值, 新值）
    /// </summary>
    public event Action<GameplayAttribute, float, float> OnAttributeChanged;

    private void Awake()
    {
        // 从 Inspector 配置列表初始化属性字典
        foreach (var entry in _attributeInitList)
        {
            if (!_attributes.ContainsKey(entry.attribute))
            {
                var attrValue = new AttributeValue(entry.baseValue);
                _attributes[entry.attribute] = attrValue;
            }
        }

        // 确保核心属性存在（即使 Inspector 列表为空也有默认值）
        EnsureAttribute(GameplayAttribute.AttackPower, 10f);
        EnsureAttribute(GameplayAttribute.Defense, 5f);
        EnsureAttribute(GameplayAttribute.HealthMax, 100f);
        EnsureAttribute(GameplayAttribute.PoiseMax, 50f);

        // 注册变更回调和钳制
        foreach (var kvp in _attributes)
        {
            var attr = kvp.Key;
            var attrValue = kvp.Value;

            // 转发到通用事件
            attrValue.OnValueChanged = (oldVal, newVal) =>
                OnAttributeChanged?.Invoke(attr, oldVal, newVal);

            // 属性钳制：确保聚合值不低于 0
            attrValue.OnPreAttributeChange = (val) => Mathf.Max(0f, val);
        }
    }

    private void Start()
    {
        Health = HealthMax;
        Poise = PoiseMax;
    }

    private void Update()
    {
        HandlePoiseRecovery();
    }

    // ========================
    // Health
    // ========================

    public void ModifyHealth(float value)
    {
        if (_isDead) return;

        float oldHealth = Health;
        Health = Mathf.Clamp(Health + value, 0, HealthMax);

        if (!Mathf.Approximately(oldHealth, Health))
            OnAttributeChanged?.Invoke(GameplayAttribute.Health, oldHealth, Health);

        if (Health <= 0)
        {
            _isDead = true;
            OnDeath?.Invoke();
        }
    }

    // ========================
    // Poise
    // ========================

    public void ModifyPoise(float value)
    {
        if (_isDead) return;

        float oldPoise = Poise;
        Poise = Mathf.Clamp(Poise + value, 0, PoiseMax);

        if (!Mathf.Approximately(oldPoise, Poise))
            OnAttributeChanged?.Invoke(GameplayAttribute.Poise, oldPoise, Poise);

        if (value < 0)
        {
            _lastPoiseHitTime = Time.time;
        }

        if (Poise <= 0 && !_isBroken)
        {
            _isBroken = true;
            OnPoiseBreak?.Invoke();
        }
    }

    private void HandlePoiseRecovery()
    {
        if (_isDead) return;
        if (_isBroken) return;

        if (Poise >= PoiseMax) return;

        if (Time.time - _lastPoiseHitTime >= PoiseRecoverDelay)
        {
            Poise += PoiseRecoverRate * Time.deltaTime;
            Poise = Mathf.Clamp(Poise, 0, PoiseMax);
        }
    }

    public void ResetPoise()
    {
        Poise = PoiseMax;
        _isBroken = false;
        OnPoiseRecover?.Invoke();
    }

    // ========================
    // 属性访问
    // ========================

    /// <summary>
    /// 根据枚举获取对应的 AttributeValue，供 ASC 注册/移除修改器。
    /// 支持动态注册的属性。
    /// </summary>
    public AttributeValue GetAttributeValue(GameplayAttribute attribute)
    {
        if (_attributes.TryGetValue(attribute, out var attrValue))
            return attrValue;
        return null;
    }

    /// <summary>
    /// 运行时动态注册一个新属性
    /// </summary>
    public AttributeValue RegisterAttribute(GameplayAttribute attribute, float baseValue)
    {
        if (_attributes.TryGetValue(attribute, out var existing))
            return existing;

        var attrValue = new AttributeValue(baseValue);
        attrValue.OnValueChanged = (oldVal, newVal) =>
            OnAttributeChanged?.Invoke(attribute, oldVal, newVal);
        attrValue.OnPreAttributeChange = (val) => Mathf.Max(0f, val);
        _attributes[attribute] = attrValue;
        return attrValue;
    }

    /// <summary>
    /// 检查是否已注册某个属性
    /// </summary>
    public bool HasAttribute(GameplayAttribute attribute)
    {
        return _attributes.ContainsKey(attribute);
    }

    // ========================
    // 内部辅助
    // ========================

    private float GetAttributeCurrentValue(GameplayAttribute attribute)
    {
        if (_attributes.TryGetValue(attribute, out var attrValue))
            return attrValue.GetCurrentValue();
        return 0f;
    }

    private void EnsureAttribute(GameplayAttribute attribute, float defaultBaseValue)
    {
        if (!_attributes.ContainsKey(attribute))
            _attributes[attribute] = new AttributeValue(defaultBaseValue);
    }
}
