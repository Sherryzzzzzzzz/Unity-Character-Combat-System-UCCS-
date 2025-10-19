using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerSkyLightAttackState : PlayerStateBase
{
    private float moveScale = 0.1f; // 攻击期间移动输入缩放

    public override void Enter()
    {
        base.Enter();
        
        playerModel.stopGravity = true;

        playerModel.isAttacking = true;

        if (!playerModel.isComboChain)
        {
            playerModel.PlaySkill(playerModel.lightSkyStart);
        }

        playerModel.pac.OnSkillEnd += OnSkillEnd;
    }

    public override void Update()
    {
        base.Update();
    }

    private void OnSkillEnd()
    {
        if (!playerModel.isComboChain)
        {
            Debug.Log("[LightAttackState] 攻击结束，准备返回地面");

            playerModel.isAttacking = false;
            playerModel.pac.OnSkillEnd -= OnSkillEnd;

            playerModel.StartCoroutine(WaitAndReturnToGround());
        }
        else
        {
            Debug.Log("[LightAttackState] 连段过渡中，保持攻击状态");
        }
    }

    private IEnumerator WaitAndReturnToGround()
    {
        yield return new WaitForSeconds(0.1f);
        playerModel.ChangePlayerState(PlayerState.sky);
    }

    public override void Exit()
    {
        base.Exit();
        playerModel.stopGravity = false;
        playerModel.pac.OnSkillEnd -= OnSkillEnd;
        playerModel.pac.StopAndCleanup();
    }
}