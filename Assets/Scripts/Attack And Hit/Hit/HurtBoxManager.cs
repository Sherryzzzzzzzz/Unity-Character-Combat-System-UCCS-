using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Animancer;
using Cinemachine;

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
    public GameplayTagSO perfectDodgeTag;

    [Header("格挡配置")]
    [Tooltip("格挡伤害减免比例 (0-1)，0.8 = 减免80%伤害")]
    public float blockDamageReduction = 0.8f;
    [Tooltip("格挡时消耗的Poise基础值")]
    public float blockPoiseCostBase = 10f;
    [Tooltip("格挡破防后施加给防御者的硬直效果")]
    public GameplayEffect guardBreakEffect;
    [Tooltip("格挡破防后授予的标签（用于触发硬直动画）")]
    public GameplayTagSO guardBreakTag;

    [Header("格挡反应")]
    [Tooltip("格挡成功时播放的动画（受击层 Layer 2）")]
    public ClipTransition blockReactionAnimation;
    [Tooltip("格挡火花 VFX 预制体")]
    public GameObject blockSparksVFX;
    [Tooltip("格挡音效")]
    public AudioClip blockSound;
    [Tooltip("格挡时相机震动幅度")]
    public float blockCameraShake = 0.3f;

    [Header("弹反 VFX")]
    [Tooltip("弹反成功时生成的 VFX 预制体")]
    public GameObject parrySuccessVFX;

    private TagComponent _tagComponent;

    private HitReactionController hitReactionController;
    private AnimancerComponent _animancer;
    private CinemachineImpulseSource _impulseSource;
    private AudioSource _audioSource;
    public List<BodyPartMapping> bodyPartMappings;

    public bool isHitting { get; private set; }

    public bool isInvincible = false;

    private AbilitySystemComponent _asc;
    private AttributeSet _attributes;

    private void Awake()
    {
        _tagComponent = GetComponent<TagComponent>();
        hitReactionController = GetComponent<HitReactionController>();
        _asc = GetComponent<AbilitySystemComponent>();
        _attributes = GetComponent<AttributeSet>();
        _animancer = GetComponent<AnimancerComponent>();
        _impulseSource = GetComponent<CinemachineImpulseSource>();
        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null)
            _audioSource = gameObject.AddComponent<AudioSource>();
    }

    private void Update()
    {
        isHitting = hitReactionController.isHitting;
    }

    /// <summary>
    /// 强制重置受击状态，清除整条链路（HitReactionController + HurtBoxManager）。
    /// 由 PlayerModel 超时安全网调用。
    /// </summary>
    public void ForceResetHitState()
    {
        if (hitReactionController != null)
            hitReactionController.ForceReset();
        isHitting = false;
    }

    public void ProcessHit(AttackEvent hit, GameObject attacker, AbilitySystemComponent attackerASC = null)
    {
        if (isInvincible)
            return;

        AbilitySystemComponent attackerAscLocal = attackerASC ?? attacker?.GetComponent<AbilitySystemComponent>();

        // 弹反 (Parry)
        if (_tagComponent.HasTag(perfectParryTag) ||
            _tagComponent.HasTag(normalParryTag))
        {
            var attackerTagComponent = attacker.GetComponent<TagComponent>();
            if (attackerTagComponent != null && parrySuccessTag != null)
                attackerTagComponent.AddTransientTag(parrySuccessTag);

            // 弹反成功 VFX
            if (parrySuccessVFX != null)
            {
                Vector3 hitPoint = attacker != null ?
                    (transform.position + attacker.transform.position) / 2f : transform.position;
                Instantiate(parrySuccessVFX, hitPoint, Quaternion.identity);
            }
            return;
        }

        // 格挡 (Guard/Block)
        if (_tagComponent.HasTag(guardingTag))
        {
            HandleBlockedHit(hit, attacker, attackerAscLocal);
            return;
        }

        // 完美闪避 (Perfect Dodge)
        if (perfectDodgeTag != null && _tagComponent.HasTag(perfectDodgeTag))
        {
            if (hit.attackData != null && hit.attackData.perfectDodgePunishEffect != null)
            {
                var punish = hit.attackData.perfectDodgePunishEffect;
                if (attackerAscLocal != null)
                {
                    try
                    {
                        int handle = attackerAscLocal.ApplyGameplayEffect(punish, _asc);
                        if (handle > 0)
                        {
                            var attackerTagComp = attackerAscLocal.GetComponent<TagComponent>();
                            bool alreadyHasPunishTag = false;
                            if (attackerTagComp != null && punish != null)
                            {
                                foreach (var granted in punish.grantedTags)
                                {
                                    if (granted != null && attackerTagComp.HasTag(granted))
                                    {
                                        alreadyHasPunishTag = true;
                                        break;
                                    }
                                }
                            }

                            if (!alreadyHasPunishTag)
                                attackerAscLocal.InterruptCurrentAbility();
                        }
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogWarning($"HurtBoxManager: applying perfect-dodge punish effect threw: {e}");
                    }
                }
            }

            return;
        }

        // 正常受击 - 施加伤害
        ApplyDamageToTarget(hit, attacker, attackerASC);
    }

    /// <summary>
    /// 处理被格挡的攻击
    /// </summary>
    private void HandleBlockedHit(AttackEvent hit, GameObject attacker, AbilitySystemComponent attackerAscLocal)
    {
        float poiseCost = blockPoiseCostBase;
        float damage = 0f;

        // 计算减伤后的伤害和 Poise 消耗
        if (hit.attackData != null && hit.attackData.effect != null)
        {
            damage = hit.attackData.effect.damage * (1f - blockDamageReduction);

            // 根据攻击力度调整 Poise 消耗
            poiseCost = hit.attackData.forceType switch
            {
                AttackForceType.Light => blockPoiseCostBase * 0.5f,
                AttackForceType.Medium => blockPoiseCostBase * 1.0f,
                AttackForceType.Heavy => blockPoiseCostBase * 2.0f,
                _ => blockPoiseCostBase
            };
        }

        // 1. 应用减伤后的伤害
        if (damage > 0f && _attributes != null)
        {
            _attributes.ModifyHealth(-damage);
            Debug.Log($"{gameObject.name} blocked! Took {damage:F1} reduced damage ({(1f - blockDamageReduction) * 100f:F0}% bleed-through)");
        }

        // 2. 消耗防御者 Poise
        if (_attributes != null)
        {
            _attributes.ModifyPoise(-poiseCost);
        }

        // 3. 施加 stagger 给攻击者
        if (hit.attackData != null && hit.attackData.staggerEffect != null)
        {
            var stagger = hit.attackData.staggerEffect;
            if (attackerAscLocal != null)
            {
                try
                {
                    int handle = attackerAscLocal.ApplyGameplayEffect(stagger, _asc);
                    if (handle > 0)
                    {
                        var attackerTagComp = attackerAscLocal.GetComponent<TagComponent>();
                        bool alreadyHasStaggerTag = false;
                        if (attackerTagComp != null && stagger != null)
                        {
                            foreach (var granted in stagger.grantedTags)
                            {
                                if (granted != null && attackerTagComp.HasTag(granted))
                                {
                                    alreadyHasStaggerTag = true;
                                    break;
                                }
                            }
                        }
                        if (!alreadyHasStaggerTag)
                        {
                            attackerAscLocal.InterruptCurrentAbility();
                        }
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"HurtBoxManager: applying stagger effect threw: {e}");
                }
            }
        }

        // 4. 检查 Poise 是否耗尽 → 破防
        if (_attributes != null && _attributes.Poise <= 0f && _attributes.IsBroken)
        {
            // 移除格挡标签
            if (guardingTag != null && _tagComponent != null)
                _tagComponent.RemoveTag(guardingTag);

            // 施加破防硬直效果
            if (guardBreakEffect != null && _asc != null)
            {
                _asc.ApplyGameplayEffect(guardBreakEffect, _asc);
            }

            // 授予破防标签
            if (guardBreakTag != null && _tagComponent != null)
                _tagComponent.AddTag(guardBreakTag);

            Debug.Log($"{gameObject.name} guard broken! Poise depleted.");
        }

        // 5. 播放格挡反应（动画 + VFX + 音效 + 相机震动）
        PlayBlockReaction(hit, attacker);
    }

    /// <summary>
    /// 播放格挡反应：动画、火花VFX、音效、相机震动
    /// </summary>
    private void PlayBlockReaction(AttackEvent hit, GameObject attacker)
    {
        // 格挡火花 VFX
        if (blockSparksVFX != null)
        {
            Vector3 hitPoint = attacker != null ?
                (transform.position + attacker.transform.position) / 2f : transform.position;
            Quaternion hitRotation = attacker != null ?
                Quaternion.LookRotation(attacker.transform.position - transform.position) : Quaternion.identity;
            Instantiate(blockSparksVFX, hitPoint, hitRotation);
        }

        // 格挡音效
        if (blockSound != null && _audioSource != null)
        {
            _audioSource.PlayOneShot(blockSound);
        }

        // 相机震动
        if (_impulseSource != null && blockCameraShake > 0f)
        {
            Vector3 shakeDir = attacker != null ?
                (attacker.transform.position - transform.position).normalized : Vector3.back;
            _impulseSource.GenerateImpulseWithVelocity(shakeDir * blockCameraShake);
        }

        // 格挡动画（在受击层播放）
        if (_animancer != null && blockReactionAnimation != null && blockReactionAnimation.Clip != null)
        {
            int blockLayerIndex = 2; // 与 HitReactionController 使用同一层
            if (_animancer.Layers.Count <= blockLayerIndex)
                _animancer.Layers.Count = blockLayerIndex + 1;

            var blockLayer = _animancer.Layers[blockLayerIndex];
            blockLayer.SetWeight(1f);
            var state = blockLayer.Play(blockReactionAnimation, 0.05f, FadeMode.FromStart);
            state.Events(this).OnEnd = () =>
            {
                blockLayer.StartFade(0f, 0.2f);
            };
        }
    }

    /// <summary>
    /// 正常受击：施加伤害效果并播放受击反应
    /// </summary>
    private void ApplyDamageToTarget(AttackEvent hit, GameObject attacker, AbilitySystemComponent attackerASC)
    {
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