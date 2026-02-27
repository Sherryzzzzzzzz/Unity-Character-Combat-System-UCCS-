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

[CreateAssetMenu(menuName = "GAS-like/GameplayEffect")]
public class GameplayEffect : ScriptableObject
{
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

    [Header("标签")]
    [Tooltip("效果激活时授予目标的标签")]
    public List<GameplayTagSO> grantedTags = new List<GameplayTagSO>();
    [Tooltip("施加条件：目标必须拥有的标签")]
    public List<GameplayTagSO> applicationRequiredTags = new List<GameplayTagSO>();
    [Tooltip("施加条件：目标不能拥有的标签")]
    public List<GameplayTagSO> applicationBlockedTags = new List<GameplayTagSO>();

    [Header("属性修改器")]
    public List<EffectAttributeModifier> modifiers = new List<EffectAttributeModifier>();

    [Header("GameplayCue")]
    [Tooltip("效果施加/移除时触发的 Cue 标签（用于 VFX/SFX 解耦）")]
    public GameplayTagSO cueTag;
}
