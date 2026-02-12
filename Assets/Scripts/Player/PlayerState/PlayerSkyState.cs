using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerSkyState : PlayerStateBase
{
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
        
        if (playerController.lightAttack)
        {
            Debug.Log("In Sky State: Light Attack Triggered");
            playerModel.ChangePlayerState(PlayerState.attack,AttackType.skyLight);
            return;
        }
    }
}
