using System.Collections;
using UnityEngine;

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

    [Header("Time Slow Effect (由 HurtBoxManager.HandlePerfectDodge 触发)")]
    [Tooltip("完美闪避时动画速度降到多少（0.15 = 15%速度）")]
    public float slowMotionSpeed = 0.15f;
    [Tooltip("时间减速持续秒数")]
    public float slowMotionDuration = 0.5f;
    [Tooltip("减速恢复的渐变时间")]
    public float slowMotionRecoveryTime = 0.3f;

    [Header("Audio")]
    [Tooltip("完美闪避音效")]
    public AudioClip perfectDodgeSound;

    private Coroutine _tagRemovalCoroutine;

    private TagComponent _tagComponent;
    private AudioSource _audioSource;

    private void Awake()
    {
        _tagComponent = GetComponent<TagComponent>();
        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null)
            _audioSource = gameObject.AddComponent<AudioSource>();

        if (_tagComponent == null)
            Debug.LogWarning($"{gameObject.name}: DodgeAbility requires a TagComponent", this);
    }

    /// <summary>
    /// 玩家按下闪避时调用。
    /// ★ 改为：闪避即授予完美闪避候选标签（受击瞬间由 HurtBoxManager 判定），
    ///   不再依赖"闪避按下瞬间"的敌人扫描——否则敌人攻击判定帧在闪避开始之后时无法触发。
    /// </summary>
    public bool AttemptDodge()
    {
        GrantPerfectDodgeTag();

        // 即时反馈（可选增强）：敌人正在/近期攻击 → 提前播放完美闪避音效预告
        if (perfectDodgeSound != null && _audioSource != null && IsEnemyAttackInRange())
            _audioSource.PlayOneShot(perfectDodgeSound);

        return true;
    }

    /// <summary>
    /// 授予完美闪避候选标签（持续 perfectDodgeTagDuration 秒）。
    /// 该窗口内被敌人攻击命中 = 完美闪避（慢动作 + 惩罚由 HurtBoxManager.HandlePerfectDodge 执行）。
    /// </summary>
    private void GrantPerfectDodgeTag()
    {
        if (_tagComponent != null && perfectDodgeTag != null)
        {
            _tagComponent.AddTag(perfectDodgeTag);
            if (_tagRemovalCoroutine != null)
                StopCoroutine(_tagRemovalCoroutine);
            _tagRemovalCoroutine = StartCoroutine(RemoveTagAfterDelay(perfectDodgeTagDuration));
        }
    }

    /// <summary>
    /// 检测范围内是否有敌人正在或近期执行过攻击技能（宽限期检测）
    /// </summary>
    private bool IsEnemyAttackInRange()
    {
        if (enemyLayer.value == 0)
            enemyLayer = LayerMask.GetMask("Enemy");

        Collider[] hits = Physics.OverlapSphere(transform.position, detectionRadius, enemyLayer);

        foreach (var hit in hits)
        {
            var esc = hit.GetComponentInParent<EnemySkillComponent>();
            if (esc == null)
                continue;

            // 1) 敌人正在攻击
            if (esc.IsPlaying && esc.HasActiveAttackEvents)
                return true;

            // 2) 宽限期：敌人近期攻击过（刚收招也能触发完美闪避）
            if (esc.HasRecentAttack(recentAttackWindow))
                return true;
        }
        return false;
    }

    private IEnumerator RemoveTagAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (_tagComponent != null && perfectDodgeTag != null)
            _tagComponent.RemoveTag(perfectDodgeTag);
        _tagRemovalCoroutine = null;
    }
}
