using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.PlayerLoop;

public class PlayerGroundState : PlayerStateBase
{
    private float aimSpeed;
    float speed = 0;

    [Header("倾斜参数")]
    public float maxTiltAngle = 15f; // 最大左右倾斜角度
    public float tiltSmooth = 5f;    // 倾斜平滑度

    private float currentTilt = 0f;

    public override void Update()
    {
        base.Update();

        // ✅ 如果正在攻击，就不进入移动控制
        if (playerModel.isAttacking)
            return;

        // ✅ 如果正在受击，只允许移动，不响应动作输入
        if (playerModel.isHitting)
            return;

        // === 正常移动逻辑 ===
        if (playerController.running)
        {
            aimSpeed = playerModel.runSpeed * playerController.movement.magnitude;
        }
        else
        {
            aimSpeed = playerModel.walkSpeed * playerController.movement.magnitude;
        }

        float accel = (playerController.movement.magnitude > 0.1f) ? 8f : 4f;
        speed = Mathf.Lerp(speed, aimSpeed, Time.deltaTime * accel);
        playerController.speed = speed;

        // === 倾斜控制 ===
        if (playerController.movement.magnitude > 0.1f && playerController.speed > 5f)
        {
            Vector3 forward = playerController.transform.forward;
            Vector3 moveDir = playerController.movement.normalized;
            float angle = Vector3.SignedAngle(forward, moveDir, Vector3.up);

            float targetTilt = Mathf.Clamp(angle / 90f, -1f, 1f) * maxTiltAngle;
            currentTilt = Mathf.Lerp(currentTilt, targetTilt, Time.deltaTime * tiltSmooth);
        }
        else
        {
            currentTilt = Mathf.Lerp(currentTilt, 0, Time.deltaTime * (tiltSmooth * 1.5f));
        }

        Quaternion targetRot = Quaternion.Euler(0, playerController.transform.eulerAngles.y, currentTilt);
        playerController.transform.rotation = targetRot;
        
        if (playerController.jump && playerController.isGround)
        {
            if (playerModel.pac.CanBeCanceledBy(CancelActionType.Jump))
            {
                playerModel.gravityVector.y = Mathf.Sqrt(playerModel.gravity * -2.0f * playerModel.jumpHeight);
                playerModel.ChangePlayerState(PlayerState.sky);
                return;
            }
        }

        if (playerController.dodge)
        {
            OnDodgeButtonPressed();
            return;
        }

        if (playerController.aim)
        {
            playerModel.ts.ToggleLockOn();
    
            if (playerModel.ts.HasTarget)
            {
                playerModel.ChangePlayerState(PlayerState.aim);
                return;
            }
        }
        
        if (playerModel.ts.HasTarget)
        {
            playerModel.ChangePlayerState(PlayerState.aim);
            return;
        }
        
        if (playerController.lightAttack)
        {
            playerModel.ChangePlayerState(PlayerState.attack, AttackType.light);
            return;
        }
        
        if (playerController.heavyAttack)
        {
            playerModel.ChangePlayerState(PlayerState.attack,AttackType.heavy);
            return;
        }

        if (playerController.combatArt)
        {
            playerModel.ChangePlayerState(PlayerState.attack, AttackType.skill);
            return;
        }

        if (playerController.defend)
        {
            playerModel.ChangePlayerState(PlayerState.guard);
            return;
        }
    }

    void OnDodgeButtonPressed()
    {
        // ★ 确保体力已消耗（同帧幂等，不会重复扣）
        if (!playerController.TryConsumeDodgeStamina())
        {
            playerController.dodge = false;
            return;
        }

        // ★ 尝试完美闪避检测
        var dodgeAbility = playerModel.GetComponent<DodgeAbility>();
        if (dodgeAbility != null)
            dodgeAbility.AttemptDodge();

        Vector2 moveInput = playerController.movement; // 获取原始的 Vector2 输入
        
        if (moveInput.magnitude < 0.1f)
        {
            Debug.Log("Dodge with no input -> Dodging Backward");
            playerModel.PlaySkill(playerModel.dodgeB);
            return;
        }
        
        Transform cameraTransform = PlayerController.Instance?.cameraTransform; if (cameraTransform == null) return;
        Transform playerTransform = playerModel.transform;
        Vector3 camForward = cameraTransform.forward;
        Vector3 camRight = cameraTransform.right;
        camForward.y = 0; // 忽略Y轴，保持在水平面
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
            Debug.Log($"Dodging Backward (Angle: {angle})");
            playerModel.PlaySkill(playerModel.dodgeB);
        }
        else 
        {
            float crossY = Vector3.Cross(playerForward, desiredMoveDirection).y;

            if (crossY > 0)
            {
                Debug.Log($"Dodging Right (Angle: {angle}, CrossY: {crossY})");
                playerModel.PlaySkill(playerModel.dodgeR);
            }
            else
            {
                Debug.Log($"Dodging Left (Angle: {angle}, CrossY: {crossY})");
                playerModel.PlaySkill(playerModel.dodgeL);
            }
        }
    }
    
}

