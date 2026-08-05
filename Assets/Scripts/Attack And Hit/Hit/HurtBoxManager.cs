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
    [Tooltip("格挡时消耗的Stamina（体力）基础值")]
    public float blockStaminaCostBase = 15f;
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

    [Header("受击音效")]
    [Tooltip("普通受击音效")]
    public AudioClip hitSound;
    [Tooltip("重击受击音效（可选，为空则用普通音效）")]
    public AudioClip heavyHitSound;

    [Header("弹反 VFX")]
    [Tooltip("弹反成功时生成的 VFX 预制体")]
    public GameObject parrySuccessVFX;
    [Tooltip("弹反成功音效")]
    public AudioClip parrySuccessSound;

    [Header("Just Guard (鬼泣式完美格挡)")]
    [Tooltip("完美格挡窗口标签：格挡激活后的极短窗口内被命中 = Just Guard（无伤、零消耗、弹开攻击者、慢动作、反击加成）")]
    public GameplayTagSO justGuardTag;
    [Tooltip("Just Guard 窗口时长（秒）。进入格挡后此窗口内被命中即触发")]
    public float justGuardWindow = 0.13f;
    [Tooltip("Just Guard 成功后授予的反击标签：持有期间下一次攻击伤害提升")]
    public GameplayTagSO counterReadyTag;
    [Tooltip("反击伤害倍率 (1.5 = 150% 伤害)")]
    public float counterMultiplier = 1.5f;
    [Tooltip("反击状态持续时长（秒）")]
    public float counterWindowDuration = 0.8f;
    [Tooltip("Just Guard 慢动作时间缩放 (0.15 = 15% 速度)")]
    public float justGuardTimeScale = 0.15f;
    [Tooltip("Just Guard 慢动作持续真实秒数")]
    public float justGuardSlowMotionDuration = 0.12f;
    [Tooltip("Just Guard 弹开攻击者的击退力度倍率（相对普通格挡）")]
    public float justGuardPushMultiplier = 2f;

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

        // 运行时兜底：Just Guard / 反击标签未配置时自动创建，保证新系统开箱即用
        if (justGuardTag == null)
            justGuardTag = CreateRuntimeTag("State.Guarding.JustGuard");
        if (counterReadyTag == null)
            counterReadyTag = CreateRuntimeTag("State.Counter.Ready");
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

        // ★ 防御兜底：通过接口检查防御状态，不依赖 PlayerModel
        var defense = GetComponent<UCCS.IDefenseStateProvider>();
        if (defense != null && defense.IsDefending)
        {
            var defendingAsc = attackerASC ?? attacker?.GetComponent<AbilitySystemComponent>();
            HandleBlockedHit(hit, attacker, defendingAsc);
            return;
        }

        var attackerAscLocal = attackerASC ?? attacker?.GetComponent<AbilitySystemComponent>();

        // 弹反 (Parry) — 鬼泣式三级判定：Just Guard(完美格挡) > 完美弹反 > 普通弹反
        if (justGuardTag != null && _tagComponent.HasTag(justGuardTag))
        {
            HandleJustGuard(hit, attacker, attackerAscLocal);
            return;
        }
        if (_tagComponent.HasTag(perfectParryTag) ||
            _tagComponent.HasTag(normalParryTag))
        {
            HandleParry(hit, attacker);
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
        float staminaCost = blockStaminaCostBase;
        float damage = 0f;

        // 计算减伤后的伤害（含 Defense 减伤）
        if (hit.attackData != null && hit.attackData.effect != null)
        {
            float rawDamage = hit.attackData.effect.damage * hit.attackData.effect.damageMultiplier;
            float defense = _attributes != null ? _attributes.Defense : 0f;
            // 格挡减伤后再扣 defense
            damage = Mathf.Max(1f, (rawDamage * (1f - blockDamageReduction)) - defense * 0.5f);

            poiseCost = hit.attackData.forceType switch
            {
                AttackForceType.Light => blockPoiseCostBase * 0.5f,
                AttackForceType.Medium => blockPoiseCostBase * 1.0f,
                AttackForceType.Heavy => blockPoiseCostBase * 2.0f,
                AttackForceType.Blow => blockPoiseCostBase * 3.0f,
                _ => blockPoiseCostBase
            };

            staminaCost = hit.attackData.forceType switch
            {
                AttackForceType.Light => blockStaminaCostBase * 0.5f,
                AttackForceType.Medium => blockStaminaCostBase * 1.0f,
                AttackForceType.Heavy => blockStaminaCostBase * 1.5f,
                AttackForceType.Blow => blockStaminaCostBase * 2.0f,
                _ => blockStaminaCostBase
            };
        }

        // 1. 应用减伤后的伤害
        if (damage > 0f && _attributes != null)
        {
            _attributes.ModifyHealth(-damage);
#if UNITY_EDITOR
            Debug.Log($"{gameObject.name} blocked! Took {damage:F1} dmg (raw={(hit.attackData?.effect?.damage ?? 0f):F0}, blocked={blockDamageReduction*100f:F0}%, defense={_attributes?.Defense ?? 0f:F0})");
#endif
        }

        // 2. 消耗防御者 Poise（韧性）
        if (_attributes != null)
        {
            _attributes.ModifyPoise(-poiseCost);
        }

        // 2.5. 消耗防御者 Stamina（体力）
        if (_attributes != null && staminaCost > 0f)
        {
            _attributes.ModifyStamina(-staminaCost);
        }

        // 3. 攻击者被格挡反制：stagger + 击退
        if (attackerAscLocal != null)
        {
            // 优先使用 AttackData 上配置的 staggerEffect
            if (hit.attackData != null && hit.attackData.staggerEffect != null)
            {
                attackerAscLocal.ApplyGameplayEffect(hit.attackData.staggerEffect, _asc);
            }
            else
            {
                // Fallback：直接让攻击者硬直（中断当前技能 + 小击退）
                var attackerSkill = attacker?.GetComponent<PlayerSkillComponent>();
                if (attackerSkill != null && attackerSkill.isPlaying)
                {
                    attackerSkill.StopAndCleanup(true, false);
                }
                else
                {
                    var enemySkill = attacker?.GetComponent<EnemySkillComponent>();
                    enemySkill?.StopAndCleanup();
                }
            }

            // 击退攻击者
            var attackerCC = attacker?.GetComponent<CharacterController>();
            if (attackerCC != null)
            {
                Vector3 pushDir = (attacker.transform.position - transform.position).normalized;
                float pushForce = hit.attackData?.forceType switch
                {
                    AttackForceType.Light => 2f,
                    AttackForceType.Medium => 4f,
                    AttackForceType.Heavy => 6f,
                    AttackForceType.Blow => 10f,
                    _ => 3f
                };
                if (_animancer != null)
                    StartCoroutine(PushBackRoutine(attackerCC, pushDir, pushForce, 0.2f));
            }

            // 攻击者命中停顿（双向停顿，强化格挡手感）
            var attackerHitStop = attacker?.GetComponent<HitStopController>();
            if (attackerHitStop != null && hit.attackData != null)
                attackerHitStop.ApplyAttackerHitStop(hit.attackData.forceType);
        }

        // 4. 检查 Poise 是否耗尽 → 破防
        if (_attributes != null && _attributes.Poise <= 0f && _attributes.IsBroken)
        {
            if (guardingTag != null && _tagComponent != null)
                _tagComponent.RemoveTag(guardingTag);

            if (guardBreakEffect != null && _asc != null)
                _asc.ApplyGameplayEffect(guardBreakEffect, _asc);

            if (guardBreakTag != null && _tagComponent != null)
                _tagComponent.AddTag(guardBreakTag);

            // 破防冲击波 VFX
            var pool = UnityEngine.Object.FindFirstObjectByType<GlobalVFXPool>();
            if (pool != null)
                pool.SpawnGuardBreakWave(transform.position);

            Debug.Log($"{gameObject.name} guard broken! Poise depleted.");
        }

        // 5. 播放格挡反应
        PlayBlockReaction(hit, attacker);
    }

    private System.Collections.IEnumerator PushBackRoutine(CharacterController cc, Vector3 dir, float force, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            cc.Move(dir * force * Time.deltaTime);
            elapsed += Time.deltaTime;
            yield return null;
        }
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

        // 格挡动画（在受击层以低权重混合播放，不覆盖防御动画）
        if (_animancer != null && blockReactionAnimation != null && blockReactionAnimation.Clip != null)
        {
            int blockLayerIndex = 2;
            if (_animancer.Layers.Count <= blockLayerIndex)
                _animancer.Layers.Count = blockLayerIndex + 1;

            var blockLayer = _animancer.Layers[blockLayerIndex];
            // ★ 使用低权重混合而非完全覆盖（0.35 让格挡反馈可见但不打断防御姿态）
            blockLayer.SetWeight(0.35f);
            var state = blockLayer.Play(blockReactionAnimation, 0.03f, FadeMode.FromStart);
            state.Events(this).OnEnd = () =>
            {
                blockLayer.StartFade(0f, 0.15f);
            };
        }
    }

    /// <summary>
    /// 鬼泣式 Just Guard（完美格挡）：
    /// 无伤、零韧性/体力消耗、攻击者被大幅弹开、触发慢动作 + 蓝白火花 + 反击状态。
    /// </summary>
    private void HandleJustGuard(AttackEvent hit, GameObject attacker, AbilitySystemComponent attackerAscLocal)
    {
        // 1. 给攻击者施加"被弹反"事件 → Parryable 中断攻击并播放被弹反硬直
        var attackerTagComponent = attacker != null ? attacker.GetComponent<TagComponent>() : null;
        if (attackerTagComponent != null && parrySuccessTag != null)
            attackerTagComponent.AddTransientTag(parrySuccessTag);

        // 2. 弹开攻击者（比普通格挡更强的击退 + 强制受击停顿）
        if (attacker != null && attacker != gameObject)
        {
            var attackerCC = attacker.GetComponent<CharacterController>();
            if (attackerCC != null)
            {
                Vector3 pushDir = (attacker.transform.position - transform.position).normalized;
                pushDir.y = 0f;
                if (pushDir.sqrMagnitude < 0.01f) pushDir = -transform.forward;

                float basePush = hit.attackData?.forceType switch
                {
                    AttackForceType.Light => 4f,
                    AttackForceType.Medium => 6f,
                    AttackForceType.Heavy => 8f,
                    AttackForceType.Blow => 12f,
                    _ => 5f
                };
                StartCoroutine(PushBackRoutine(attackerCC, pushDir, basePush * justGuardPushMultiplier, 0.28f));
            }

            var attackerHitStop = attacker.GetComponent<HitStopController>();
            if (attackerHitStop != null)
                attackerHitStop.ApplyAttackerHitStop(hit.attackData?.forceType ?? AttackForceType.Light);
        }

        // 3. 慢动作（鬼泣招牌：时间冻结后回弹）
        TimeScaleDirector.Instance.DoSlowMotion(justGuardTimeScale, justGuardSlowMotionDuration, restoreImmediately: false);

        // 4. 视觉/听觉反馈：蓝白火花 + 冲击波 + 金属音 + 相机震动 + FOV
        Vector3 hitPoint = attacker != null
            ? (transform.position + attacker.transform.position) / 2f
            : transform.position;
        var pool = FindFirstObjectByType<GlobalVFXPool>();
        if (pool != null)
            pool.SpawnClashVFX(hitPoint);
        else if (blockSparksVFX != null)
            Instantiate(blockSparksVFX, hitPoint, Quaternion.identity);

        if (parrySuccessSound != null && _audioSource != null)
            _audioSource.PlayOneShot(parrySuccessSound);

        if (_impulseSource != null)
        {
            Vector3 shakeDir = attacker != null
                ? (attacker.transform.position - transform.position).normalized
                : Vector3.back;
            _impulseSource.GenerateImpulseWithVelocity(shakeDir * (blockCameraShake * 1.5f));
        }
        if (CameraImpactEffects.Instance != null)
            CameraImpactEffects.Instance.ApplyFOVKick(AttackForceType.Heavy);

        // 5. 授予反击状态：下次攻击伤害提升，短暂持续
        if (counterReadyTag != null)
        {
            _tagComponent.AddTag(counterReadyTag);
            StartCoroutine(RemoveTagAfterDelay(counterReadyTag, counterWindowDuration));
        }
    }

    /// <summary>
    /// 普通弹反/完美弹反：中断攻击者技能并播放被弹反硬直（无慢动作与反击加成）。
    /// </summary>
    private void HandleParry(AttackEvent hit, GameObject attacker)
    {
        var attackerTagComponent = attacker != null ? attacker.GetComponent<TagComponent>() : null;
        if (attackerTagComponent != null && parrySuccessTag != null)
            attackerTagComponent.AddTransientTag(parrySuccessTag);

        // 弹反成功 VFX
        if (parrySuccessVFX != null)
        {
            Vector3 hitPoint = attacker != null ?
                (transform.position + attacker.transform.position) / 2f : transform.position;
            Instantiate(parrySuccessVFX, hitPoint, Quaternion.identity);
        }

        // 弹反成功音效
        if (parrySuccessSound != null && _audioSource != null)
            _audioSource.PlayOneShot(parrySuccessSound);
    }

    /// <summary>
    /// 消耗攻击者的反击标签（Just Guard 触发）。返回 true 表示本次攻击享受反击加成。
    /// </summary>
    private bool TryConsumeCounterTag(AbilitySystemComponent attackerASC)
    {
        if (attackerASC == null || counterReadyTag == null) return false;
        var tags = attackerASC.GetComponent<TagComponent>();
        if (tags == null || !tags.HasTag(counterReadyTag)) return false;
        tags.RemoveTag(counterReadyTag);
        return true;
    }

    private System.Collections.IEnumerator RemoveTagAfterDelay(GameplayTagSO tag, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (_tagComponent != null && tag != null)
            _tagComponent.RemoveTag(tag);
    }

    /// <summary>
    /// 运行时兜底：标签资产未在场景中配置时自动创建（保证 Just Guard / 反击开箱即用）。
    /// 仅用于运行时代码路径，不写入资产库。
    /// </summary>
    private static GameplayTagSO CreateRuntimeTag(string tagName)
    {
        var tag = ScriptableObject.CreateInstance<GameplayTagSO>();
        tag.name = tagName;
        return tag;
    }

    /// <summary>
    /// 正常受击：施加伤害效果并播放受击反应
    /// </summary>
    private void ApplyDamageToTarget(AttackEvent hit, GameObject attacker, AbilitySystemComponent attackerASC)
    {
        // 施加伤害
        if (_asc != null && attackerASC != null && hit.attackData != null && hit.attackData.effect != null)
        {
            // ★ 反击加成：攻击者持有 counterReadyTag（来自 Just Guard）时伤害提升并消耗该标签
            if (TryConsumeCounterTag(attackerASC))
            {
                var context = attackerASC.MakeEffectContext();
                context.Instigator = attacker;
                context.Origin = hit.hitPoint;
                context.Normal = attacker != null
                    ? (transform.position - attacker.transform.position).normalized
                    : Vector3.back;

                var spec = attackerASC.MakeOutgoingSpec(hit.attackData.effect, 1f, context);
                if (spec != null)
                {
                    for (int i = 0; i < spec.EffectData.modifiers.Count; i++)
                        spec.SetMagnitudeOverride(i, spec.GetMagnitude(i) * counterMultiplier);
                    _asc.ApplyEffectSpec(spec);
                }
            }
            else
            {
                _asc.ApplyGameplayEffect(hit.attackData.effect, attackerASC);
            }
        }

        // 受击音效
        if (_audioSource != null)
        {
            AudioClip clip = null;
            if (hit.attackData != null && (hit.attackData.forceType == AttackForceType.Heavy || hit.attackData.forceType == AttackForceType.Blow))
                clip = heavyHitSound != null ? heavyHitSound : hitSound;
            else
                clip = hitSound;

            if (clip != null)
                _audioSource.PlayOneShot(clip);
        }

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