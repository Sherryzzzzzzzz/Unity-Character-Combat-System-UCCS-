using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.PlayerLoop;

public class PlayerGroundAimState : PlayerStateBase 
{
    [Header("锁敌参数")]
    [Tooltip("锁敌状态下的移动速度")]
    public float moveSpeed = 3f;
    [Tooltip("翻滚后恢复朝向的过渡时间（秒）")]
    public float postDodgeRotationTime = 0.35f;

    private bool _isDodging = false;
    private Coroutine _smoothRotateCoroutine;

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
            return;
        }

        // 【FIX】进入 Aim 状态时，检查角色朝向与目标方向的角度。
        // 如果角度很大（翻滚越过敌人后），用协程平滑旋转过渡，
        // 避免在 Update 中硬拉旋转导致摄像机跳变。
        if (playerModel.ts.CurrentTarget != null)
        {
            Vector3 toTarget = playerModel.ts.CurrentTarget.position - playerModel.transform.position;
            toTarget.y = 0;
            float angle = Vector3.Angle(playerModel.transform.forward, toTarget);

            if (angle > 90f)
            {
                // 翻滚后大角度恢复：用协程平滑旋转，给摄像机时间跟随
                if (_smoothRotateCoroutine != null)
                    playerModel.StopCoroutine(_smoothRotateCoroutine);
                _smoothRotateCoroutine = playerModel.StartCoroutine(SmoothRotateToTarget());
            }
        }

        // ★ 动画层切到锁敌态（AimState：移动混合 + 面向目标转向）
        playerModel.ChangeAnimationState(PlayerAnimationState.aim);
    }

    public override void Update()
    {
        base.Update();
        
        // 2. 如果正在翻滚或受击，直接 return
        if (_isDodging) return;
        if (playerModel.isHitting) return;

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
                playerModel.ChangePlayerState(PlayerState.guard);
        }

        // ★ 转向职责已收敛到动画层 AimState（避免逻辑层+动画层双重 Slerp 导致转向过慢）
        // （翻滚后大角度恢复的 SmoothRotateToTarget 协程仍保留）

        Vector2 moveInput = playerController.movement;
        playerController.speed = moveSpeed * moveInput.magnitude;
    }

    public override void Exit()
    {
        base.Exit();

        playerModel.isAiming = false;
        playerController.speed = 0;

        // 停止可能正在运行的平滑旋转协程
        if (_smoothRotateCoroutine != null)
        {
            playerModel.StopCoroutine(_smoothRotateCoroutine);
            _smoothRotateCoroutine = null;
        }

        // 4. 安全退出：如果翻滚还没结束就切换了状态（比如被打断），也要记得取消订阅
        if (playerModel.pac != null)
        {
            playerModel.pac.OnSkillEnd -= OnDodgeFinished;
        }
        _isDodging = false;
    }

    /// <summary>
    /// 翻滚后平滑旋转回敌人方向。在指定时间内用 Lerp 从当前朝向过渡到目标朝向。
    /// 相比 Slerp 在 Update 中硬拉，协程能在过渡完成后精确对齐，避免摄像机跳变。
    /// </summary>
    private System.Collections.IEnumerator SmoothRotateToTarget()
    {
        if (playerModel.ts == null || playerModel.ts.CurrentTarget == null)
        {
            _smoothRotateCoroutine = null;
            yield break;
        }

        Quaternion startRot = playerModel.transform.rotation;
        Vector3 toTarget = playerModel.ts.CurrentTarget.position - playerModel.transform.position;
        toTarget.y = 0;

        if (toTarget.sqrMagnitude < 0.001f)
        {
            _smoothRotateCoroutine = null;
            yield break;
        }

        Quaternion endRot = Quaternion.LookRotation(toTarget);
        float elapsed = 0f;

        while (elapsed < postDodgeRotationTime)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / postDodgeRotationTime;
            // SmoothStep 让旋转有缓入缓出，避免线性过渡的生硬感
            t = t * t * (3f - 2f * t);
            playerModel.transform.rotation = Quaternion.Slerp(startRot, endRot, t);
            yield return null;
        }

        // 确保最终精确朝向目标
        playerModel.transform.rotation = endRot;
        _smoothRotateCoroutine = null;
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

        // ★ 确保体力已消耗（同帧幂等，不会重复扣）
        if (!playerController.TryConsumeDodgeStamina())
        {
            playerController.dodge = false;
            return;
        }

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

        Transform cameraTransform = PlayerController.Instance?.cameraTransform; if (cameraTransform == null) return;
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