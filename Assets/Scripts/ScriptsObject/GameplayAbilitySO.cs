using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "GAS-like/GameplayAbility")]
public class GameplayAbilitySO : ScriptableObject
{
    [Header("基本信息")]
    public string abilityName = "NewAbility";

    [Header("冷却")]
    public float cooldown = 0f;
    [Tooltip("标签驱动 CD：激活时施加的冷却效果（DurationPolicy 应为 Duration）")]
    public GameplayEffect cooldownEffect;
    [Tooltip("标签驱动 CD：冷却期间授予的标签")]
    public GameplayTagSO cooldownTag;
    [Tooltip("充能 CD：最大充能数（1 = 普通 CD）")]
    public int maxCharges = 1;
    [Tooltip("充能 CD：每次充能恢复时间（0 = 使用 cooldownEffect.duration）")]
    public float chargeRecoveryTime = 0f;

    [Header("标签 (对应 UE5 Ability Tags)")]
    [Tooltip("此能力的AssetTag（用于按Tag查找/取消/阻塞）")]
    public List<GameplayTagSO> abilityTags = new List<GameplayTagSO>();
    [Tooltip("激活时要求目标拥有的标签")]
    public List<GameplayTagSO> activationRequiredTags = new List<GameplayTagSO>();
    [Tooltip("激活时目标不能拥有的标签")]
    public List<GameplayTagSO> activationBlockedTags = new List<GameplayTagSO>();
    [Tooltip("激活时授予的标签")]
    public List<GameplayTagSO> grantedTags = new List<GameplayTagSO>();
    [Tooltip("激活时取消拥有这些Tag的其他Ability")]
    public List<GameplayTagSO> cancelAbilitiesWithTag = new List<GameplayTagSO>();
    [Tooltip("激活期间阻塞拥有这些Tag的Ability")]
    public List<GameplayTagSO> blockAbilitiesWithTag = new List<GameplayTagSO>();
    [Tooltip("激活期间授予Owner的Tag（结束时会自动移除）")]
    public List<GameplayTagSO> activationOwnedTags = new List<GameplayTagSO>();

    [Header("实例化")]
    [Tooltip("实例化策略")]
    public InstancingPolicy abilityInstancingPolicy = InstancingPolicy.InstancedPerExecution;

    [Header("行为")]
    public bool canBeInterrupted = true;

    [Header("资源消耗")]
    [Tooltip("能力激活时的资源消耗效果（Instant 类型，用于扣除 Health/Stamina 等）")]
    public GameplayEffect costEffect;

    [Header("关联效果")]
    [Tooltip("能力激活时施加的 GameplayEffect 列表")]
    public List<GameplayEffect> effectsToApply = new List<GameplayEffect>();

    /// <summary>
    /// 创建运行时能力实例并用此数据资产初始化
    /// 子类可重写以返回自定义 GameplayAbility 派生类
    /// </summary>
    public virtual GameplayAbility CreateRuntimeAbility()
    {
        var ability = new DefaultGameplayAbility();
        ability.InitializeFromData(this);
        return ability;
    }
}

/// <summary>
/// 默认的 GameplayAbility 实现，通过 SO 数据驱动
/// </summary>
public class DefaultGameplayAbility : GameplayAbility
{
    private List<GameplayEffect> _effectsToApply;

    protected override void Activate()
    {
        // 施加关联效果
        if (_effectsToApply != null && OwnerASC != null)
        {
            foreach (var effect in _effectsToApply)
            {
                if (effect != null)
                    {
                        try
                        {
                            OwnerASC.ApplyGameplayEffect(effect, OwnerASC);
                        }
                        catch (System.Exception e)
                        {
                            Debug.LogWarning($"DefaultGameplayAbility.Activate: ApplyGameplayEffect 抛出异常: {e}");
                        }
                    }
            }
        }
    }

    public void SetEffectsToApply(List<GameplayEffect> effects)
    {
        _effectsToApply = effects;
    }
}
