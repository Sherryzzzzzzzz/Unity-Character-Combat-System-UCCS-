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

        // === 跳跃 / 攻击切换 ===
        if (playerController.jump && !playerModel.isAttacking)
        {
            playerModel.gravityVector.y = Mathf.Sqrt(playerModel.gravity * -2.0f * playerModel.jumpHeight);
            playerModel.ChangePlayerState(PlayerState.sky);
            return;
        }

        if (playerController.lightAttack)
        {
            playerModel.ChangePlayerState(PlayerState.groundLightAttack);
            return;
        }
    }

}

