using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.PlayerLoop;

public class PlayerGroundAimState : PlayerStateBase 
{
    [Header("锁敌参数")]
    [Tooltip("角色朝向锁敌目标的旋转速度")]
    public float rotationSpeed = 20f;
    [Tooltip("锁敌状态下的移动速度")]
    public float moveSpeed = 3f;
    

    private bool _isDodging = false;

    public override void Enter(object parameter = null)
    {
        base.Enter(parameter);
        
        // 1. 初始状态重置锁
        _isDodging = false;
        playerModel.isAiming = true;

        // 确保进入状态时清理掉可能残留的事件订阅，防止重复订阅
        if (playerModel.pac != null)
        {
            playerModel.pac.OnSkillEnd -= OnDodgeFinished;
        }

        if (!playerModel.ts.HasTarget)
        {
            playerModel.ChangePlayerState(PlayerState.ground); 
        }
    }

    public override void Update()
    {
        base.Update();
        
        // 2. 如果正在翻滚，直接 return，禁止移动和再次翻滚
        if (_isDodging) return;
        
        if (playerController.aim) 
        {
            playerModel.ts.ToggleLockOn();
            return;
        }
        
        if (!playerModel.ts.HasTarget)
        {
            playerModel.ChangePlayerState(PlayerState.ground);
            return;
        }
        
        if (playerController.jump && playerController.isGround)
        {
            playerModel.gravityVector.y = Mathf.Sqrt(playerModel.gravity * -2.0f * playerModel.jumpHeight);
            playerModel.ChangePlayerState(PlayerState.sky);
            return;
        }

        // 3. 翻滚输入检测
        if (playerController.dodge)
        {
            OnDodgeButtonPressed();
            return; 
        }
        
        if (playerController.lightAttack)
        {
            if(!playerModel.isAttacking)
                playerModel.ChangePlayerState(PlayerState.attack, AttackType.light);
            playerModel.tagComponent.AddTransientTag(playerModel.LightAttackInputTag);
        }
        
        if (playerController.heavyAttack)
        {
            if(!playerModel.isAttacking)
                playerModel.ChangePlayerState(PlayerState.attack, AttackType.heavy);
            playerModel.tagComponent.AddTransientTag(playerModel.HeavyAttackInputTag);
        }

        if (playerController.defend)
        {
            if(!playerModel.isAttacking)
                playerModel.ChangePlayerState(PlayerState.attack, AttackType.defend);
            playerModel.tagComponent.AddTransientTag(playerModel.DefendInputTag);
        }
        
        ForceRotationTowardsTarget();

        Vector2 moveInput = playerController.movement;
        playerController.speed = moveSpeed * moveInput.magnitude;
    }

    public override void Exit()
    {
        base.Exit();
        
        playerModel.isAiming = false;
        playerController.speed = 0;
        
        // 4. 安全退出：如果翻滚还没结束就切换了状态（比如被打断），也要记得取消订阅
        if (playerModel.pac != null)
        {
            playerModel.pac.OnSkillEnd -= OnDodgeFinished;
        }
        _isDodging = false;
    }
    
    private void ForceRotationTowardsTarget()
    {
        if (!playerModel.ts || playerModel.ts.CurrentTarget == null) return;

        Vector3 directionToTarget = playerModel.ts.CurrentTarget.position - playerModel.transform.position;
        directionToTarget.y = 0; 
        if (directionToTarget != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(directionToTarget);
            playerModel.transform.rotation = Quaternion.Slerp(playerModel.transform.rotation, targetRotation, Time.deltaTime * rotationSpeed);
        }
    }
    
    // 【新增方法】翻滚结束的回调
    private void OnDodgeFinished()
    {
        _isDodging = false; // 解锁，允许再次翻滚或移动
        if (playerModel.pac != null)
        {
            playerModel.pac.OnSkillEnd -= OnDodgeFinished; // 任务完成，取消订阅
        }
    }
    
    void OnDodgeButtonPressed()
    {
        if (_isDodging) return; // 如果已经在翻滚，直接忽略
        
        // 5. 上锁
        _isDodging = true;

        Vector2 moveInput = playerController.movement;
        
        // 6. 订阅技能结束事件：当 SkillComponent 说动画播完时，调用 OnDodgeFinished
        playerModel.pac.OnSkillEnd += OnDodgeFinished;

        if (moveInput.magnitude < 0.1f)
        {
            playerModel.PlaySkill(playerModel.dodgeB);
            return;
        }

        Transform cameraTransform = Camera.main.transform;
        Transform playerTransform = playerModel.transform;
        Vector3 camForward = cameraTransform.forward;
        Vector3 camRight = cameraTransform.right;
        camForward.y = 0; camRight.y = 0;
        camForward.Normalize(); camRight.Normalize();

        Vector3 desiredMoveDirection = (camForward * moveInput.y + camRight * moveInput.x).normalized;
        Vector3 playerForward = playerTransform.forward;
        float angle = Vector3.Angle(playerForward, desiredMoveDirection);

        if (angle <= 45.0f)      playerModel.PlaySkill(playerModel.dodgeF);
        else if (angle >= 135.0f) playerModel.PlaySkill(playerModel.dodgeB);
        else
        {
            float crossY = Vector3.Cross(playerForward, desiredMoveDirection).y;
            if (crossY > 0) playerModel.PlaySkill(playerModel.dodgeR);
            else            playerModel.PlaySkill(playerModel.dodgeL);
        }
    }
}