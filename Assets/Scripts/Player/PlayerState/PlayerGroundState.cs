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
        
        if (playerController.defend && !playerModel.isAttacking)
        {
            playerModel.ChangePlayerState(PlayerState.parry);
            return;
        }

        if (playerController.dodge)
        {
            OnDodgeButtonPressed();
        }

        if (TargetingSystem.Instance.HasTarget)
        {
            playerModel.ChangePlayerState(PlayerState.aim);
        }

        if (playerController.aim)
        {
            TargetingSystem.Instance.ToggleLockOn(playerModel.transform);
        }
        
        if (playerController.lightAttack)
        {
            if(!playerModel.isAttacking)
                playerModel.ChangePlayerState(PlayerState.groundLightAttack);
            playerModel.tagComponent.AddTransientTag(playerModel.LightAttackInputTag);
        }
    }

    void OnDodgeButtonPressed()
    {
        // --- 1. 获取输入方向 (Vector2) ---
        Vector2 moveInput = playerController.movement; // 获取原始的 Vector2 输入

        // 如果没有移动输入，则默认向后闪避 (这是一个常见的游戏设计)
        if (moveInput.magnitude < 0.1f)
        {
            Debug.Log("Dodge with no input -> Dodging Backward");
            playerModel.PlaySkill(playerModel.dodgeB);
            return;
        }

        // --- 2. 将输入方向转换为世界空间下的3D向量 ---
        // 这个转换取决于你的摄像机角度
        
        // a. 获取摄像机的前方和右方（在XZ平面上）
        Transform cameraTransform = Camera.main.transform;
        Transform playerTransform = playerModel.transform;
        Vector3 camForward = cameraTransform.forward;
        Vector3 camRight = cameraTransform.right;
        camForward.y = 0; // 忽略Y轴，保持在水平面
        camRight.y = 0;
        camForward.Normalize();
        camRight.Normalize();

        // b. 根据摄像机方向和输入，计算出期望的世界移动方向
        Vector3 desiredMoveDirection = (camForward * moveInput.y + camRight * moveInput.x).normalized;

        // --- 3. 获取角色的当前朝向 ---
        Vector3 playerForward = playerTransform.forward;

        // --- 4. 计算夹角 ---
        // Vector3.Angle 返回两个向量之间的无符号夹角 (0 to 180)
        float angle = Vector3.Angle(playerForward, desiredMoveDirection);

        // --- 5. 根据夹角范围判断方向 ---
        // 我们将360度划分为四个象限：前、后、左、右
        // 前方: -45度 到 45度
        // 后方: 135度 到 225度 (即 > 135度)
        // 左右: 45度 到 135度

        if (angle <= 45.0f)
        {
            Debug.Log($"Dodging Forward (Angle: {angle})");
            playerModel.PlaySkill(playerModel.dodgeF);
        }
        else if (angle >= 135.0f)
        {
            Debug.Log($"Dodging Backward (Angle: {angle})");
            playerModel.PlaySkill(playerModel.dodgeB);
        }
        else // 角度在 45 到 135 之间，需要判断是左还是右
        {
            // --- 6. 使用叉乘判断左右 ---
            // 叉乘结果的Y分量可以告诉我们 desiredMoveDirection 在 playerForward 的左侧还是右侧
            // Vector3.Cross(A, B) -> 结果向量C。根据右手定则，如果B在A的“右侧”，C的Y分量为正。
            // 我们要判断输入方向在角色朝向的哪边，所以是 Cross(playerForward, desiredMoveDirection)
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

