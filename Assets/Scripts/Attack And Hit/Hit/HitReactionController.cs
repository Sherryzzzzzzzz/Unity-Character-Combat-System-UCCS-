using System.Collections;
using UnityEngine;
using Animancer;
using Cinemachine;

[RequireComponent(typeof(CharacterController), typeof(AnimancerComponent))]
public class HitReactionController : MonoBehaviour
{
    public float hitDurationLight = 0.7f;
    public float hitDurationMedium = 1.0f;
    public float hitDurationHeavy = 1.5f;
    public float hitDurationBlow = 3.0f;

    public float knockbackDuration = 0.25f;
    public AnimationCurve knockbackCurve = AnimationCurve.EaseInOut(0, 1, 1, 0);

    public float hitFreezeFrame = 0.03f;
    public float hitImpulseAmplitude = 1f;

    public ExpandableAnimationSet animationSet;

    public int hitLayerIndex = 2;

    public bool isHitting;

    private CharacterController cc;
    private AnimancerComponent animancer;
    private AnimancerLayer _hitLayer;
    private CinemachineImpulseSource _impulseSource;
    private Coroutine _hitFlowCoroutine;
    private HitStrength _currentHitStrength;
    private Transform tf;

    private void Awake()
    {
        tf = transform;
        cc = GetComponent<CharacterController>();
        animancer = GetComponent<AnimancerComponent>();
        _impulseSource = GetComponent<CinemachineImpulseSource>();

        if (animancer.Layers.Count <= hitLayerIndex)
            animancer.Layers.Count = hitLayerIndex + 1;

        _hitLayer = animancer.Layers[hitLayerIndex];
        _hitLayer.SetWeight(0);
    }

    /// <summary>
    /// 强制重置受击状态，停止 HitFlow 协程并清理所有标记。
    /// 由 HurtBoxManager.ForceResetHitState() 调用。
    /// </summary>
    public void ForceReset()
    {
        if (_hitFlowCoroutine != null)
        {
            StopCoroutine(_hitFlowCoroutine);
            _hitFlowCoroutine = null;
        }
        isHitting = false;
        _hitLayer.StartFade(0f, 0.25f);
    }

    public void PlayHit(AttackEvent hit)
    {
        HitStrength incomingStrength = EvaluateStrength(hit);

        if (_hitFlowCoroutine != null && incomingStrength <= _currentHitStrength)
            return;

        if (_hitFlowCoroutine != null)
        {
            // 在停止协程之前手动清理状态，因为 StopCoroutine 不会执行协程的后续代码
            isHitting = false;
            _hitLayer.StartFade(0f, 0.25f);
#if UNITY_EDITOR
            Debug.Log($"HitReactionController: 中断当前 HitFlow ({_currentHitStrength}) -> 新受击 ({incomingStrength})", this);
#endif
            StopCoroutine(_hitFlowCoroutine);
            _hitFlowCoroutine = null;
        }

        _hitFlowCoroutine = StartCoroutine(HitFlow(hit));
    }

    private IEnumerator HitFlow(AttackEvent hit)
    {
        _currentHitStrength = EvaluateStrength(hit);
        isHitting = true;

        // 受击时立即中断当前正在播放的技能（翻滚、攻击等），确保技能状态干净终止
        var playerSkill = GetComponent<PlayerSkillComponent>();
        if (playerSkill != null)
        {
            playerSkill.StopAndCleanup(true, false);
        }
        else
        {
            var enemySkill = GetComponent<EnemySkillComponent>();
            if (enemySkill != null)
                enemySkill.StopAndCleanup();
        }

        float duration = GetHitDuration(_currentHitStrength);

        // ★ 新版 HitStop：使用 HitStopController 独立卡肉（不冻结全局Time.timeScale）
        var hitStop = GetComponent<HitStopController>();
        if (hitStop != null && hit?.attackData != null)
        {
            hitStop.ApplyVictimHitStop(hit.attackData.forceType);
            // 也给攻击者一点卡肉
            var attacker = hit.hitObject;
            if (attacker != null)
            {
                var attackerHitStop = attacker.GetComponentInParent<HitStopController>();
                attackerHitStop?.ApplyAttackerHitStop(hit.attackData.forceType);
            }
        }
        else
        {
            // Fallback: 旧版冻帧（仅在 HitStopController 不存在时使用）
            if (hitFreezeFrame > 0f)
            {
                Time.timeScale = 0.01f;
                yield return new WaitForSecondsRealtime(hitFreezeFrame);
                Time.timeScale = 1f;
            }
        }

        // ★ 命中反馈 VFX + SFX + Hit Flash
        var feedbackMgr = GetComponent<HitFeedbackManager>();
        if (feedbackMgr != null && hit?.attackData != null)
        {
            Vector3 hitPoint = hit.hitPoint != Vector3.zero ? hit.hitPoint : transform.position;
            Vector3 attackDir = hit.GetForceDirection();
            feedbackMgr.PlayHitFeedback(hit.attackData.forceType, hitPoint, attackDir);
        }

        // 播动画
        _hitLayer.SetWeight(1f);
        int dir4 = Resolve4Direction(hit);
        string animName = Compose4DirAnimation(_currentHitStrength, dir4);
        PlayHitAnimation(animName);

        // 相机震动
        if (_impulseSource != null)
            _impulseSource.GenerateImpulseWithVelocity(hit.GetForceDirection());

        // 击退
        yield return StartCoroutine(ApplyKnockbackForce(hit));

        yield return new WaitForSeconds(duration);

        _hitLayer.StartFade(0f, 0.25f);
        isHitting = false;
        // Ensure any behavior locks are released (safety): resume if a behavior controller exists
        var behaviorController = GetComponent<Parryable.IBehaviorController>();
        if (behaviorController != null)
        {
            try { behaviorController.ResumeBehavior(); }
            catch (System.Exception e) { Debug.LogWarning($"HitReactionController: ResumeBehavior threw: {e}"); }
        }
        // Restore player to appropriate state after hit recovery
        var playerModel = GetComponent<PlayerModel>();
        if (playerModel != null)
        {
            var ts = playerModel.ts;
            if (ts != null && ts.HasTarget)
                playerModel.ChangePlayerState(PlayerState.aim);
            else if (PlayerController.Instance != null && PlayerController.Instance.isGround)
                playerModel.ChangePlayerState(PlayerState.ground);
            else
                playerModel.ChangePlayerState(PlayerState.sky);
        }
        _hitFlowCoroutine = null;
    }

    private IEnumerator ApplyKnockbackForce(AttackEvent hit)
    {
        // Guard: require attackData and sufficient force
        if (hit?.attackData == null || hit.attackData.forceType < AttackForceType.Medium)
            yield break;

        if (cc == null)
            yield break;

        float timer = 0f;

        while (timer < knockbackDuration)
        {
            Vector3 dir = hit.GetForceDirection();
            dir.y = 0;
            dir.Normalize();

            float curve = (knockbackCurve != null) ? knockbackCurve.Evaluate(timer / knockbackDuration) : 1f;
            float force = hit.attackData != null ? hit.attackData.hitForce : 0f;
            Vector3 move = -dir * force * curve * Time.deltaTime;
            cc.Move(move);

            timer += Time.deltaTime;
            yield return null;
        }
    }

    private HitStrength EvaluateStrength(AttackEvent hit) => hit.attackData?.forceType switch
    {
        AttackForceType.Light => HitStrength.Light,
        AttackForceType.Medium => HitStrength.Medium,
        AttackForceType.Heavy => HitStrength.Heavy,
        AttackForceType.Blow => HitStrength.Blow,
        _ => HitStrength.Light
    };

    private float GetHitDuration(HitStrength s) => s switch
    {
        HitStrength.Light => hitDurationLight,
        HitStrength.Medium => hitDurationMedium,
        HitStrength.Heavy => hitDurationHeavy,
        HitStrength.Blow => hitDurationBlow,
        _ => hitDurationLight
    };

    private int Resolve4Direction(AttackEvent hit)
    {
        Vector3 attackDir = hit.GetForceDirection();
        attackDir.y = 0;
        attackDir.Normalize();

        float dot = Vector3.Dot(tf.forward, attackDir);
        float cross = Vector3.Cross(tf.forward, attackDir).y;

        if (dot > 0.707f) return 0;
        if (dot < -0.707f) return 2;
        if (cross > 0) return 3;
        return 1;
    }

    private string FourDirectionName(int dir) => dir switch
    {
        0 => "F",
        1 => "R",
        2 => "B",
        3 => "L",
        _ => "F"
    };

    private string StrengthLetter(HitStrength s) => s switch
    {
        HitStrength.Light => "L",
        HitStrength.Medium => "M",
        HitStrength.Heavy => "H",
        HitStrength.Blow => "B",
        _ => "L"
    };

    private string Compose4DirAnimation(HitStrength s, int dir)
        => $"{FourDirectionName(dir)}_{StrengthLetter(s)}";

    private void PlayHitAnimation(string name)
    {
        if (animationSet == null) return;

        var clip = animationSet.GetClip(name);
        if (clip != null)
            _hitLayer.Play(clip, 0.1f, FadeMode.FromStart);
    }
}

public enum HitStrength { Light, Medium, Heavy, Blow }