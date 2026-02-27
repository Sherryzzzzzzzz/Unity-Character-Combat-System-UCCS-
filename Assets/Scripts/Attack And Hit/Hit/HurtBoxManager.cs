using System;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class BodyPartMapping
{
    public GameplayTagSO tag;
    public Collider collider;
}

public class HurtBoxManager : MonoBehaviour
{
    public GameplayTagSO perfectParryTag;
    public GameplayTagSO normalParryTag;
    public GameplayTagSO guardingTag;
    public GameplayTagSO parrySuccessTag;

    private TagComponent _tagComponent;
    
    private HitReactionController hitReactionController;
    public List<BodyPartMapping> bodyPartMappings;

    public bool isHitting { get; private set; }

    public bool isInvincible = false;

    private AbilitySystemComponent _asc;

    private void Awake()
    {
        _tagComponent = GetComponent<TagComponent>();
        hitReactionController = GetComponent<HitReactionController>();
        _asc = GetComponent<AbilitySystemComponent>();
    }

    private void Update()
    {
        isHitting = hitReactionController.isHitting;
    }

    public void ProcessHit(AttackEvent hit, GameObject attacker, AbilitySystemComponent attackerASC = null)
    {
        if (isInvincible)
            return;

        // 弹反
        if (_tagComponent.HasTag(perfectParryTag) ||
            _tagComponent.HasTag(normalParryTag))
        {
            var attackerTagComponent = attacker.GetComponent<TagComponent>();
            if (attackerTagComponent != null && parrySuccessTag != null)
                attackerTagComponent.AddTransientTag(parrySuccessTag);
            return;
        }

        // 格挡
        if (_tagComponent.HasTag(guardingTag))
        {
            // 若配置了 Stagger 效果，则尝试对攻击者施加
            // 按约定：在攻击者的 ASC 上调用 ApplyGameplayEffect(effect, instigatorASC)
            if (hit.attackData != null && hit.attackData.staggerEffect != null)
            {
                var stagger = hit.attackData.staggerEffect;
                AbilitySystemComponent attackerAscLocal = attackerASC ?? attacker?.GetComponent<AbilitySystemComponent>();
                if (attackerAscLocal != null)
                {
                    try
                    {
                        int handle = attackerAscLocal.ApplyGameplayEffect(stagger, _asc);
                        // 若成功施加（handle > 0），中断攻击者当前能力
                        if (handle > 0)
                        {
                            attackerAscLocal.InterruptCurrentAbility();
                        }
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogWarning($"HurtBoxManager: applying stagger effect threw: {e}");
                    }
                }
            }

            return;
        }

        // 施加伤害
        if (_asc != null && attackerASC != null && hit.attackData != null && hit.attackData.effect != null)
            _asc.ApplyGameplayEffect(hit.attackData.effect, attackerASC);

        // 受击反应
        hitReactionController?.PlayHit(hit);
    }
    
    public void ActivateHurtBox(GameplayTagSO tag)
    {
        var mapping = bodyPartMappings.Find(x => x.tag == tag);
        if (mapping != null)
            mapping.collider.enabled = true;
    }

    public void DeactivateHurtBox(GameplayTagSO tag)
    {
        var mapping = bodyPartMappings.Find(x => x.tag == tag);
        if (mapping != null)
            mapping.collider.enabled = false;
    }

}