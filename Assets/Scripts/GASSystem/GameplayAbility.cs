using System.Collections.Generic;
using UnityEngine;

public abstract class GameplayAbility
{
    protected AbilitySystemComponent OwnerASC;
    protected GameObject Owner;
    protected TagComponent TagComp;

    [Header("Cooldown")]
    public float Cooldown = 0f;
    private float lastCastTime = -999f;

    [Header("Tags")]
    public List<GameplayTagSO> ActivationRequiredTags = new();
    public List<GameplayTagSO> ActivationBlockedTags = new();
    public List<GameplayTagSO> GrantedTags = new();

    public bool CanBeInterrupted = true;

    /// <summary>
    /// 资源消耗效果（Instant 类型，扣除属性）
    /// </summary>
    protected GameplayEffect _costEffect;

    /// <summary>
    /// 从 ScriptableObject 数据资产初始化运行时参数
    /// </summary>
    public void InitializeFromData(GameplayAbilitySO data)
    {
        Cooldown = data.cooldown;
        ActivationRequiredTags = new List<GameplayTagSO>(data.activationRequiredTags);
        ActivationBlockedTags = new List<GameplayTagSO>(data.activationBlockedTags);
        GrantedTags = new List<GameplayTagSO>(data.grantedTags);
        CanBeInterrupted = data.canBeInterrupted;
        _costEffect = data.costEffect;

        if (this is DefaultGameplayAbility defaultAbility)
        {
            defaultAbility.SetEffectsToApply(data.effectsToApply);
        }
    }

    public void Initialize(AbilitySystemComponent asc)
    {
        OwnerASC = asc;
        Owner = asc.gameObject;
        TagComp = Owner.GetComponent<TagComponent>();
    }

    public bool IsOnCooldown()
    {
        return Time.time < lastCastTime + Cooldown;
    }

    /// <summary>
    /// 检查资源是否足够支付 Cost Effect
    /// 若无 costEffect 则返回 true
    /// </summary>
    public bool CheckCost()
    {
        if (_costEffect == null) return true;
        if (OwnerASC == null || OwnerASC.Attributes == null) return false;

        // 模拟检查：遍历 costEffect 的 modifiers，确认扣除后属性 >= 0
        // 同时检查 damage 字段（如果 costEffect 用 damage 扣血）
        var attrs = OwnerASC.Attributes;

        if (_costEffect.damage > 0f)
        {
            if (attrs.Health < _costEffect.damage) return false;
        }

        foreach (var mod in _costEffect.modifiers)
        {
            var attrValue = attrs.GetAttributeValue(mod.attribute);
            if (attrValue != null)
            {
                // 对于 Instant 扣除类 Cost，value 通常为负
                // 检查 baseValue + value >= 0
                if (attrValue.BaseValue + mod.value < 0f) return false;
            }
        }

        return true;
    }

    /// <summary>
    /// 原子提交：扣除 Cost 并启动冷却
    /// </summary>
    public bool CommitAbility()
    {
        // 扣除 Cost —— 如果 costEffect 存在则尝试施加并检查返回值
        try
        {
            if (_costEffect != null && OwnerASC != null)
            {
                int handle = OwnerASC.ApplyGameplayEffect(_costEffect, OwnerASC);
                if (handle == -1)
                    return false; // 施加被拒绝
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"CommitAbility: ApplyGameplayEffect 抛出异常: {e}");
            return false;
        }

        // 启动冷却
        lastCastTime = Time.time;
        return true;
    }

    public bool TryActivate()
    {
        // 1. 冷却检查
        if (IsOnCooldown())
            return false;

        // 2. 标签检查（防护 TagComp 为空的情况）
        if (TagComp != null)
        {
            foreach (var tag in ActivationRequiredTags)
                if (!TagComp.HasTag(tag))
                    return false;

            foreach (var tag in ActivationBlockedTags)
                if (TagComp.HasTag(tag))
                    return false;
        }
        else
        {
            // 没有 TagComponent：若存在必须要求的标签，则视为无法激活
            if (ActivationRequiredTags != null && ActivationRequiredTags.Count > 0)
                return false;
        }

        // 3. Cost 检查
        if (!CheckCost())
            return false;

        // 4. 激活
        if (!ActivateInternal()) return false;
        return true;
        return true;
    }

    private bool ActivateInternal()
    {
        // Commit: 扣除 Cost + 启动冷却
        if (!CommitAbility())
            return false;

        // 授予标签（检查 TagComp 是否存在）
        if (TagComp != null)
        {
            foreach (var tag in GrantedTags)
                TagComp.AddTag(tag);
        }

        // 设置当前能力（检查 OwnerASC）
        if (OwnerASC != null)
            OwnerASC.SetCurrentAbility(this);

        // 调用子类实现
        Activate();
        return true;
    }

    protected abstract void Activate();

    public virtual void End()
    {
        if (TagComp != null)
        {
            foreach (var tag in GrantedTags)
                TagComp.RemoveTag(tag);
        }

        if (OwnerASC != null)
            OwnerASC.ClearCurrentAbility(this);
    }
}
