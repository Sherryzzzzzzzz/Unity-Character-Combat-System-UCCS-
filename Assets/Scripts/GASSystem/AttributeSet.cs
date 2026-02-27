using UnityEngine;
using System;

public class AttributeSet : MonoBehaviour
{
    [Header("Health")]
    public float Health;
    [SerializeField] private AttributeValue _healthMax = new AttributeValue(100f);

    [Header("Poise (韧性)")]
    public float Poise;
    [SerializeField] private AttributeValue _poiseMax = new AttributeValue(50f);

    [Tooltip("多久未受击开始恢复")]
    public float PoiseRecoverDelay = 3f;

    [Tooltip("每秒恢复多少韧性")]
    public float PoiseRecoverRate = 10f;

    [Header("Combat")]
    [SerializeField] private AttributeValue _attackPower = new AttributeValue(10f);
    [SerializeField] private AttributeValue _defense = new AttributeValue(5f);

    // 兼容性属性访问器，返回聚合值
    public float AttackPower => _attackPower.GetCurrentValue();
    public float Defense => _defense.GetCurrentValue();
    public float HealthMax => _healthMax.GetCurrentValue();
    public float PoiseMax => _poiseMax.GetCurrentValue();

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
        // 为每个 AttributeValue 注册变更回调，转发到通用事件
        _attackPower.OnValueChanged = (oldVal, newVal) =>
            OnAttributeChanged?.Invoke(GameplayAttribute.AttackPower, oldVal, newVal);
        _defense.OnValueChanged = (oldVal, newVal) =>
            OnAttributeChanged?.Invoke(GameplayAttribute.Defense, oldVal, newVal);
        _healthMax.OnValueChanged = (oldVal, newVal) =>
            OnAttributeChanged?.Invoke(GameplayAttribute.HealthMax, oldVal, newVal);
        _poiseMax.OnValueChanged = (oldVal, newVal) =>
            OnAttributeChanged?.Invoke(GameplayAttribute.PoiseMax, oldVal, newVal);
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
    // 属性修改器访问
    // ========================

    /// <summary>
    /// 根据枚举获取对应的 AttributeValue，供 ASC 注册/移除修改器
    /// </summary>
    public AttributeValue GetAttributeValue(GameplayAttribute attribute)
    {
        switch (attribute)
        {
            case GameplayAttribute.AttackPower: return _attackPower;
            case GameplayAttribute.Defense: return _defense;
            case GameplayAttribute.HealthMax: return _healthMax;
            case GameplayAttribute.PoiseMax: return _poiseMax;
            default: return null;
        }
    }
}
