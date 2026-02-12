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
    ground,sky,attack,aim
}

public class PlayerModel : MonoBehaviour,IStateOwner, Parryable.IBehaviorController
{
    private StateMachine animationStateMachine;
    private StateMachine playerStateMachine;
    public PlayerAnimationState _PlayerAnimationState{ get; private set; }
    public PlayerState _PlayerState{ get; private set; }
    [SerializeField] 
    public AnimancerComponent animancer;
    
    public Animator animator;

    public PlayerAnimationSet AnimationSet;

    public PlayerSkillComponent pac;
    public TargetingSystem ts;

    // 技能动画资源
    public SkillTimelineAsset lightStart;//轻攻击起手式
    public SkillTimelineAsset lightSkyStart;//空中轻攻击起手式
    public SkillTimelineAsset heavyStart;//重攻击起手式
    public SkillTimelineAsset defendStart;//防御起手式
    public SkillTimelineAsset dodgeF;
    public SkillTimelineAsset dodgeB;
    public SkillTimelineAsset dodgeR;
    public SkillTimelineAsset dodgeL;
    
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
    public GameplayTagSO DefendInputTag;
    
    public float detectRadius = 0.5f; // 检测半径
    public LayerMask enemyLayer;     // 敌人层
    public Transform nearestEnemy;   // 当前最近的敌人
    public bool isHitting = false;
    public bool isDefending = false;
    public bool isAiming = false;

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
    }

    void Start()
    {
        ChangeAnimationState(PlayerAnimationState.idle);
        ChangePlayerState(PlayerState.ground);
    }
    
    void Update()
    {
        if (_behaviorDisabled) return;
        
        DetectNearestEnemy();
        isAttacking = pac.isPlaying;
        isHitting = GetComponent<HurtBoxManager>().isHitting;
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
        }
        _PlayerAnimationState = animationState;
    }

    public void ChangePlayerState(PlayerState newState, object parameter = null)
    {
        Debug.Log($"Changing state from {_PlayerState} to {newState}");

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
        
        if (cc != null && cc.enabled && !stopGravity)
        {
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
