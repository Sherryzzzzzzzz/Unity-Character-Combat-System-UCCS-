using UnityEngine;
using System.Collections.Generic;

// ============================================================
// GameplayEventData.cs — 对应 UE5 FGameplayEventData
// GameplayEvent 的负载数据，是跨Ability通信的核心机制
// ============================================================

/// <summary>
/// GameplayEvent 负载数据 — 对应 UE5 FGameplayEventData
///
/// 当 HandleGameplayEvent 被调用时，此结构体携带所有上下文信息传递给监听该事件的能力。
/// 这是 UE GAS 的核心通信机制：Ability A 触发事件 → Ability B/C/D 响应。
///
/// 使用场景:
/// - 受击事件: "Event.Damage.Taken" + Payload(Instigator=攻击者, Magnitude=伤害值)
/// - 连击事件: "Event.Combo.Window" + Payload(OptionalObject=武器)
/// - 状态事件: "Event.Status.Changed" + Payload(Target=目标)
/// </summary>
[System.Serializable]
public class GameplayEventData
{
    /// <summary>事件标签（哪个事件被触发）</summary>
    public GameplayTagSO EventTag;

    /// <summary>事件发起者（Instigator）</summary>
    public GameObject Instigator;

    /// <summary>事件目标</summary>
    public GameObject Target;

    /// <summary>可选对象1（如武器、技能特效）</summary>
    public Object OptionalObject;

    /// <summary>可选对象2</summary>
    public Object OptionalObject2;

    /// <summary>效果上下文（包含更详细的信息）</summary>
    public GameplayEffectContext Context;

    /// <summary>事件关联的数值（如伤害量、治疗量）</summary>
    public float EventMagnitude;

    /// <summary>目标数据的原始标签容器（暂未使用，预留）</summary>
    public List<GameplayTagSO> TargetTags = new List<GameplayTagSO>();

    /// <summary>目标上的ASC列表（暂未使用，预留）</summary>
    public List<AbilitySystemComponent> TargetASCs = new List<AbilitySystemComponent>();

    /// <summary>创建一个空的GameplayEventData</summary>
    public GameplayEventData() { }

    /// <summary>快速创建带有Tag的GameplayEventData</summary>
    public GameplayEventData(GameplayTagSO eventTag)
    {
        EventTag = eventTag;
    }

    /// <summary>快速创建带有Tag、Instigator、Target的GameplayEventData</summary>
    public GameplayEventData(GameplayTagSO eventTag, GameObject instigator, GameObject target)
    {
        EventTag = eventTag;
        Instigator = instigator;
        Target = target;
    }
}

/// <summary>
/// GameplayTag查询 — 对应 UE5 FGameplayTagQuery
///
/// 支持复杂的布尔Tag匹配逻辑:
/// - MatchAny: 目标拥有Tag列表中的任意一个
/// - MatchAll: 目标拥有Tag列表中的所有
/// - NoMatch: 目标不拥有Tag列表中的任意一个
///
/// 用于 GE Application Requirements、Ability Activation Checks 等
/// </summary>
[System.Serializable]
public class GameplayTagQuery
{
    /// <summary>必须全部匹配的Tag</summary>
    public List<GameplayTagSO> MatchAllTags = new List<GameplayTagSO>();

    /// <summary>匹配任意一个即可的Tag</summary>
    public List<GameplayTagSO> MatchAnyTags = new List<GameplayTagSO>();

    /// <summary>不能拥有的Tag（如果拥有则匹配失败）</summary>
    public List<GameplayTagSO> NoMatchTags = new List<GameplayTagSO>();

    /// <summary>
    /// 对指定的 TagComponent 执行查询
    /// </summary>
    public bool Matches(TagComponent tagComp)
    {
        if (tagComp == null)
            return MatchAllTags.Count == 0 && MatchAnyTags.Count == 0;

        // 检查 NoMatch 条件
        foreach (var tag in NoMatchTags)
            if (tagComp.HasTag(tag)) return false;

        // 检查 MatchAll 条件
        foreach (var tag in MatchAllTags)
            if (!tagComp.HasTagOrChild(tag)) return false;

        // 检查 MatchAny 条件
        if (MatchAnyTags.Count > 0)
        {
            bool anyMatch = false;
            foreach (var tag in MatchAnyTags)
                if (tagComp.HasTagOrChild(tag)) { anyMatch = true; break; }
            if (!anyMatch) return false;
        }

        return true;
    }

    /// <summary>是否是空查询（无条件通过）</summary>
    public bool IsEmpty =>
        MatchAllTags.Count == 0 && MatchAnyTags.Count == 0 && NoMatchTags.Count == 0;

    /// <summary>创建一个无条件的空查询</summary>
    public static GameplayTagQuery MakeEmpty() => new GameplayTagQuery();

    /// <summary>创建一个 MatchAny 查询</summary>
    public static GameplayTagQuery MakeQueryMatchAny(params GameplayTagSO[] tags)
    {
        var q = new GameplayTagQuery();
        q.MatchAnyTags.AddRange(tags);
        return q;
    }

    /// <summary>创建一个同时包含 MatchAll/NoMatch 的完整查询</summary>
    public static GameplayTagQuery MakeQuery(
        GameplayTagSO[] matchAll = null,
        GameplayTagSO[] matchAny = null,
        GameplayTagSO[] noMatch = null)
    {
        var q = new GameplayTagQuery();
        if (matchAll != null) q.MatchAllTags.AddRange(matchAll);
        if (matchAny != null) q.MatchAnyTags.AddRange(matchAny);
        if (noMatch != null) q.NoMatchTags.AddRange(noMatch);
        return q;
    }
}
