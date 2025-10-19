using System.Collections;
using System.Collections.Generic;
using Animancer;
using UnityEngine;

public class IdleState : PlayerStateBase
{
    private AnimancerComponent _Animancer;
    private ClipTransition _IdleAnimation;

    public override void Init(IStateOwner owner)
    {
        base.Init(owner);
        _Animancer = playerModel.animancer;
        _IdleAnimation = playerModel.AnimationSet.idle;
    }

    public override void Enter()
    {
        base.Enter();
        _Animancer.Play(_IdleAnimation,0.25f,FadeMode.FromStart);
    }

    public override void Update()
    {
        base.Update();
        if (playerController.movement.magnitude != 0)
        {
            playerModel.ChangeAnimationState(PlayerAnimationState.move);
        }

        if (playerController.jump)
        {
            playerModel.ChangeAnimationState(PlayerAnimationState.jump);
        }
        
        if (!playerController.isGround)
        {
            playerModel.ChangeAnimationState(PlayerAnimationState.fall);
        }
        
    }
}
