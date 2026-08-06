using System.Collections;
using UnityEngine;
using Animancer;
using Cinemachine;

[RequireComponent(typeof(CharacterController), typeof(AnimancerComponent))]
public class HitReactionController : MonoBehaviour
{
    public float hitDurationLight = 0.5f;
    public float hitDurationMedium = 0.8f;
    public float hitDurationHeavy = 1.2f;
    public float hitDurationBlow = 2.0f;

    public float knockbackDuration = 0.25f;
    public AnimationCurve knockbackCurve = AnimationCurve.EaseInOut(0, 1, 1, 0);

    public float hitFreezeFrame = 0.03f;
    public float hitImpulseAmplitude = 1f;

    [Header("受击恢复过渡 (P4)")]
    [Tooltip("受击动画结束/被打断时，受击层淡出时长（秒）。0=瞬切（生硬弹回）")]
    public float hitRecoveryFadeTime = 0.12f;

    [Header("受击动画链 (连锁反应)")]
    [Tooltip("★ 受击动画链：连续受击时逐级升级受击动画（轻→中→重→吹飞）。\n关闭则保持原有逻辑：弱受击不打断强受击，同级重新播放")]
    public bool useHitChain = true;

    [Header("击退力度档位 (P3)")]
    [Tooltip("AttackData.hitForce > 0 时优先用数据值；否则用以下档位默认（单位 m/s）")]
    public float knockbackSpeedMedium = 9.5f;
    public float knockbackSpeedHeavy = 20f;
    public float knockbackSpeedBlow = 32f;

    [Header("击飞 (P10)")]
    [Tooltip("击飞重力加速度（负值，绝对值越大坠落越快）")]
    public float launchGravity = -10f;
    [Tooltip("★ 全局最低滞空时间（秒）：击飞后保证至少浮空这么久再允许落地（连招窗口的下限）。\n技能资产 AttackData.minAirTime > 0 时以资产为准")]
    public float minLaunchAirTime = 1.8f;
    [Tooltip("击飞滞空动画名（在 animationSet 中查找，留空=不播）")]
    public string airAnimationName = "Air";
    [Tooltip("落地受身动画名（在 animationSet 中查找，留空=不播）")]
    public string landAnimationName = "Land";

    public ExpandableAnimationSet animationSet;

    public int hitLayerIndex = 2;

    public bool isHitting;

    private CharacterController cc;
    private AnimancerComponent animancer;
    private AnimancerLayer _hitLayer;
    private AnimancerLayer _baseLayer;
    private CinemachineImpulseSource _impulseSource;
    private Coroutine _hitFlowCoroutine;
    private HitStrength _currentHitStrength;
    private Transform tf;

    // ★ P10: 击飞状态（受击方被重击打飞：垂直初速 + 重力积分，直到落地）
    private bool _isLaunched;
    private float _launchVelocity;
    public bool IsLaunched => _isLaunched;

    private void Awake()
    {
        tf = transform;
        cc = GetComponent<CharacterController>();
        animancer = GetComponent<AnimancerComponent>();
        _impulseSource = GetComponent<CinemachineImpulseSource>();

        // ★ 自装配反馈链路：确保 HitFeedbackManager（VFX/SFX/闪白/震屏）+ HitStopController（卡肉）存在。
        // 场景/预制体未手动挂载时自动补全，避免命中反馈链路缺失导致退化成全局冻帧。
        if (GetComponent<HitFeedbackManager>() == null)
            gameObject.AddComponent<HitFeedbackManager>();
        if (GetComponent<HitStopController>() == null)
            gameObject.AddComponent<HitStopController>();

        if (animancer.Layers.Count <= hitLayerIndex)
            animancer.Layers.Count = hitLayerIndex + 1;

        _hitLayer = animancer.Layers[hitLayerIndex];
        _baseLayer = animancer.Layers[0];
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
        // ★ 淡出而非瞬切，避免受击结束“弹回”待机/移动姿态
        _hitLayer.StartFade(0f, hitRecoveryFadeTime);
        _baseLayer.StartFade(1f, hitRecoveryFadeTime);
        // ★ P10: 复位击飞状态（超时安全网触发时，恢复自由落体）
        _isLaunched = false;
        _launchVelocity = 0f;
        // ★ 强制释放卡肉请求，动画速度立即恢复正常
        var hitStop = GetComponent<HitStopController>();
        if (hitStop != null) hitStop.ReleaseAll();
    }

    public void PlayHit(AttackEvent hit)
    {
        // 【防御保护】格挡/防御/翻滚状态下不播放受击动画
        var playerModel = GetComponent<PlayerModel>();
        if (playerModel != null && (playerModel.isDefending || playerModel.isDodging))
        {
            Debug.Log($"{gameObject.name}: PlayHit blocked - player is defending/dodging", this);
            return;
        }

        HitStrength incomingStrength = EvaluateStrength(hit);

        // ★ 受击动画链：连续受击时逐级升级受击动画（轻→中→重→吹飞）。
        //   升级后的强度 = max(当前强度+1, 本次受击强度)，封顶吹飞。
        //   例：轻→轻 = L→M；轻→重 = 直接 H；中→轻 = M→H（轻击也能把中段反应推升到重段）
        if (_hitFlowCoroutine != null)
        {
            if (useHitChain)
            {
                int nextLevel = Mathf.Max((int)_currentHitStrength + 1, (int)incomingStrength);
                _currentHitStrength = (HitStrength)Mathf.Min(nextLevel, (int)HitStrength.Blow);
            }
            else if (incomingStrength <= _currentHitStrength)
            {
                return; // 非链模式：弱受击不打断强受击
            }
            else
            {
                _currentHitStrength = incomingStrength;
            }
        }
        else
        {
            _currentHitStrength = incomingStrength;
        }

        // 中断当前 HitFlow（StopCoroutine 不会执行协程后续代码，需手动清理状态）
        if (_hitFlowCoroutine != null)
        {
            isHitting = false;
            _hitLayer.StartFade(0f, hitRecoveryFadeTime);
            _baseLayer.StartFade(1f, hitRecoveryFadeTime);
            // ★ P10: 旧击飞被新受击打断 → 复位垂直速度，恢复自由落体（新受击若也是击飞会重新注入）
            _isLaunched = false;
            _launchVelocity = 0f;
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
        // ★ 免疫：受击流程任何环节抛异常都不允许卡死角色（isHitting/动画层/卡肉速度）。
        //   C# 禁止在带 catch 的 try 里 yield，因此用 try/finally：
        //   协程迭代器的 finally 在正常结束或异常传播时都会执行，异常由 Unity 记录堆栈。
        try
        {
            yield return StartCoroutine(HitFlowCore(hit));
        }
        finally
        {
            // ★ 兜底清理：无论正常结束还是异常，以下状态必然复位——
            //   修复“攻击不了（isHitting 卡死）”“移动慢动作（卡肉速度未释放）”“动画层残留”
            _hitLayer.StartFade(0f, hitRecoveryFadeTime);
            _baseLayer.StartFade(1f, hitRecoveryFadeTime);
            isHitting = false;
            _isLaunched = false;
            _launchVelocity = 0f;

            var hitStop = GetComponent<HitStopController>();
            if (hitStop != null) hitStop.ReleaseAll();

            ResumeBehaviorAndState();
            _hitFlowCoroutine = null;
        }
    }

    /// <summary>受击流程主体（异常由 HitFlow 的 try/finally 兜底）</summary>
    private IEnumerator HitFlowCore(AttackEvent hit)
    {
        // _currentHitStrength 已由 PlayHit 决定（含受击链升级逻辑），此处不再重新计算
        isHitting = true;

        // 受击时立即中断当前正在播放的技能（翻滚、攻击等），确保技能状态干净终止
        var playerSkill = GetComponent<PlayerSkillComponent>();
        if (playerSkill != null)
        {
            playerSkill.StopAndCleanup(true, false);
            playerSkill.ForceSuppressAttackLayer(); // ★ P9: 立即让攻击层让位，避免与受击层权重混合
        }
        else
        {
            var enemySkill = GetComponent<EnemySkillComponent>();
            if (enemySkill != null)
            {
                enemySkill.StopAndCleanup();
                enemySkill.ForceSuppressAttackLayer();
            }
        }

        float duration = GetHitDuration(_currentHitStrength); // 仅作为超时兜底

        // ★ 新版 HitStop：使用 HitStopController 独立卡肉（不冻结全局Time.timeScale）
        var hitStop = GetComponent<HitStopController>();
        if (hitStop != null && hit?.attackData != null)
        {
            hitStop.ApplyVictimHitStop(hit.attackData.forceType);
            // 也给攻击者一点卡肉（★ 修复：旧代码误用 hit.hitObject（受击方自己的碰撞体）给受击方叠双重卡肉）
            if (hit.attackerRoot != null)
            {
                var attackerHitStop = hit.attackerRoot.GetComponent<HitStopController>();
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
        // ★ 免疫：反馈层任何异常（粒子/材质等）都不得打断受击流程，否则会卡死 isHitting/动画层
        var feedbackMgr = GetComponent<HitFeedbackManager>();
        if (feedbackMgr != null && hit?.attackData != null)
        {
            try
            {
                Vector3 hitPoint = hit.hitPoint != Vector3.zero ? hit.hitPoint : transform.position;
                Vector3 attackDir = hit.GetForceDirection();
                feedbackMgr.PlayHitFeedback(hit.attackData.forceType, hitPoint, attackDir);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"HitReactionController: PlayHitFeedback threw (已忽略，不影响受击流程): {e.Message}", this);
            }
        }

        // 播动画 — 等动画播完才解除硬直
        _hitLayer.SetWeight(1f);
        _baseLayer.StartFade(0f, 0.1f); // 淡出基础层，让受击动画完全覆盖
        int dir4 = Resolve4Direction(hit);
        string animName = Compose4DirAnimation(_currentHitStrength, dir4);
        var hitState = PlayHitAnimation(animName);

        // 相机震动
        if (_impulseSource != null)
            _impulseSource.GenerateImpulseWithVelocity(hit.GetForceDirection());

        // 击退 / 击飞
        if (hit.attackData.launchHeight > 0.01f)
        {
            // ★ P10: 击飞 —— 完整接管 水平击退+垂直上抛+滞空姿态+落地受身
            yield return StartCoroutine(ApplyLaunchSequence(hit));
        }
        else
        {
            yield return StartCoroutine(ApplyKnockbackForce(hit));
        }

        // 等受击动画播完（或超时兜底）
        float waited = 0f;
        while (hitState != null && hitState.IsPlaying && waited < duration * 1.1f)
        {
            waited += Time.deltaTime;
            yield return null;
        }
    }

    /// <summary>受击结束：恢复 AI / 玩家状态机（尽力而为，不抛异常）</summary>
    private void ResumeBehaviorAndState()
    {
        var behaviorController = GetComponent<Parryable.IBehaviorController>();
        if (behaviorController != null)
        {
            try { behaviorController.ResumeBehavior(); }
            catch (System.Exception e) { Debug.LogWarning($"HitReactionController: ResumeBehavior threw: {e}"); }
        }
        var playerModel = GetComponent<PlayerModel>();
        if (playerModel != null)
        {
            try
            {
                var ts = playerModel.ts;
                if (ts != null && ts.HasTarget)
                    playerModel.ChangePlayerState(PlayerState.aim);
                else if (PlayerController.Instance != null && PlayerController.Instance.isGround)
                    playerModel.ChangePlayerState(PlayerState.ground);
                else
                    playerModel.ChangePlayerState(PlayerState.sky);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"HitReactionController: 状态恢复 threw: {e}");
            }
        }
    }

    private IEnumerator ApplyKnockbackForce(AttackEvent hit)
    {
        // Guard: require attackData and sufficient force
        if (hit?.attackData == null || hit.attackData.forceType < AttackForceType.Medium)
            yield break;

        if (cc == null)
            yield break;

        // ★ P3: 击退方向改为“攻击者 → 受击者”（水平投影），稳定可控；
        //    不再依赖武器挥动轨迹（轨迹随动画摆动，方向不稳定）。
        Vector3 dir = GetKnockbackDirection(hit);
        float force = GetKnockbackForce(hit.attackData.forceType, hit.attackData.hitForce);
        if (force <= 0f || dir.sqrMagnitude < 0.0001f)
            yield break;

        float timer = 0f;
        while (timer < knockbackDuration)
        {
            float curve = (knockbackCurve != null) ? knockbackCurve.Evaluate(timer / knockbackDuration) : 1f;
            cc.Move(dir * force * curve * Time.deltaTime);
            timer += Time.deltaTime;
            yield return null;
        }
    }

    /// <summary>击退方向：攻击者位置 → 受击者位置（水平投影），回退到攻击轨迹反方向/背后</summary>
    private Vector3 GetKnockbackDirection(AttackEvent hit)
    {
        // 优先用攻击者根节点（由 MeleeWeapon/形状判定写入 hit.attackerRoot）
        if (hit != null && hit.attackerRoot != null)
        {
            Vector3 dir = transform.position - hit.attackerRoot.transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.0001f)
                return dir.normalized;
        }

        // Fallback 1：攻击判定轨迹方向的反向
        if (hit != null)
        {
            Vector3 attackDir = hit.GetForceDirection();
            attackDir.y = 0f;
            if (attackDir.sqrMagnitude > 0.0001f)
                return -attackDir.normalized;
        }

        // Fallback 2：角色背后
        return -tf.forward;
    }

    /// <summary>击退力度：AttackData.hitForce > 0 时以数据为准，否则用档位默认（m/s）</summary>
    private float GetKnockbackForce(AttackForceType type, float dataForce)
    {
        if (dataForce > 0f) return dataForce;
        return type switch
        {
            AttackForceType.Medium => knockbackSpeedMedium,
            AttackForceType.Heavy => knockbackSpeedHeavy,
            AttackForceType.Blow => knockbackSpeedBlow,
            _ => 0f
        };
    }

    // ================================================================
    // ★ P10: 击飞物理（上抛 + 水平击退 + 重力回落 + 滞空/落地动画）
    // ================================================================

    private IEnumerator ApplyLaunchSequence(AttackEvent hit)
    {
        if (hit?.attackData == null || cc == null) yield break;

        _isLaunched = true;
        // 垂直初速：由目标击飞高度反推 v = sqrt(-2·g·h)
        _launchVelocity = Mathf.Sqrt(Mathf.Abs(-2f * launchGravity * hit.attackData.launchHeight));

        Vector3 hDir = GetKnockbackDirection(hit);
        float hForce = GetKnockbackForce(hit.attackData.forceType, hit.attackData.hitForce) * 0.5f; // 水平分量减半，垂直为主

        // ★ 最低滞空时间：资产级 minAirTime > 0 优先，否则用全局默认——保证浮空连招窗口
        float minAirTime = hit.attackData.minAirTime > 0f
            ? hit.attackData.minAirTime
            : minLaunchAirTime;

        // 滞空姿态（若动画集配置了 Air）
        PlayLaunchAnimation(airAnimationName);

        float elapsed = 0f;
        const float maxDuration = 3f; // 安全网：最多滞空 3 秒
        while (elapsed < maxDuration)
        {
            float curve = (knockbackCurve != null) ? knockbackCurve.Evaluate(Mathf.Clamp01(elapsed / knockbackDuration)) : 1f;
            _launchVelocity += launchGravity * Time.deltaTime;
            cc.Move((hDir * hForce * curve + Vector3.up * _launchVelocity) * Time.deltaTime);
            elapsed += Time.deltaTime;

            // 落地：开始下落且触地，且已满足最低滞空时间（否则继续浮空，延长连招窗口）
            if (_launchVelocity < 0f && cc.isGrounded && elapsed >= minAirTime)
                break;
            yield return null;
        }

        _isLaunched = false;
        _launchVelocity = 0f;

        // 落地受身动画并等待播完
        // ★ 玩家被击飞时跳过：受击后应快速恢复操作（否则击飞期间既不能攻击也不能移动，体验断裂）
        //   敌人保留完整的倒地+起身演出（这是玩家的连招窗口）
        if (GetComponent<PlayerModel>() == null)
            yield return StartCoroutine(PlayLaunchAnimationAndWait(landAnimationName));
    }

    private void PlayLaunchAnimation(string name)
    {
        if (animationSet == null || string.IsNullOrEmpty(name)) return;
        var clip = animationSet.GetClip(name);
        if (clip != null)
        {
            var state = _hitLayer.Play(clip, 0.1f, FadeMode.FromStart);
            state.TimeD = 0; // ★ 从头播放（避免复用被中断的旧状态）
        }
    }

    private IEnumerator PlayLaunchAnimationAndWait(string name)
    {
        if (animationSet == null || string.IsNullOrEmpty(name)) yield break;
        var clip = animationSet.GetClip(name);
        if (clip == null) yield break;

        var state = _hitLayer.Play(clip, 0.1f, FadeMode.FromStart);
        state.TimeD = 0; // ★ 从头播放
        float waited = 0f;
        while (state != null && state.IsPlaying && waited < 1.2f)
        {
            waited += Time.deltaTime;
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
        // ★ P3: 受击方向优先用“攻击者→受击者”向量（稳定），回退到武器轨迹方向
        Vector3 attackDir;
        if (hit != null && hit.attackerRoot != null)
        {
            attackDir = transform.position - hit.attackerRoot.transform.position;
            attackDir.y = 0f;
            if (attackDir.sqrMagnitude < 0.0001f)
                attackDir = hit.GetForceDirection();
        }
        else
        {
            attackDir = hit != null ? hit.GetForceDirection() : Vector3.forward;
        }
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

    private AnimancerState PlayHitAnimation(string name)
    {
        if (animationSet == null) return null;
        var clip = animationSet.GetClip(name);
        if (clip != null)
        {
            var state = _hitLayer.Play(clip, 0.1f, FadeMode.FromStart);
            state.TimeD = 0; // ★ 从头播放（避免复用被中断的旧受击状态）
            return state;
        }
        return null;
    }
}

public enum HitStrength { Light, Medium, Heavy, Blow }