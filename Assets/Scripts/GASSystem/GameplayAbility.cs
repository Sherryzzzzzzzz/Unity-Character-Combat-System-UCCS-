using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 技能冷却信息（统一普通 CD 和充能 CD 查询）
/// </summary>
public struct SkillCooldownInfo
{
    public bool IsOnCooldown;
    public float RemainingTime;
    public float TotalDuration;
    public int RemainingCharges;
    public int MaxCharges;
    public bool IsChargeBased;
}

public abstract class GameplayAbility
{
    protected AbilitySystemComponent OwnerASC;
    protected GameObject Owner;
    protected TagComponent TagComp;

    // ===== 能力元数据（对应 UE5） =====
    /// <summary>实例化策略</summary>
    public InstancingPolicy AbilityInstancingPolicy = InstancingPolicy.InstancedPerExecution;
    /// <summary>网络执行策略（单机默认LocalOnly）</summary>
    public NetExecutionPolicy AbilityNetExecutionPolicy = NetExecutionPolicy.LocalOnly;
    /// <summary>此能力的AssetTag（用于按Tag查找/取消/阻塞）</summary>
    public List<GameplayTagSO> AbilityTags = new();
    /// <summary>激活时取消拥有这些Tag的其他能力</summary>
    public List<GameplayTagSO> CancelAbilitiesWithTag = new();
    /// <summary>激活期间阻塞拥有这些Tag的能力</summary>
    public List<GameplayTagSO> BlockAbilitiesWithTag = new();
    /// <summary>激活期间授予Owner的Tag</summary>
    public List<GameplayTagSO> ActivationOwnedTags = new();
    /// <summary>来源对象</summary>
    public object SourceObject;
    /// <summary>能力等级</summary>
    public int AbilityLevel = 1;
    /// <summary>所属的Spec</summary>
    public GameplayAbilitySpec Spec;

    // ===== 当前激活信息 =====
    protected int CurrentSpecHandle;
    protected GameplayAbilityActorInfo CurrentActorInfo;

    [Header("Cooldown")]
    public float Cooldown = 0f;
    private float lastCastTime = -999f;

    // 标签驱动 CD
    protected GameplayEffect _cooldownEffect;
    protected GameplayTagSO _cooldownTag;

    // 充能 CD
    public int MaxCharges = 1;
    public float ChargeRecoveryTime = 0f;
    private int _remainingCharges;
    private float _chargeRecoveryTimer;

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
        _cooldownEffect = data.cooldownEffect;
        _cooldownTag = data.cooldownTag;
        MaxCharges = Mathf.Max(1, data.maxCharges);
        ChargeRecoveryTime = data.chargeRecoveryTime;
        _remainingCharges = MaxCharges;

        // 新 GAS 字段
        AbilityInstancingPolicy = data.abilityInstancingPolicy;
        AbilityTags = new List<GameplayTagSO>(data.abilityTags);
        CancelAbilitiesWithTag = new List<GameplayTagSO>(data.cancelAbilitiesWithTag);
        BlockAbilitiesWithTag = new List<GameplayTagSO>(data.blockAbilitiesWithTag);
        ActivationOwnedTags = new List<GameplayTagSO>(data.activationOwnedTags);

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

    /// <summary>
    /// 检查是否在冷却中。
    /// 优先使用标签驱动 CD，fallback 到旧时间戳逻辑。
    /// 充能模式下检查剩余充能数。
    /// </summary>
    public bool IsOnCooldown()
    {
        // 充能模式
        if (MaxCharges > 1)
            return _remainingCharges <= 0;

        // 标签驱动 CD
        if (_cooldownTag != null && TagComp != null)
            return TagComp.HasTag(_cooldownTag);

        // Fallback: 旧时间戳逻辑
        if (Cooldown > 0)
            return Time.time < lastCastTime + Cooldown;

        return false;
    }

    /// <summary>
    /// 获取 CD 剩余时间
    /// </summary>
    public float GetCooldownRemaining()
    {
        // 标签驱动 CD：查找 Owner 上的 CooldownEffect
        if (_cooldownTag != null && _cooldownEffect != null && OwnerASC != null)
        {
            // 通过 ASC 查找活跃的 CooldownEffect
            // 注意：这里简单返回 CooldownEffect 的 duration 减去已过时间
            // 实际精确值需要从 ActiveGameplayEffect.TimeRemaining 获取
        }

        // Fallback
        if (Cooldown > 0)
        {
            float remaining = (lastCastTime + Cooldown) - Time.time;
            return Mathf.Max(0f, remaining);
        }

        return 0f;
    }

    /// <summary>
    /// 获取统一的冷却信息
    /// </summary>
    public SkillCooldownInfo GetCooldownInfo()
    {
        var info = new SkillCooldownInfo();
        info.MaxCharges = MaxCharges;
        info.IsChargeBased = MaxCharges > 1;
        info.RemainingCharges = _remainingCharges;

        if (info.IsChargeBased)
        {
            info.IsOnCooldown = _remainingCharges <= 0;
            info.RemainingTime = _chargeRecoveryTimer > 0f ? GetChargeRecoveryDuration() - _chargeRecoveryTimer : 0f;
            info.TotalDuration = GetChargeRecoveryDuration();
        }
        else
        {
            info.IsOnCooldown = IsOnCooldown();
            info.RemainingTime = GetCooldownRemaining();
            info.TotalDuration = _cooldownEffect != null ? _cooldownEffect.duration : Cooldown;
        }

        return info;
    }

    /// <summary>
    /// 每帧更新充能恢复（需要外部调用或通过 ASC Tick）
    /// </summary>
    public void TickChargeRecovery(float deltaTime)
    {
        if (MaxCharges <= 1) return;
        if (_remainingCharges >= MaxCharges) return;

        _chargeRecoveryTimer += deltaTime;
        float recoveryDuration = GetChargeRecoveryDuration();

        while (_chargeRecoveryTimer >= recoveryDuration && _remainingCharges < MaxCharges)
        {
            _chargeRecoveryTimer -= recoveryDuration;
            _remainingCharges++;
        }

        if (_remainingCharges >= MaxCharges)
            _chargeRecoveryTimer = 0f;
    }

    private float GetChargeRecoveryDuration()
    {
        if (ChargeRecoveryTime > 0f) return ChargeRecoveryTime;
        if (_cooldownEffect != null) return _cooldownEffect.duration;
        return Cooldown > 0f ? Cooldown : 1f;
    }

    /// <summary>
    /// 检查资源是否足够支付 Cost Effect
    /// 若无 costEffect 则返回 true
    /// </summary>
    public bool CheckCost()
    {
        if (_costEffect == null) return true;
        if (OwnerASC == null || OwnerASC.Attributes == null) return false;

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
                if (attrValue.BaseValue + mod.value < 0f) return false;
            }
        }

        return true;
    }

    /// <summary>
    /// 原子提交：扣除 Cost、启动冷却、施加 CooldownEffect
    /// </summary>
    public bool CommitAbility()
    {
        float prevLastCastTime = lastCastTime;

        try
        {
            if (_costEffect != null && OwnerASC != null)
            {
                int handle = OwnerASC.ApplyGameplayEffect(_costEffect, OwnerASC);
                if (handle == -1)
                    return false;
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"CommitAbility: ApplyGameplayEffect 抛出异常: {e}");
            lastCastTime = prevLastCastTime;
            return false;
        }

        // 启动冷却
        lastCastTime = Time.time;

        // 充能模式：消耗一个充能
        if (MaxCharges > 1)
        {
            _remainingCharges = Mathf.Max(0, _remainingCharges - 1);
        }

        // 标签驱动 CD：施加 CooldownEffect
        if (_cooldownEffect != null && _cooldownTag != null && OwnerASC != null && MaxCharges <= 1)
        {
            var cdSpec = new CooldownEffectSpec(_cooldownEffect, OwnerASC, _cooldownTag);
            OwnerASC.ApplyEffectSpec(cdSpec);
        }

        return true;
    }

    public bool TryActivate()
    {
        // 1. 冷却检查
        if (IsOnCooldown())
            return false;

        // 2. 标签检查
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
            if (ActivationRequiredTags != null && ActivationRequiredTags.Count > 0)
                return false;
        }

        // 3. Cost 检查
        if (!CheckCost())
            return false;

        // 4. 激活
        if (!ActivateInternal()) return false;
        return true;
    }

    private bool ActivateInternal()
    {
        float prevLastCastTime = lastCastTime;

        if (!CommitAbility())
            return false;

        if (TagComp != null)
        {
            foreach (var tag in GrantedTags)
                TagComp.AddTag(tag);
        }

        if (OwnerASC != null)
            OwnerASC.SetCurrentAbility(this);

        try
        {
            Activate();
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"ActivateInternal: Activate() 抛出异常，回滚能力激活: {e}");
            try
            {
                End();
            }
            catch (System.Exception inner)
            {
                Debug.LogWarning($"ActivateInternal: End() 在回滚时也抛出异常: {inner}");
            }

            lastCastTime = prevLastCastTime;
            return false;
        }

        return true;
    }

    protected abstract void Activate();

    // ================================================================
    // 协议化能力生命周期（对应 UE5 GameplayAbility）
    // ================================================================

    /// <summary>
    /// 检查是否可以激活（不修改状态）
    /// 对应 UE5: UGameplayAbility::CanActivateAbility
    /// </summary>
    public bool CanActivateAbility(GameplayAbilityActorInfo actorInfo)
    {
        if (IsOnCooldown()) return false;
        if (!CheckCost()) return false;
        if (TagComp != null)
        {
            foreach (var tag in ActivationRequiredTags)
                if (!TagComp.HasTagOrChild(tag)) return false;
            foreach (var tag in ActivationBlockedTags)
                if (TagComp.HasTagOrChild(tag)) return false;
        }
        else
        {
            if (ActivationRequiredTags != null && ActivationRequiredTags.Count > 0) return false;
        }
        return true;
    }

    /// <summary>
    /// 预激活钩子（在 ActivateAbility 之前调用）
    /// 对应 UE5: 隐式的 PreActivate 逻辑
    /// </summary>
    public virtual void PreActivate(int specHandle, GameplayAbilityActorInfo actorInfo)
    {
        CurrentSpecHandle = specHandle;
        CurrentActorInfo = actorInfo;
    }

    /// <summary>
    /// 实际的激活入口（接受 Handle 和 ActorInfo）
    /// 对应 UE5: UGameplayAbility::ActivateAbility
    /// </summary>
    public virtual void ActivateAbility(int specHandle, GameplayAbilityActorInfo actorInfo)
    {
        PreActivate(specHandle, actorInfo);

        if (!CommitAbility(specHandle, actorInfo, out _))
        {
            EndAbility(specHandle, actorInfo, new GameplayAbilityActivationInfo(), true);
            return;
        }

        // 授予Tag
        if (TagComp != null)
        {
            foreach (var tag in GrantedTags)
                TagComp.AddTag(tag);
        }

        if (OwnerASC != null)
            OwnerASC.SetCurrentAbility(this);

        Activate();
    }

    /// <summary>
    /// 提交能力（Cost + Cooldown）
    /// 对应 UE5: UGameplayAbility::CommitAbility
    /// </summary>
    public bool CommitAbility(int specHandle, GameplayAbilityActorInfo actorInfo, out List<GameplayTagSO> relevantTags)
    {
        relevantTags = null;
        if (!CommitCost(specHandle, actorInfo)) return false;
        if (!CommitCooldown(specHandle, actorInfo, false, out relevantTags)) return false;

        if (OwnerASC != null)
            OwnerASC.OnAbilityCommitted?.Invoke(this);

        return true;
    }

    /// <summary>
    /// 只提交消耗
    /// 对应 UE5: UGameplayAbility::CommitAbilityCost
    /// </summary>
    public bool CommitCost(int specHandle, GameplayAbilityActorInfo actorInfo)
    {
        if (_costEffect == null) return true;
        if (OwnerASC == null || OwnerASC.Attributes == null) return false;

        var attrs = OwnerASC.Attributes;
        if (_costEffect.damage > 0f && attrs.Health < _costEffect.damage) return false;

        foreach (var mod in _costEffect.modifiers)
        {
            var attrValue = attrs.GetAttributeValue(mod.attribute);
            if (attrValue != null && attrValue.BaseValue + mod.value < 0f) return false;
        }

        try
        {
            int handle = OwnerASC.ApplyGameplayEffect(_costEffect, OwnerASC);
            return handle != -1;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"CommitCost: threw {e}");
            return false;
        }
    }

    /// <summary>
    /// 只提交冷却
    /// 对应 UE5: UGameplayAbility::CommitAbilityCooldown
    /// </summary>
    public bool CommitCooldown(int specHandle, GameplayAbilityActorInfo actorInfo, bool forceCooldown, out List<GameplayTagSO> relevantTags)
    {
        relevantTags = null;
        lastCastTime = Time.time;

        if (MaxCharges > 1)
        {
            _remainingCharges = Mathf.Max(0, _remainingCharges - 1);
        }

        if (_cooldownEffect != null && _cooldownTag != null && OwnerASC != null && MaxCharges <= 1)
        {
            var cdSpec = new CooldownEffectSpec(_cooldownEffect, OwnerASC, _cooldownTag);
            OwnerASC.ApplyEffectSpec(cdSpec);
        }

        return true;
    }

    /// <summary>
    /// 结束能力
    /// 对应 UE5: UGameplayAbility::EndAbility
    /// </summary>
    public virtual void EndAbility(int specHandle, GameplayAbilityActorInfo actorInfo,
        GameplayAbilityActivationInfo activationInfo, bool bWasCancelled)
    {
        // 移除授予Tag
        if (TagComp != null)
        {
            foreach (var tag in GrantedTags)
                TagComp.RemoveTag(tag);
            foreach (var tag in ActivationOwnedTags)
                TagComp.RemoveTag(tag);
        }

        // 减少Spec的ActiveCount
        if (Spec != null) Spec.ActiveCount--;

        if (OwnerASC != null)
        {
            OwnerASC.ClearCurrentAbility(this);
            OwnerASC.OnAbilityEnded?.Invoke(this);
        }
    }

    /// <summary>
    /// 判断此能力是否应响应某个 GameplayEvent
    /// 对应 UE5: UGameplayAbility::ShouldAbilityRespondToEvent
    /// </summary>
    public virtual bool ShouldAbilityRespondToEvent(GameplayTagSO eventTag, GameplayEventData payload)
    {
        if (eventTag == null) return false;
        // 检查 AbilityTags 中是否有匹配的 tag（通过层级匹配）
        foreach (var abilityTag in AbilityTags)
            if (abilityTag == eventTag || abilityTag.HasChild(eventTag) || eventTag.HasChild(abilityTag))
                return true;
        return false;
    }

    /// <summary>
    /// 设置当前角色信息
    /// </summary>
    public void SetCurrentActorInfo(int specHandle, GameplayAbilityActorInfo actorInfo)
    {
        CurrentSpecHandle = specHandle;
        CurrentActorInfo = actorInfo;
    }

    /// <summary>
    /// 获取能力等级
    /// 对应 UE5: UGameplayAbility::GetAbilityLevel
    /// </summary>
    public int GetAbilityLevel() => AbilityLevel;

    /// <summary>
    /// 获取来源对象
    /// 对应 UE5: UGameplayAbility::GetCurrentSourceObject
    /// </summary>
    public object GetCurrentSourceObject() => SourceObject;

    // 向后兼容的旧版 End（被新代码中调用）
    public virtual void End()
    {
        EndAbility(CurrentSpecHandle, CurrentActorInfo,
            new GameplayAbilityActivationInfo(), false);
    }
}
