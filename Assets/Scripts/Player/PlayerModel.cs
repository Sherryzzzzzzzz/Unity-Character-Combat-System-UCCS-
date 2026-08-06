using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Animancer;

public enum PlayerAnimationState
{
    idle,move,jump,fall,aim
}

public enum PlayerState
{
    ground,sky,attack,aim,guard
}

public class PlayerModel : MonoBehaviour, IStateOwner, Parryable.IBehaviorController, UCCS.IDefenseStateProvider, UCCS.IPlayerMarker
{
    private StateMachine animationStateMachine;
    private StateMachine playerStateMachine;

#if UNITY_EDITOR
    public StateMachine DebugPlayerStateMachine => playerStateMachine;
    public StateMachine DebugAnimationStateMachine => animationStateMachine;
#endif

    public PlayerAnimationState _PlayerAnimationState{ get; private set; }
    public PlayerState _PlayerState{ get; private set; }
    [SerializeField] 
    public AnimancerComponent animancer;
    
    public Animator animator;

    public PlayerAnimationSet AnimationSet;

    public PlayerSkillComponent pac;
    public TargetingSystem ts;
    public MeleeWeapon wp;
    public AttributeSet attributeSet;

    // 技能动画资源
    public SkillTimelineAsset lightStart;//轻攻击起手式
    public SkillTimelineAsset lightSkyStart;//空中轻攻击起手式
    public SkillTimelineAsset heavyStart;//重攻击起手式
    public SkillTimelineAsset combatArtStart;//战技起手式
    public SkillTimelineAsset defendStart;//防御起手式
    public SkillTimelineAsset dodgeF;
    public SkillTimelineAsset dodgeB;
    public SkillTimelineAsset dodgeR;
    public SkillTimelineAsset dodgeL;

    [Header("Guard")]
    public ClipTransition guardAnimation;
    public ClipTransition guardEndAnimation;
    public GameplayEffect guardEffect;
    
    public SkillTimelineAsset currentSkill;
    public bool isAttacking = false;
    private bool _behaviorDisabled = false;
    
    
    #region 重力相关
    public float gravity = -9.8f;
    public float jumpHeight = 2f;
    [HideInInspector]
    public Vector3 gravityVector;
    public bool stopGravity = false;
    #endregion
    
    public CharacterController cc { get;private set; }
    public bool isComboChain = false;
    
    public float walkSpeed = 3f;
    public float runSpeed = 10f;
    public TagComponent tagComponent;
    public GameplayTagSO LightAttackInputTag;
    public GameplayTagSO HeavyAttackInputTag;
    public GameplayTagSO CombatArtInputTag;
    public GameplayTagSO DefendInputTag;
    
    public float detectRadius = 0.5f; // 检测半径
    public LayerMask enemyLayer;     // 敌人层
    public Transform nearestEnemy;   // 当前最近的敌人
    public bool isHitting = false;
    public bool isDefending = false;
    bool UCCS.IDefenseStateProvider.IsDefending => isDefending;
    public bool isDodging = false;
    public bool isAiming = false;

    [SerializeField] private float maxHitStateDuration = 3f;
    private float _hitStateTimer;

    private HitReactionController _hitReaction; // ★ P10: 击飞期间跳过自身重力积分

    // 缓存常用引用
    private HurtBoxManager _hbm;
    private float _detectTimer;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        animancer = GetComponent<AnimancerComponent>();
        animationStateMachine = new StateMachine(this);
        playerStateMachine = new StateMachine(this);
        cc = GetComponent<CharacterController>();
        pac = GetComponent<PlayerSkillComponent>();
        tagComponent = GetComponent<TagComponent>();
        ts = GetComponent<TargetingSystem>();
        wp.Init(GetComponent<AbilitySystemComponent>());
        attributeSet = GetComponent<AttributeSet>();
        _hbm = GetComponent<HurtBoxManager>();
        _hitReaction = GetComponent<HitReactionController>();
    }

    void Start()
    {
        ChangeAnimationState(PlayerAnimationState.idle);
        ChangePlayerState(PlayerState.ground);
    }

    void Update()
    {
        // Always sample sensors so we can decide whether to resume behavior
        // 降频检测敌人（不需要每帧跑 Physics.OverlapSphere）
        _detectTimer += Time.deltaTime;
        if (_detectTimer >= 0.3f)
        {
            _detectTimer = 0f;
            DetectNearestEnemy();
        }
        isAttacking = (pac != null && pac.isPlaying) || _PlayerState == PlayerState.guard;
        isHitting = (_hbm != null) && _hbm.isHitting;

        // 受击超时安全网
        if (isHitting)
        {
            _hitStateTimer += Time.deltaTime;
            if (_hitStateTimer >= maxHitStateDuration)
            {
#if UNITY_EDITOR
                Debug.LogWarning($"PlayerModel: isHitting 超时 ({_hitStateTimer:F1}s >= {maxHitStateDuration}s)，强制恢复!", this);
#endif
                if (_hbm != null)
                    _hbm.ForceResetHitState();
                isHitting = false;
                _behaviorDisabled = false;
                _hitStateTimer = 0f;
            }
        }
        else
        {
            _hitStateTimer = 0f;
        }

        // If behavior was previously disabled, only prevent other updates while still in hit state.
        if (_behaviorDisabled)
        {
            if (!isHitting)
            {
                // Hit reaction finished but behavior was left disabled: resume now
                _behaviorDisabled = false;
                Debug.Log("PlayerModel: behavior resumed automatically after hit finished.", this);

                // Restore to correct state based on current context
                if (_PlayerState != PlayerState.guard)
                {
                    if (ts != null && ts.HasTarget)
                        ChangePlayerState(PlayerState.aim);
                    else if (PlayerController.Instance.isGround)
                        ChangePlayerState(PlayerState.ground);
                    else
                        ChangePlayerState(PlayerState.sky);
                }
            }
            else
            {
                // Still in hit state: keep behaviors disabled
                return;
            }
        }

        // Normal update work continues below
        // (previously nothing else was here, but leaving placeholder for clarity)
    }

    public void ChangeAnimationState(PlayerAnimationState animationState)
    {
        switch (animationState)
        {
            case PlayerAnimationState.idle:
                animationStateMachine.EnterState<IdleState>();
                break;
            case PlayerAnimationState.move:
                animationStateMachine.EnterState<MoveState>();
                break;
            case PlayerAnimationState.jump:
                animationStateMachine.EnterState<JumpState>();
                break;
            case PlayerAnimationState.fall:
                animationStateMachine.EnterState<FallState>();
                break;
            case PlayerAnimationState.aim:
                animationStateMachine.EnterState<AimState>();
                break;
        }
        _PlayerAnimationState = animationState;
    }

    public void ChangePlayerState(PlayerState newState, object parameter = null)
    {
        switch (newState)
        {
            case PlayerState.ground:
                playerStateMachine.EnterState<PlayerGroundState>(parameter);
                break;
            case PlayerState.sky:
                playerStateMachine.EnterState<PlayerSkyState>(parameter);
                break;
            case PlayerState.attack:
                playerStateMachine.EnterState<PlayerAttackState>(parameter);
                break;
            case PlayerState.aim:
                playerStateMachine.EnterState<PlayerGroundAimState>(parameter);
                break;
            case PlayerState.guard:
                playerStateMachine.EnterState<PlayerGuardState>(parameter);
                break;
        }
        _PlayerState = newState;
    }

    public void PlaySkill(SkillTimelineAsset skill = null)
    {
        if(isHitting) return;
        if(skill != null)
            pac.PlaySkill(skill);
    }
    
    void OnAnimatorMove()
    {
        if (animator == null) return;
        if (cc == null || !cc.enabled || stopGravity) return;

        // ★ P10: 被击飞期间，垂直运动由 HitReactionController 的击飞物理接管
        if (_hitReaction != null && _hitReaction.IsLaunched)
            return;

        bool isGrounded = PlayerController.Instance.isGround;
        float groundDistance = PlayerController.Instance.groundDistance;

        if (isGrounded && gravityVector.y < 0f)
        {
            // 保持角色紧贴地面
            gravityVector.y = gravity;
        }
        else
        {
            // 累积重力
            gravityVector.y += gravity * Time.deltaTime;
        }

        // 应用重力位移
        cc.Move(gravityVector * Time.deltaTime);
    }
    
    private void DetectNearestEnemy()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, detectRadius, enemyLayer);

        Transform closest = null;
        float minDistance = Mathf.Infinity;

        foreach (Collider hit in hits)
        {
            // 可以加个过滤条件，比如排除自己或非敌人
            if (hit.transform == transform) continue;

            float distance = Vector3.Distance(transform.position, hit.transform.position);
            if (distance < minDistance)
            {
                minDistance = distance;
                closest = hit.transform;
            }
        }

        nearestEnemy = closest;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, detectRadius);
    }
    
    public void InterruptAndDisableBehavior()
    {
        _behaviorDisabled = true;
    }

    public void ResumeBehavior()
    {
        _behaviorDisabled = false;
    }
}
