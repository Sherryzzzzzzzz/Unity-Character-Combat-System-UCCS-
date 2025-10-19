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

    private void Awake()
    {
        animator = GetComponent<Animator>();
        animancer = GetComponent<AnimancerComponent>();
        animationStateMachine = new StateMachine(this);
        playerStateMachine = new StateMachine(this);
        cc = GetComponent<CharacterController>();
        pac = GetComponent<PlayerAttackComponent>();
    }

    void Start()
    {
        ChangeAnimationState(PlayerAnimationState.idle);
        ChangePlayerState(PlayerState.ground);
    }
    
    void Update()
    {
        isAttacking = pac.isPlaying;

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
        Debug.Log(_PlayerState);
    }

    public void PlaySkill(SkillTimelineAsset skill = null)
    {
        if (!isComboChain)
        {
            switch (_PlayerState)
            {
                case PlayerState.ground:
                    pac.PlaySkill(lightStart);  // ✅ 第一次播放起手式
                    isComboChain = true;
                    break;
                case PlayerState.sky:
                    pac.PlaySkill(lightSkyStart);
                    isComboChain = true;
                    break;
            }
            
        }
        else
        {
            pac.PlaySkill(skill ?? lightStart); // ✅ 后续由 ComboEvent 指定
        }

        currentSkill = skill ?? lightStart;
    }
    
    void OnAnimatorMove()
    {
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
        
        if (animator == null) return;
        
        Vector3 deltaPosition = animator.deltaPosition;
        cc.Move(deltaPosition);
        transform.rotation *= animator.deltaRotation;
    }
}
