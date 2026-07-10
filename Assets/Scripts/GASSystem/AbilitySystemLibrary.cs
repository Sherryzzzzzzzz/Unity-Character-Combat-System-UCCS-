using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// GAS 便捷函数库 — 对应 UE5 UAbilitySystemBlueprintLibrary
/// 提供常用的查询和操作函数，简化 GAS API 调用
/// </summary>
public static class AbilitySystemLibrary
{
    #region Attribute Helpers

    /// <summary>获取属性的当前值</summary>
    public static float GetAttributeValue(AbilitySystemComponent asc, GameplayAttribute attr)
    {
        if (asc?.Attributes == null) return 0f;
        var value = asc.Attributes.GetAttributeValue(attr);
        return value != null ? value.GetCurrentValue() : 0f;
    }

    /// <summary>检查属性值是否满足条件</summary>
    public static bool CheckAttributeCondition(AbilitySystemComponent asc, GameplayAttribute attr, float threshold, System.Func<float, float, bool> condition)
    {
        float val = GetAttributeValue(asc, attr);
        return condition(val, threshold);
    }

    #endregion

    #region Tag Helpers

    /// <summary>检查 ASC 是否拥有指定 Tag</summary>
    public static bool HasTag(AbilitySystemComponent asc, GameplayTagSO tag)
        => asc?.GetTagCount(tag) > 0;

    /// <summary>检查 ASC 是否拥有指定 Tag 或其子 Tag</summary>
    public static bool HasTagOrChild(AbilitySystemComponent asc, GameplayTagSO tag)
    {
        var tc = asc?.GetComponent<TagComponent>();
        return tc != null && tc.HasTagOrChild(tag);
    }

    /// <summary>获取 Tag 的引用计数</summary>
    public static int GetTagCount(AbilitySystemComponent asc, GameplayTagSO tag)
        => asc?.GetTagCount(tag) ?? 0;

    #endregion

    #region Effect Helpers

    /// <summary>简单施加效果（一行调用）</summary>
    public static int ApplyEffect(AbilitySystemComponent source, AbilitySystemComponent target, GameplayEffect effect, float level = 1f)
    {
        if (source == null || target == null || effect == null) return -1;
        var context = source.MakeEffectContext();
        context.Instigator = source.gameObject;
        var spec = source.MakeOutgoingSpec(effect, level, context);
        return source.ApplyGameplayEffectSpecToTarget(spec, target);
    }

    /// <summary>施加效果到自身</summary>
    public static int ApplyEffectToSelf(AbilitySystemComponent asc, GameplayEffect effect, float level = 1f)
    {
        if (asc == null || effect == null) return -1;
        var context = asc.MakeEffectContext();
        var spec = asc.MakeOutgoingSpec(effect, level, context);
        return asc.ApplyGameplayEffectSpecToSelf(spec);
    }

    /// <summary>从发起者向目标施加伤害效果</summary>
    public static int ApplyDamage(AbilitySystemComponent attacker, AbilitySystemComponent target,
        GameplayEffect damageEffect, Vector3 hitPoint, Vector3 hitNormal, float setByCallerDamage = 0f)
    {
        if (attacker == null || target == null || damageEffect == null) return -1;
        var context = attacker.MakeEffectContext();
        context.Instigator = attacker.gameObject;
        context.Origin = hitPoint;
        context.Normal = hitNormal;
        context.HitResult = new HitResultInfo { Location = hitPoint, Normal = hitNormal };

        var spec = attacker.MakeOutgoingSpec(damageEffect, 1f, context);
        return attacker.ApplyGameplayEffectSpecToTarget(spec, target);
    }

    #endregion

    #region Ability Helpers

    /// <summary>通过 Tag 触发所有匹配能力</summary>
    public static int ActivateAbilitiesByTag(AbilitySystemComponent asc, GameplayTagSO tag)
        => asc?.TryActivateAbilitiesByTag(tag) ?? 0;

    /// <summary>发送 GameplayEvent</summary>
    public static int SendGameplayEvent(AbilitySystemComponent asc, GameplayTagSO eventTag,
        GameObject instigator = null, GameObject target = null, float magnitude = 0f)
    {
        if (asc == null) return 0;
        var payload = new GameplayEventData(eventTag, instigator, target)
        {
            EventMagnitude = magnitude
        };
        return asc.HandleGameplayEvent(eventTag, payload);
    }

    /// <summary>取消拥有指定 Tag 的所有能力</summary>
    public static void CancelAbilitiesWithTag(AbilitySystemComponent asc, GameplayTagSO tag)
        => asc?.CancelAbilitiesWithTag(tag);

    #endregion

    #region Queries

    /// <summary>检查 ASC 是否有活跃的 Ability</summary>
    public static bool HasActiveAbility(AbilitySystemComponent asc)
    {
        if (asc == null) return false;
        foreach (var spec in asc.ActivatableAbilities)
            if (spec.ActiveCount > 0) return true;
        return false;
    }

    /// <summary>获取活跃 GE 的数量</summary>
    public static int GetActiveEffectCount(AbilitySystemComponent asc)
        => asc?.GetNumActiveGameplayEffects() ?? 0;

    /// <summary>获取属性修改器汇总值</summary>
    public static float GetModifiedAttributeValue(AbilitySystemComponent asc, GameplayAttribute attr)
    {
        if (asc?.Attributes == null) return 0f;
        var val = asc.Attributes.GetAttributeValue(attr);
        return val?.GetCurrentValue() ?? 0f;
    }

    #endregion
}

/// <summary>
/// GAS 测试辅助类 — 用于快速验证 GAS 系统功能
/// </summary>
public static class GASDebug
{
    /// <summary>打印 ASC 的所有活跃 Ability</summary>
    public static void LogActiveAbilities(AbilitySystemComponent asc)
    {
        if (asc == null) { Debug.Log("[GAS Debug] ASC is null"); return; }
        Debug.Log($"[GAS Debug] === {asc.gameObject.name} Active Abilities ===");
        foreach (var spec in asc.ActivatableAbilities)
        {
            if (spec.ActiveCount > 0)
                Debug.Log($"  Handle={spec.Handle} ActiveCount={spec.ActiveCount} Ability={spec.Ability?.GetType().Name}");
        }
    }

    /// <summary>打印 ASC 的所有 Tag</summary>
    public static void LogAllTags(AbilitySystemComponent asc)
    {
        if (asc == null) { Debug.Log("[GAS Debug] ASC is null"); return; }
        Debug.Log($"[GAS Debug] === {asc.gameObject.name} Tags ===");
        Debug.Log($"  TagCount={asc.GetTagCount(null)}");
    }

    /// <summary>打印 ASC 的活跃 GE</summary>
    public static void LogActiveEffects(AbilitySystemComponent asc)
    {
        if (asc == null) { Debug.Log("[GAS Debug] ASC is null"); return; }
        Debug.Log($"[GAS Debug] === {asc.gameObject.name} Active Effects: {asc.GetNumActiveGameplayEffects()} ===");
    }

    /// <summary>打印 Ability 的完整 CD 信息</summary>
    public static void LogCooldownInfo(GameplayAbility ability)
    {
        if (ability == null) return;
        var info = ability.GetCooldownInfo();
        Debug.Log($"[GAS Debug] CD: OnCooldown={info.IsOnCooldown} Remaining={info.RemainingTime:F1}s/" +
                  $"{info.TotalDuration:F1}s Charges={info.RemainingCharges}/{info.MaxCharges} ChargeBased={info.IsChargeBased}");
    }
}
