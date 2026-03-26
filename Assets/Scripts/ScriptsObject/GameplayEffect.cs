using System.Collections.Generic;
using UnityEngine;

public enum DurationPolicy
{
    Instant,   // 即时生效后不保留
    Duration,  // 持续指定时间后自动移除
    Infinite   // 永久生效直到手动移除
}

public enum StackingPolicy
{
    None,            // 不可叠加，忽略重复施加
    RefreshDuration, // 刷新剩余时间
    AddStacks        // 增加层数
}

/// <summary>
/// 效果类型枚举 — 驱动 EffectSpecFactory 创建对应子类
/// </summary>
public enum EffectType
{
    Custom,     // 默认，使用基类行为
    Damage,     // 伤害计算
    Heal,       // 治疗计算
    Buff,       // 属性修改器管理
    Cooldown,   // CD 标签管理
    Cost        // 消耗检查与扣除
}

/// <summary>
/// 堆叠溢出策略
/// </summary>
public enum OverflowPolicy
{
    RejectNew,              // 拒绝新施加
    TriggerOverflowEffect   // 触发溢出效果
}

/// <summary>
/// 堆叠到期策略
/// </summary>
public enum ExpirationPolicy
{
    RemoveAllStacks,  // 移除全部堆叠
    RemoveOneStack    // 移除一层堆叠
}

/// <summary>
/// 持续时间刷新策略
/// </summary>
public enum DurationRefreshPolicy
{
    ResetOnRefresh,   // 刷新时重置为最大持续时间
    ExtendOnRefresh   // 刷新时在剩余时间上追加
}

[CreateAssetMenu(menuName = "GAS-like/GameplayEffect")]
public class GameplayEffect : ScriptableObject
{
    [Header("效果类型")]
    [Tooltip("效果类型，驱动 EffectSpecFactory 创建对应运行时子类")]
    public EffectType effectType = EffectType.Custom;

    [Header("基础数值")]
    public float damage = 10f;
    public float poiseDamage = 20f;
    public float damageMultiplier = 1f;

    [Header("持续时间")]
    public DurationPolicy durationPolicy = DurationPolicy.Instant;
    [Tooltip("Duration 类型的持续时间（秒）")]
    public float duration = 0f;
    [Tooltip("周期 Tick 间隔（秒），0 表示不产生周期 Tick")]
    public float period = 0f;

    [Header("堆叠规则")]
    public StackingPolicy stackingPolicy = StackingPolicy.None;
    public int maxStacks = 1;

    [Header("高级堆叠")]
    [Tooltip("堆叠满时的溢出策略")]
    public OverflowPolicy overflowPolicy = OverflowPolicy.RejectNew;
    [Tooltip("到期时的堆叠移除策略")]
    public ExpirationPolicy expirationPolicy = ExpirationPolicy.RemoveAllStacks;
    [Tooltip("刷新时的持续时间策略")]
    public DurationRefreshPolicy refreshPolicy = DurationRefreshPolicy.ResetOnRefresh;
    [Tooltip("溢出时触发的效果")]
    public GameplayEffect overflowEffect;

    [Header("标签")]
    [Tooltip("效果激活时授予目标的标签")]
    public List<GameplayTagSO> grantedTags = new List<GameplayTagSO>();
    [Tooltip("施加条件：目标必须拥有的标签")]
    public List<GameplayTagSO> applicationRequiredTags = new List<GameplayTagSO>();
    [Tooltip("施加条件：目标不能拥有的标签")]
    public List<GameplayTagSO> applicationBlockedTags = new List<GameplayTagSO>();

    [Header("属性修改器")]
    public List<EffectAttributeModifier> modifiers = new List<EffectAttributeModifier>();

    [Header("执行计算")]
    [Tooltip("自定义执行计算（优先于硬编码伤害公式）")]
    public GameplayEffectExecutionCalculation executionCalculation;

    [Header("技能关联")]
    [Tooltip("技能结束时是否自动移除此效果")]
    public bool cancelOnAbilityEnd = false;

    [Header("GameplayCue")]
    [Tooltip("效果施加/移除时触发的 Cue 标签（用于 VFX/SFX 解耦）")]
    public GameplayTagSO cueTag;
}
