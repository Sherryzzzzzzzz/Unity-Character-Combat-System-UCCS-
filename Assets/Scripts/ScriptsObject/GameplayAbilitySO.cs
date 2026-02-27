using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "GAS-like/GameplayAbility")]
public class GameplayAbilitySO : ScriptableObject
{
    [Header("基本信息")]
    public string abilityName = "NewAbility";

    [Header("冷却")]
    public float cooldown = 0f;

    [Header("标签")]
    [Tooltip("激活时要求目标拥有的标签")]
    public List<GameplayTagSO> activationRequiredTags = new List<GameplayTagSO>();
    [Tooltip("激活时目标不能拥有的标签")]
    public List<GameplayTagSO> activationBlockedTags = new List<GameplayTagSO>();
    [Tooltip("激活时授予的标签")]
    public List<GameplayTagSO> grantedTags = new List<GameplayTagSO>();

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
