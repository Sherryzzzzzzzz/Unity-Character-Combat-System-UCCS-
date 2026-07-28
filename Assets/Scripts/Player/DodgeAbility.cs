using System.Collections;
using UnityEngine;
using Animancer;

/// <summary>
/// DodgeAbility — 完美闪避检测 + 时间减速特效
///
/// 检测逻辑：玩家闪避时，扫描附近敌人是否有激活的攻击状态（EnemySkillComponent.IsPlaying）。
/// 无需敌人显式打开判定窗口。
/// </summary>
public class DodgeAbility : MonoBehaviour
{
    [Header("Perfect Dodge Settings")]
    [Tooltip("完美闪避检测半径")]
    public float detectionRadius = 4f;
    [Tooltip("敌人层（用于检测附近敌人）")]
    public LayerMask enemyLayer;

    [Tooltip("完美闪避宽限期（秒）— 敌人在此时间内攻击过即可触发完美闪避")]
    public float recentAttackWindow = 0.8f;

    [Tooltip("完美闪避 Tag 持续时间")]
    public float perfectDodgeTagDuration = 0.8f;

    [Tooltip("GameplayTag to add on perfect dodge")]
    public GameplayTagSO perfectDodgeTag;

    [Tooltip("Optional GameplayEffect for perfect dodge")]
    public GameplayEffect perfectDodgeSelfEffect;

    [Header("Time Slow Effect")]
    [Tooltip("完美闪避时动画速度降到多少（0.15 = 15%速度）")]
    public float slowMotionSpeed = 0.15f;
    [Tooltip("时间减速持续秒数")]
    public float slowMotionDuration = 0.5f;
    [Tooltip("减速恢复的渐变时间")]
    public float slowMotionRecoveryTime = 0.3f;

    [Header("Cooldown")]
    [Tooltip("完美闪避冷却时间（防止连发）")]
    public float perfectDodgeCooldown = 1.5f;

    [Header("Audio")]
    [Tooltip("完美闪避音效")]
    public AudioClip perfectDodgeSound;

    private float _lastPerfectDodgeTime = -999f;
    private Coroutine _tagRemovalCoroutine;
    private Coroutine _slowMotionCoroutine;

    private TagComponent _tagComponent;
    private AbilitySystemComponent _asc;
    private AnimancerComponent _animancer;
    private AudioSource _audioSource;

    private void Awake()
    {
        _tagComponent = GetComponent<TagComponent>();
        _asc = GetComponent<AbilitySystemComponent>();
        _animancer = GetComponent<AnimancerComponent>();
        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null)
            _audioSource = gameObject.AddComponent<AudioSource>();

        if (_tagComponent == null)
            Debug.LogWarning($"{gameObject.name}: DodgeAbility requires a TagComponent", this);
    }

    /// <summary>
    /// 玩家按下闪避时调用。返回值表示是否是完美闪避。
    /// </summary>
    public bool AttemptDodge()
    {
        // 冷却检查
        if (Time.time - _lastPerfectDodgeTime < perfectDodgeCooldown)
        {
            Debug.Log($"[DodgeAbility] 冷却中: {Time.time - _lastPerfectDodgeTime:F2}s / {perfectDodgeCooldown}s");
            return false;
        }

        Debug.Log($"[DodgeAbility] 开始检测... enemyLayer={enemyLayer.value}, radius={detectionRadius}");

        if (IsEnemyAttackInRange())
        {
            _lastPerfectDodgeTime = Time.time;
            OnPerfectDodge();
            return true;
        }
        Debug.Log("[DodgeAbility] 附近没有正在攻击的敌人");
        return false;
    }

    /// <summary>
    /// 检测范围内是否有敌人正在或近期执行过攻击技能（宽限期检测）
    /// </summary>
    private bool IsEnemyAttackInRange()
    {
        if (enemyLayer.value == 0)
            enemyLayer = LayerMask.GetMask("Enemy");

        Collider[] hits = Physics.OverlapSphere(transform.position, detectionRadius, enemyLayer);
        Debug.Log($"[DodgeAbility] OverlapSphere 找到 {hits.Length} 个敌人 (layer={enemyLayer.value})");

        foreach (var hit in hits)
        {
            var esc = hit.GetComponentInParent<EnemySkillComponent>();
            if (esc == null)
            {
                Debug.Log($"[DodgeAbility] {hit.name}: 无EnemySkillComponent");
                continue;
            }

            Debug.Log($"[DodgeAbility] {esc.name}: IsPlaying={esc.IsPlaying}, HasActiveAttack={esc.HasActiveAttackEvents}, RecentAttack={esc.HasRecentAttack(recentAttackWindow)}(window={recentAttackWindow}s, lastEnd={esc.LastAttackEndTime:F1})");

            // 1) 敌人正在攻击
            if (esc.IsPlaying && esc.HasActiveAttackEvents)
            {
                Debug.Log("[DodgeAbility] ✅ 完美闪避! (敌人攻击中)");
                return true;
            }

            // 2) ★ 宽限期：敌人近期攻击过（刚收招也能触发完美闪避）
            if (esc.HasRecentAttack(recentAttackWindow))
            {
                Debug.Log($"[DodgeAbility] ✅ 完美闪避! (宽限期: {Time.time - esc.LastAttackEndTime:F2}s前攻击)");
                return true;
            }
        }
        return false;
    }

    private void OnPerfectDodge()
    {
        Debug.Log($"{gameObject.name}: Perfect Dodge!");

        // 0) 音效
        if (perfectDodgeSound != null && _audioSource != null)
            _audioSource.PlayOneShot(perfectDodgeSound);

        // 1) 时间减速（动画层减速，不影响全局 Time.timeScale）
        if (_slowMotionCoroutine != null)
            StopCoroutine(_slowMotionCoroutine);
        _slowMotionCoroutine = StartCoroutine(SlowMotionRoutine());

        // 2) 相机 FOV Kick
        if (CameraImpactEffects.Instance != null)
            CameraImpactEffects.Instance.ApplyFOVKick(AttackForceType.Medium);

        // 3) 完美闪避 VFX
        var pool = UnityEngine.Object.FindFirstObjectByType<GlobalVFXPool>();
        if (pool != null)
            pool.SpawnPerfectDodgeVFX(transform.position);

        // 4) 防御者 Tag
        if (_tagComponent != null && perfectDodgeTag != null)
        {
            _tagComponent.AddTag(perfectDodgeTag);
            if (_tagRemovalCoroutine != null)
                StopCoroutine(_tagRemovalCoroutine);
            _tagRemovalCoroutine = StartCoroutine(RemoveTagAfterDelay(perfectDodgeTagDuration));
        }

        // 5) 可选 GameplayEffect
        if (perfectDodgeSelfEffect != null && _asc != null)
        {
            int handle = _asc.ApplyGameplayEffect(perfectDodgeSelfEffect, _asc);
            Debug.Log($"{gameObject.name}: Applied perfectDodgeSelfEffect handle={handle}");
        }
    }

    /// <summary>
    /// 动画层减速 + 渐变恢复
    /// </summary>
    private IEnumerator SlowMotionRoutine()
    {
        var savedSpeeds = new System.Collections.Generic.List<(AnimancerLayer layer, float speed)>();
        if (_animancer != null)
        {
            for (int i = 0; i < _animancer.Layers.Count; i++)
            {
                var layer = _animancer.Layers[i];
                var state = layer.CurrentState;
                if (state != null && state.Speed > 0.01f)
                {
                    savedSpeeds.Add((layer, state.Speed));
                    state.Speed = slowMotionSpeed;
                }
            }
        }

        yield return new WaitForSecondsRealtime(slowMotionDuration);

        float elapsed = 0f;
        while (elapsed < slowMotionRecoveryTime)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / slowMotionRecoveryTime;
            float currentTarget = Mathf.Lerp(slowMotionSpeed, 1f, t);
            foreach (var (layer, _) in savedSpeeds)
            {
                var state = layer.CurrentState;
                if (state != null)
                    state.Speed = currentTarget;
            }
            yield return null;
        }

        foreach (var (layer, originalSpeed) in savedSpeeds)
        {
            var state = layer.CurrentState;
            if (state != null)
                state.Speed = originalSpeed;
        }

        _slowMotionCoroutine = null;
    }

    private IEnumerator RemoveTagAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (_tagComponent != null && perfectDodgeTag != null)
            _tagComponent.RemoveTag(perfectDodgeTag);
        _tagRemovalCoroutine = null;
    }
}
