using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyModel : MonoBehaviour, IStateOwner, Parryable.IBehaviorController, UCCS.IMovementController
{
    private CharacterController cc;
    public float speed;
    private StateMachine stateMachine;
    private GameObject _player;
    public Vector2 moveDir;
    public float angle;
    public ExpandableAnimationSet AnimationSet;
    public bool isRunning;
    public float rotateSpeed = 5f;
    public bool isHitting = false;
    public BTreeRunner bTreeRunner;

    // 缓存常用引用，避免每帧 GetComponent
    private HurtBoxManager _hbm;
    private AttributeSet _attributes;

    [Header("硬直恢复延迟")]
    [Tooltip("受击结束后额外等待多久才恢复 AI")]
    public float staggerRecoveryDelay = 0.4f;
    private float _staggerRecoveryTimer;
    private bool _wasHitting;

    [Header("死亡")]
    [Tooltip("死亡后延迟销毁（秒），0=不销毁")]
    public float deathDestroyDelay = 3f;

    // ── 行为树移动命令 ──
    /// <summary>BTA_MoveTo 设置的移动目标，EnemyModel.Update 每帧消费</summary>
    public Vector3? moveCommandTarget;
    public float moveCommandStopDist = 1f;

    // ── IMovementController 实现 ──
    public Vector3? MoveTarget { get => moveCommandTarget; set => moveCommandTarget = value; }
    public float MoveStopDistance { get => moveCommandStopDist; set => moveCommandStopDist = value; }
    float UCCS.IMovementController.Speed => speed;
    bool UCCS.IMovementController.IsMoving => moveCommandTarget.HasValue;
    public void MoveTowards(Vector3 target, float stopDistance)
    {
        moveCommandTarget = target;
        moveCommandStopDist = stopDistance;
    }
    public void StopMoving()
    {
        moveCommandTarget = null;
        moveDir = Vector2.zero;
    }

    private EnemyAnimationDriver _animDriver;
    private EnemySkillComponent _skillComp;

    private void Awake()
    {
        cc = GetComponent<CharacterController>();
        _player = GameObject.FindGameObjectWithTag("Player");
        bTreeRunner = GetComponent<BTreeRunner>();
        _hbm = GetComponent<HurtBoxManager>();
        _attributes = GetComponent<AttributeSet>();
        _animDriver = GetComponent<EnemyAnimationDriver>();
        _skillComp = GetComponent<EnemySkillComponent>();

        if (_attributes != null)
            _attributes.OnDeath += OnDeath;
    }

    void Start()
    {
        // Start 时再试一次（Awake 可能 player 还没生成）
        if (_player == null)
            _player = GameObject.FindGameObjectWithTag("Player");

        IgnorePlayerCollision();
    }

    void IgnorePlayerCollision()
    {
        if (_player == null || cc == null) return;
        var playerCC = _player.GetComponent<CharacterController>();
        if (playerCC != null)
        {
            Physics.IgnoreCollision(cc, playerCC, true);
            // 也从 player 侧忽略
            var playerCols = _player.GetComponentsInChildren<Collider>();
            var enemyCols = GetComponentsInChildren<Collider>();
            foreach (var pc in playerCols)
                foreach (var ec in enemyCols)
                    Physics.IgnoreCollision(pc, ec, true);
        }
    }

    private void OnDeath()
    {
        Debug.Log($"[EnemyDeath] {name} 死亡!");

        // 立即禁用 CharacterController，阻止行为树节点继续 Move
        if (cc != null)
            cc.enabled = false;

        // 关闭行为树组件（禁用整个组件，防止 mid-tick 残留调用）
        if (bTreeRunner != null)
            bTreeRunner.enabled = false;

        // 禁用所有碰撞体（不可再被攻击或碰撞）
        foreach (var col in GetComponentsInChildren<Collider>())
            col.enabled = false;

        // 停止当前技能播放
        var skill = GetComponent<EnemySkillComponent>();
        if (skill != null)
        {
            if (skill.IsPlaying)
                skill.StopAndCleanup();
            skill.enabled = false;
        }

        // 清除玩家锁定
        var playerTS = FindFirstObjectByType<TargetingSystem>();
        if (playerTS != null && playerTS.HasTarget && playerTS.CurrentTarget != null)
        {
            if (playerTS.CurrentTarget.IsChildOf(transform) || playerTS.CurrentTarget == transform)
            {
                playerTS.ToggleLockOn();
            }
        }

        // 播放死亡动画
        float deathDuration = 2f; // 兜底时间
        if (_animDriver != null)
        {
            _animDriver.ChangeState(EnemyAnimationState.Death);
            // 获取死亡动画实际时长
            var deathClip = _animDriver.GetComponent<EnemyAnimationData>()?.DeathClip;
            if (deathClip?.Clip != null)
                deathDuration = deathClip.Clip.length;
        }

        // Boss血条隐藏
        var bossBar = FindFirstObjectByType<BossHealthBar>();
        if (bossBar != null)
            bossBar.gameObject.SetActive(false);

        // 协程：等待死亡动画播放完毕再销毁
        StartCoroutine(DestroyAfterDeathAnim(deathDuration));
    }

    private System.Collections.IEnumerator DestroyAfterDeathAnim(float delay)
    {
        yield return new WaitForSeconds(delay);
        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if (_attributes != null)
            _attributes.OnDeath -= OnDeath;
    }

    private void Update()
    {
        isHitting = _hbm != null && _hbm.isHitting;

        // ── 硬直恢复延迟：受击结束后不立刻中断过渡 ──
        if (_wasHitting && !isHitting)
        {
            _staggerRecoveryTimer = staggerRecoveryDelay;
        }

        if (isHitting)
        {
            _staggerRecoveryTimer = 0f;
            moveCommandTarget = null; // 清移动命令，防止硬直中位移
            moveDir = Vector2.zero;
            if (bTreeRunner != null && bTreeRunner.IsRunning) bTreeRunner.Pause();
            _wasHitting = true;
            return;
        }

        // 恢复延迟倒计时中 → 保持 AI 暂停
        if (_staggerRecoveryTimer > 0f)
        {
            _staggerRecoveryTimer -= Time.deltaTime;
            _wasHitting = false;
            return;
        }

        // 恢复 AI
        if (bTreeRunner != null && !bTreeRunner.IsRunning) bTreeRunner.Play();
        _wasHitting = false;

        // ── 帧级移动：BT 设目标，此处执行。动画侧根据 moveDir.magnitude 选跑/走 ──
        if (moveCommandTarget.HasValue && cc != null)
        {
            float dx = moveCommandTarget.Value.x - transform.position.x;
            float dz = moveCommandTarget.Value.z - transform.position.z;
            float sqr = dx * dx + dz * dz;

            if (sqr > moveCommandStopDist * moveCommandStopDist)
            {
                float inv = 1f / Mathf.Sqrt(sqr);
                cc.Move(new Vector3(dx * inv * speed * Time.deltaTime, -9.81f * Time.deltaTime, dz * inv * speed * Time.deltaTime));
                // moveDir 单位方向 × speed — 动画据此判断跑/走
                moveDir = new Vector2(dx * inv * speed, dz * inv * speed);
            }
            else
            {
                moveDir = Vector2.zero;
            }
        }

        // ── 水平分离 ──
        if (_player != null && cc != null)
        {
            float dx = _player.transform.position.x - transform.position.x;
            float dz = _player.transform.position.z - transform.position.z;
            float hDist = Mathf.Sqrt(dx * dx + dz * dz);
            float minDist = cc.radius + 0.3f;

            if (hDist < minDist && hDist > 0.001f)
            {
                float inv = 1f / hDist;
                float overlap = minDist - hDist;
                cc.Move(new Vector3(-dx * inv * overlap * 0.2f, 0f, -dz * inv * overlap * 0.2f));
            }
        }

        // ── 持续下压力 ──
        if (cc != null && !cc.isGrounded)
        {
            cc.Move(new Vector3(0, -9.81f * Time.deltaTime, 0));
        }

        // 转向玩家（仅在非技能播放时，避免攻击过程中频繁旋转）
        if (moveDir.sqrMagnitude < 0.01f && _player != null && (_skillComp == null || !_skillComp.IsPlaying))
        {
            Vector3 toTarget = _player.transform.position - transform.position;
            toTarget.y = 0;
            if (toTarget.sqrMagnitude > 0.16f) // 距离 > 0.4m 才转，太近不转
            {
                Quaternion targetRot = Quaternion.LookRotation(toTarget);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * rotateSpeed * 0.5f); // 半速旋转
            }
        }
        angle = Vector3.SignedAngle(new Vector3(moveDir.x, 0, moveDir.y), transform.forward, Vector3.up);
    }

    public void InterruptAndDisableBehavior()
    {
        if (bTreeRunner != null)
        {
            bTreeRunner.Pause();
        }
    }

    public void ResumeBehavior()
    {
        if (bTreeRunner != null)
        {
            bTreeRunner.Play();
        }
    }
}
