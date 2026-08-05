using System;
using System.Collections.Generic;

/// <summary>
/// GameplayEffect 的运行时实例，管理非即时效果的状态
/// </summary>
public class ActiveGameplayEffect : UCCS.IStackCountSource
{
    private static int _nextHandle = 1;

    /// <summary>
    /// 唯一标识句柄
    /// </summary>
    public int Handle { get; }

    public GameplayEffect EffectData { get; }
    public AbilitySystemComponent InstigatorASC { get; }

    /// <summary>
    /// 关联的 GameplayEffectSpec，持有属性快照
    /// </summary>
    public GameplayEffectSpec Spec { get; }

    public float TimeRemaining { get; private set; }
    public int CurrentStacks { get; private set; }

    /// <summary>UCCS.IStackCountSource 显式实现（供 AttributeModifier StackCount 感知）</summary>
    int UCCS.IStackCountSource.CurrentStacks => CurrentStacks;
    public bool IsExpired => EffectData.durationPolicy == DurationPolicy.Duration && TimeRemaining <= 0f;

    // 已注册到 AttributeSet 的修改器引用
    public List<AttributeModifier> RegisteredModifiers { get; } = new List<AttributeModifier>();

    private float _periodTimer;

    /// <summary>
    /// 周期 Tick 触发时的回调
    /// </summary>
    public Action OnPeriodicTick;

    public ActiveGameplayEffect(GameplayEffect effectData, AbilitySystemComponent instigator, GameplayEffectSpec spec = null)
    {
        Handle = _nextHandle++;
        EffectData = effectData;
        InstigatorASC = instigator;
        Spec = spec;
        TimeRemaining = effectData.duration;
        CurrentStacks = 1;
        _periodTimer = 0f;
    }

    /// <summary>
    /// 每帧更新
    /// </summary>
    public void Tick(float deltaTime)
    {
        if (EffectData.durationPolicy == DurationPolicy.Duration)
        {
            TimeRemaining -= deltaTime;
        }

        if (EffectData.period > 0f)
        {
            _periodTimer += deltaTime;
            while (_periodTimer >= EffectData.period)
            {
                _periodTimer -= EffectData.period;
                try { OnPeriodicTick?.Invoke(); }
                catch (System.Exception e) { UnityEngine.Debug.LogWarning($"ActiveGameplayEffect.Tick: OnPeriodicTick threw: {e}"); }
            }
        }
    }

    /// <summary>
    /// 刷新持续时间（重置为最大值）
    /// </summary>
    public void Refresh()
    {
        TimeRemaining = EffectData.duration;
    }

    /// <summary>
    /// 延长持续时间（在剩余时间上追加）
    /// </summary>
    public void Extend(float additionalTime)
    {
        TimeRemaining += additionalTime;
    }

    /// <summary>
    /// 增加层数（不超过 maxStacks），同时刷新时间
    /// </summary>
    public void AddStack()
    {
        if (CurrentStacks < EffectData.maxStacks)
        {
            CurrentStacks++;
        }
        Refresh();
    }

    /// <summary>
    /// 减少一层堆叠
    /// </summary>
    public void RemoveStack()
    {
        if (CurrentStacks > 0)
        {
            CurrentStacks--;
        }
    }
}
