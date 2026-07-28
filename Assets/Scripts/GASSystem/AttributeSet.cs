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

public class AttributeSet : MonoBehaviour, UCCS.IAttributeProvider
{
    [Header("Health")]
    public float Health
    {
        get => GetAttributeCurrentValue(GameplayAttribute.Health);
        set
        {
            if (_attributes.TryGetValue(GameplayAttribute.Health, out var v))
                v.BaseValue = value;
        }
    }

    [Header("Poise (韧性)")]
    public float Poise
    {
        get => GetAttributeCurrentValue(GameplayAttribute.Poise);
        set
        {
            if (_attributes.TryGetValue(GameplayAttribute.Poise, out var v))
                v.BaseValue = value;
        }
    }

    [Header("Stamina (体力)")]
    public float Stamina
    {
        get => GetAttributeCurrentValue(GameplayAttribute.Stamina);
        set
        {
            if (_attributes.TryGetValue(GameplayAttribute.Stamina, out var v))
                v.BaseValue = value;
        }
    }

    [Tooltip("每秒恢复多少体力")]
    public float StaminaRecoverRate = 25f;

    [Tooltip("消耗体力后多久开始恢复")]
    public float StaminaRecoverDelay = 0.5f;

    [Tooltip("多久未受击开始恢复")]
    public float PoiseRecoverDelay = 3f;

    [Tooltip("每秒恢复多少韧性")]
    public float PoiseRecoverRate = 10f;

    [Header("属性配置")]
    [Tooltip("Inspector 中配置的属性初始值列表")]
    [SerializeField] private List<AttributeInitEntry> _attributeInitList = new List<AttributeInitEntry>
    {
        new AttributeInitEntry { attribute = GameplayAttribute.AttackPower, baseValue = 15f },
        new AttributeInitEntry { attribute = GameplayAttribute.Defense, baseValue = 15f },
        new AttributeInitEntry { attribute = GameplayAttribute.HealthMax, baseValue = 100f },
        new AttributeInitEntry { attribute = GameplayAttribute.PoiseMax, baseValue = 60f },
        new AttributeInitEntry { attribute = GameplayAttribute.StaminaMax, baseValue = 100f },
    };

    [Header("Runtime Values")]
    [SerializeField] private float _health;
    [SerializeField] private float _healthMax;
    [SerializeField] private float _poise;
    [SerializeField] private float _poiseMax;
    [SerializeField] private float _stamina;
    [SerializeField] private float _staminaMax;

    /// <summary>
    /// 运行时属性字典
    /// </summary>
    private readonly Dictionary<GameplayAttribute, AttributeValue> _attributes = new Dictionary<GameplayAttribute, AttributeValue>();

    // 兼容性属性访问器
    public float AttackPower => GetAttributeCurrentValue(GameplayAttribute.AttackPower);
    public float Defense => GetAttributeCurrentValue(GameplayAttribute.Defense);
    public float HealthMax => GetAttributeCurrentValue(GameplayAttribute.HealthMax);
    public float PoiseMax => GetAttributeCurrentValue(GameplayAttribute.PoiseMax);
    public float StaminaMax => GetAttributeCurrentValue(GameplayAttribute.StaminaMax);

    private float _lastStaminaUseTime;
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

    /// <summary>玩家属性全局引用（由 PlayerModel 的 AttributeSet 在 Awake 中设置）</summary>
    public static AttributeSet PlayerAttributes { get; private set; }

    private void Awake()
    {
        // 如果这个 AttributeSet 属于玩家，设全局引用
        if (CompareTag("Player") || GetComponent<PlayerModel>() != null ||
            GetComponentInParent<PlayerModel>() != null || GetComponentInChildren<PlayerModel>() != null)
            PlayerAttributes = this;

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
        EnsureAttribute(GameplayAttribute.AttackPower, 15f);
        EnsureAttribute(GameplayAttribute.Defense, 15f);
        EnsureAttribute(GameplayAttribute.HealthMax, 100f);
        EnsureAttribute(GameplayAttribute.PoiseMax, 60f);
        EnsureAttribute(GameplayAttribute.StaminaMax, 100f);
        // Health/Poise/Stamina 也注册到字典中以支持 GAS Modifier
        EnsureAttribute(GameplayAttribute.Health, 0f);
        EnsureAttribute(GameplayAttribute.Poise, 0f);
        EnsureAttribute(GameplayAttribute.Stamina, 0f);

        // 注册变更回调和钳制
        foreach (var kvp in _attributes)
        {
            var attr = kvp.Key;
            var attrValue = kvp.Value;

            // 转发到通用事件
            attrValue.OnValueChanged = (oldVal, newVal) =>
            {
                SyncRuntimeValues();
                OnAttributeChanged?.Invoke(attr, oldVal, newVal);
            };

            // 属性钳制：确保聚合值不低于 0
            attrValue.OnPreAttributeChange = (val) => Mathf.Max(0f, val);
        }
    }

    private void Start()
    {
        Health = HealthMax;
        Poise = PoiseMax;
        Stamina = StaminaMax;
        SyncRuntimeValues();
    }

    private void Update()
    {
        HandleStaminaRecovery();
        HandlePoiseRecovery();
    }

    // ========================
    // Health
    // ========================

    public void ModifyHealth(float value)
    {
        if (_isDead) return;

        if (!_attributes.TryGetValue(GameplayAttribute.Health, out var healthAttr)) return;

        float newBaseValue = Mathf.Clamp(healthAttr.BaseValue + value, 0, HealthMax);
        healthAttr.BaseValue = newBaseValue;
        // BaseValue setter chains to OnValueChanged → OnAttributeChanged(GameplayAttribute.Health, ...)

        if (Health <= 0)
        {
            _isDead = true;
            OnDeath?.Invoke();
        }
    }

    // ========================
    // Stamina
    // ========================

    public bool TryConsumeStamina(float value)
    {
        if (_isDead) return false;
        if (value <= 0f) return true;
        if (Stamina < value) return false;

        ModifyStamina(-value);
        return true;
    }

    public void ModifyStamina(float value)
    {
        if (_isDead) return;
        if (!_attributes.TryGetValue(GameplayAttribute.Stamina, out var staminaAttr)) return;

        float newBaseValue = Mathf.Clamp(staminaAttr.BaseValue + value, 0, StaminaMax);
        staminaAttr.BaseValue = newBaseValue;

        if (value < 0f)
            _lastStaminaUseTime = Time.time;
    }

    private void HandleStaminaRecovery()
    {
        if (_isDead) return;
        if (Stamina >= StaminaMax) return;
        if (Time.time - _lastStaminaUseTime < StaminaRecoverDelay) return;
        if (!_attributes.TryGetValue(GameplayAttribute.Stamina, out var staminaAttr)) return;

        float newBaseValue = Mathf.Clamp(staminaAttr.BaseValue + StaminaRecoverRate * Time.deltaTime, 0, StaminaMax);
        staminaAttr.BaseValue = newBaseValue;
    }

    // ========================
    // Poise
    // ========================

    public void ModifyPoise(float value)
    {
        if (_isDead) return;

        if (!_attributes.TryGetValue(GameplayAttribute.Poise, out var poiseAttr)) return;

        float newBaseValue = Mathf.Clamp(poiseAttr.BaseValue + value, 0, PoiseMax);
        poiseAttr.BaseValue = newBaseValue;
        // BaseValue setter chains to OnValueChanged → OnAttributeChanged(GameplayAttribute.Poise, ...)

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
            if (!_attributes.TryGetValue(GameplayAttribute.Poise, out var poiseAttr)) return;
            float newBaseValue = Mathf.Clamp(poiseAttr.BaseValue + PoiseRecoverRate * Time.deltaTime, 0, PoiseMax);
            poiseAttr.BaseValue = newBaseValue;
            // BaseValue setter chains to OnValueChanged → OnAttributeChanged → UI updates
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
        {
            SyncRuntimeValues();
            OnAttributeChanged?.Invoke(attribute, oldVal, newVal);
        };
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

    private void SyncRuntimeValues()
    {
        _health = Health;
        _healthMax = HealthMax;
        _poise = Poise;
        _poiseMax = PoiseMax;
        _stamina = Stamina;
        _staminaMax = StaminaMax;
    }

    private void EnsureAttribute(GameplayAttribute attribute, float defaultBaseValue)
    {
        if (!_attributes.ContainsKey(attribute))
            _attributes[attribute] = new AttributeValue(defaultBaseValue);
    }
}
