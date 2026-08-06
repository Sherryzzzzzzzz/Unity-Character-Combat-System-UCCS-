using UnityEngine;
using Animancer;

/// <summary>
/// 下落动画态：播放下落动画，落地回待机（带淡入过渡）。
/// </summary>
public class FallState : PlayerStateBase
{
    private AnimancerComponent _Animancer;
    private ClipTransition _FallAnimation;

    public override void Init(IStateOwner owner)
    {
        base.Init(owner);
        _Animancer = playerModel.animancer;
        _FallAnimation = playerModel.AnimationSet.sky;
    }

    public override void Enter()
    {
        base.Enter();
        _Animancer.Play(_FallAnimation, 0.25f, FadeMode.FixedSpeed);
    }

    public override void Update()
    {
        base.Update();

        // 落地 → 回待机
        if (playerController.isGround)
        {
            playerModel.ChangeAnimationState(PlayerAnimationState.idle);
        }
    }
}
