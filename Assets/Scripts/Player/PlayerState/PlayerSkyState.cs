using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerSkyState : PlayerStateBase
{
    public override void Enter(object parameter = null)
    {
        base.Enter(parameter);
        // Maintain aiming flag if we have a lock-on target
        if (playerModel.ts != null && playerModel.ts.HasTarget)
        {
            playerModel.isAiming = true;
        }
    }

    public override void Update()
    {
        float verticalVelocity = playerModel.gravityVector.y;
        if (verticalVelocity < 0 && playerController.isGround&&!playerModel.ts.HasTarget)
        {
            playerModel.ChangePlayerState(PlayerState.ground);
            return;
        }

        if (verticalVelocity < 0 && playerController.isGround&&playerModel.ts.HasTarget)
        {
            playerModel.ChangePlayerState(PlayerState.aim);
            return;
        }

        // Face lock-on target while in air
        if (playerModel.ts != null && playerModel.ts.HasTarget && playerModel.ts.CurrentTarget != null)
        {
            Vector3 dir = playerModel.ts.CurrentTarget.position - playerModel.transform.position;
            dir.y = 0;
            if (dir.sqrMagnitude > 0.001f)
            {
                Quaternion targetRot = Quaternion.LookRotation(dir);
                playerModel.transform.rotation = Quaternion.Slerp(
                    playerModel.transform.rotation, targetRot, Time.deltaTime * 20f);
            }

            // 【FIX】Apply horizontal movement based on input while locked on in air.
            // PlayerController.Update() handles camera-relative direction, but speed was 0
            // because PlayerGroundAimState.Exit() resets it. Set it here so the player
            // can move horizontally during a lock-on jump.
            playerController.speed = playerModel.walkSpeed * playerController.movement.magnitude;
        }

        if (playerController.lightAttack)
        {
            Debug.Log("In Sky State: Light Attack Triggered");
            playerModel.ChangePlayerState(PlayerState.attack,AttackType.skyLight);
            return;
        }

        // 【BUG 6 FIX】空中也允许进入格挡/弹反（空中防御）
        if (playerController.defend)
        {
            playerModel.ChangePlayerState(PlayerState.attack, AttackType.defend);
            return;
        }
    }

    public override void Exit()
    {
        base.Exit();
        // Clear aiming flag — the next state (aim/ground) will set it appropriately
        playerModel.isAiming = false;
    }
}
