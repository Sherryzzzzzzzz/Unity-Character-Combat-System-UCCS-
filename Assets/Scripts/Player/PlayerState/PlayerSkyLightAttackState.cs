using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerSkyLightAttackState : PlayerStateBase
{
    private float moveScale = 0.1f; // 攻击期间移动输入缩放

    public override void Enter()
    {
        base.Enter();

        playerModel.isAttacking = true;

        if (!playerModel.isComboChain)
        {
            playerModel.PlaySkill(playerModel.lightSkyStart);
        }
    }

    public override void Update()
    {
        base.Update();
        if(playerController.isGround)
            playerModel.ChangePlayerState(PlayerState.ground);
    }

    public override void Exit()
    {
        base.Exit();
        playerModel.pac.StopAndCleanup();
    }
}