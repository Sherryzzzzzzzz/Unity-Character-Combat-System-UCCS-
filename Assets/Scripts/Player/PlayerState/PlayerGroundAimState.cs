using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.PlayerLoop;

// 为了清晰，可以考虑重命名为 PlayerLockedOnState
public class PlayerGroundAimState : PlayerStateBase 
{
    [Header("锁敌参数")]
    [Tooltip("角色朝向锁敌目标的旋转速度")]
    public float rotationSpeed = 20f;
    [Tooltip("锁敌状态下的移动速度")]
    public float moveSpeed = 3f; // 可以是一个固定速度，或者从 PlayerModel 读取

    // Animator 参数哈希值，用于优化性能
    private static readonly int LockOnXHash = Animator.StringToHash("LockOn_X");
    private static readonly int LockOnYHash = Animator.StringToHash("LockOn_Y");
    private static readonly int IsLockedOnHash = Animator.StringToHash("IsLockedOn");

    public override void Enter()
    {
        base.Enter();
        
        playerModel.isAiming = true;
        
        // 进入此状态时，立即通知 Animator
        playerModel.animator.SetBool(IsLockedOnHash, true);

        // 确保 TargetingSystem 中有目标，如果没有，则立即退出状态
        // 这是一个安全检查，防止意外进入此状态
        if (!TargetingSystem.Instance.HasTarget)
        {
            Debug.LogWarning("Entered PlayerGroundAimState without a valid target. Reverting to default state.");
            // 假设你有一个默认的地面状态叫 PlayerState.ground
            playerModel.ChangePlayerState(PlayerState.ground); 
        }
    }

    public override void Update()
    {
        base.Update();
        
        // --- 核心安全检查 ---
        // 如果在运行中丢失了目标（例如敌人死亡），则退出锁敌状态
        if (!TargetingSystem.Instance.HasTarget)
        {
            playerModel.ChangePlayerState(PlayerState.ground);
            return;
        }

        // --- 检查是否可以被其他动作打断 ---
        // ✅ 如果正在攻击，则不执行移动和旋转，但仍然保持朝向
        if (playerModel.isAttacking)
        {
            ForceRotationTowardsTarget(); // 即使在攻击中，也要保持面向敌人
            // 清空移动动画参数，防止在攻击时还播放移动动画
            playerModel.animator.SetFloat(LockOnXHash, 0, 0.1f, Time.deltaTime);
            playerModel.animator.SetFloat(LockOnYHash, 0, 0.1f, Time.deltaTime);
            return;
        }
        
        // --- 处理状态切换输入 ---
        if (playerController.jump)
        {
            // (跳跃逻辑保持不变)
            playerModel.gravityVector.y = Mathf.Sqrt(playerModel.gravity * -2.0f * playerModel.jumpHeight);
            playerModel.ChangePlayerState(PlayerState.sky);
            return;
        }
        if (playerController.defend)
        {
            playerModel.ChangePlayerState(PlayerState.parry);
            return;
        }
        if (playerController.dodge)
        {
            OnDodgeButtonPressed();
            // OnDodgeButtonPressed 内部会播放技能，isAttacking 会变为 true
            // 所以下一帧会自动进入上面的 isAttacking 判断
            return;
        }
        
        if (playerController.lightAttack)
        {
            if(!playerModel.isAttacking)
                playerModel.ChangePlayerState(PlayerState.groundLightAttack);
            playerModel.tagComponent.AddTransientTag(playerModel.LightAttackInputTag);
        }

        // --- 锁敌状态下的核心逻辑 ---
        
        // 1. 强制角色朝向目标
        ForceRotationTowardsTarget();

        // 2. 将输入转换为相对于角色的局部移动
        Vector2 moveInput = playerController.movement;
        Vector3 moveDirection = (playerModel.transform.forward * moveInput.y + playerModel.transform.right * moveInput.x).normalized;
        
        // 3. 应用移动
        // playerController.speed 可能是你用来驱动 CharacterController.Move 的变量
        playerController.speed = moveSpeed * moveInput.magnitude;
        // 你可能需要一个方法来告诉 PlayerController 使用我们计算出的 moveDirection
        // playerController.SetOverrideMoveDirection(moveDirection);

        // 4. 更新动画参数，驱动锁敌混合树
        playerModel.animator.SetFloat(LockOnXHash, moveInput.x, 0.1f, Time.deltaTime);
        playerModel.animator.SetFloat(LockOnYHash, moveInput.y, 0.1f, Time.deltaTime);

        if (!TargetingSystem.Instance.HasTarget)
        {
            playerModel.ChangePlayerState(PlayerState.ground);
        }
        
        if (playerController.aim)
        {
            TargetingSystem.Instance.ToggleLockOn(playerModel.transform);
        }
    }

    public override void Exit()
    {
        base.Exit();
        
        playerModel.isAiming = false;
        
        // 退出此状态时，通知 Animator
        playerModel.animator.SetBool(IsLockedOnHash, false);

        // (可选) 清空动画参数，确保平滑过渡回自由移动
        playerModel.animator.SetFloat(LockOnXHash, 0);
        playerModel.animator.SetFloat(LockOnYHash, 0);

        // 重置 PlayerController 的速度
        playerController.speed = 0;
    }

    /// <summary>
    /// 辅助方法：强制角色平滑地转向当前锁定的目标。
    /// </summary>
    private void ForceRotationTowardsTarget()
    {
        if (!TargetingSystem.Instance.HasTarget) return;

        Vector3 directionToTarget = TargetingSystem.Instance.CurrentTarget.position - playerModel.transform.position;
        directionToTarget.y = 0; // 保持水平旋转
        Quaternion targetRotation = Quaternion.LookRotation(directionToTarget);
        playerModel.transform.rotation = Quaternion.Slerp(playerModel.transform.rotation, targetRotation, Time.deltaTime * rotationSpeed);
    }
    
    // OnDodgeButtonPressed 方法保持不变，它已经是正确的逻辑
    void OnDodgeButtonPressed()
    {
        Vector2 moveInput = playerController.movement;
        if (moveInput.magnitude < 0.1f)
        {
            playerModel.PlaySkill(playerModel.dodgeB);
            return;
        }

        Transform cameraTransform = Camera.main.transform;
        Transform playerTransform = playerModel.transform;
        Vector3 camForward = cameraTransform.forward;
        Vector3 camRight = cameraTransform.right;
        camForward.y = 0;
        camRight.y = 0;
        camForward.Normalize();
        camRight.Normalize();

        Vector3 desiredMoveDirection = (camForward * moveInput.y + camRight * moveInput.x).normalized;
        Vector3 playerForward = playerTransform.forward;
        float angle = Vector3.Angle(playerForward, desiredMoveDirection);

        if (angle <= 45.0f)
        {
            playerModel.PlaySkill(playerModel.dodgeF);
        }
        else if (angle >= 135.0f)
        {
            playerModel.PlaySkill(playerModel.dodgeB);
        }
        else
        {
            float crossY = Vector3.Cross(playerForward, desiredMoveDirection).y;
            if (crossY > 0)
            {
                playerModel.PlaySkill(playerModel.dodgeR);
            }
            else
            {
                playerModel.PlaySkill(playerModel.dodgeL);
            }
        }
    }
}