using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Animancer;

public enum PlayerAnimationState
{
    idle,move,jump,fall
}

public enum PlayerState
{
    ground,sky,groundLightAttack,skyLightAttack
}

public class PlayerModel : MonoBehaviour,IStateOwner
{
    private StateMachine animationStateMachine;
    private StateMachine playerStateMachine;
    private PlayerAnimationState _PlayerAnimationState;
    private PlayerState _PlayerState;
    [SerializeField] 
    public AnimancerComponent animancer;
    
    public Animator animator;

    public PlayerAnimationSet AnimationSet;

    public PlayerAttackComponent pac;

    public SkillTimelineAsset lightStart;//轻攻击起手式
    public SkillTimelineAsset lightSkyStart;//空中轻攻击起手式
    public SkillTimelineAsset currentSkill;
    public bool isAttacking = false;
    
    
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
    
    public float detectRadius = 0.5f; // 检测半径
    public LayerMask enemyLayer;     // 敌人层
    public Transform nearestEnemy;   // 当前最近的敌人
    public bool isHitting = false;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        animancer = GetComponent<AnimancerComponent>();
        animationStateMachine = new StateMachine(this);
        playerStateMachine = new StateMachine(this);
        cc = GetComponent<CharacterController>();
        pac = GetComponent<PlayerAttackComponent>();
        tagComponent = GetComponent<TagComponent>();
    }

    void Start()
    {
        ChangeAnimationState(PlayerAnimationState.idle);
        ChangePlayerState(PlayerState.ground);
    }
    
    void Update()
    {
        DetectNearestEnemy();
        isAttacking = pac.isPlaying;
        isHitting = GetComponent<HurtBoxManager>().isHitting;

        if (isAttacking)
        {
            // 如果有最近的敌人，就朝向它
            if (nearestEnemy != null)
            {
                Vector3 lookDirection = nearestEnemy.position - transform.position;
                lookDirection.y = 0; // 保持水平
                if (lookDirection.sqrMagnitude > 0.01f)
                {
                    Quaternion targetRotation = Quaternion.LookRotation(lookDirection);
                    transform.rotation =
                        Quaternion.Slerp(transform.rotation, targetRotation, 15f * Time.deltaTime); // 用一个较快的速度转向
                }
            }
        }

        if (pac.isPlaying)
        {
            currentSkill = pac.CurrentSkill;
        }
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

    public void ChangePlayerState(PlayerState state)
    {
        Debug.Log(state.ToString());
        switch (state)
        {
            case PlayerState.ground:
                playerStateMachine.EnterState<PlayerGroundState>();
                break;
            case PlayerState.sky:
                playerStateMachine.EnterState<PlayerSkyState>();
                break;
            case PlayerState.groundLightAttack:
                playerStateMachine.EnterState<PlayerLightAttackState>();
                break;
            case PlayerState.skyLightAttack:
                playerStateMachine.EnterState<PlayerSkyLightAttackState>();
                break;
        }
        _PlayerState = state;
    }

    public void PlaySkill(SkillTimelineAsset skill = null)
    {
        
        switch (_PlayerState)
        {
            case PlayerState.ground:
                isComboChain = true;
                pac.PlaySkill(lightStart);  // ✅ 第一次播放起手式
                break;
            case PlayerState.sky:
                isComboChain = true;
                pac.PlaySkill(lightSkyStart);
                break;
        }

        currentSkill = skill ?? lightStart;
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
        
        if ((isAttacking||isHitting) && animator != null)
        {
            Vector3 deltaPosition = animator.deltaPosition;
            cc.Move(deltaPosition);
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
}
