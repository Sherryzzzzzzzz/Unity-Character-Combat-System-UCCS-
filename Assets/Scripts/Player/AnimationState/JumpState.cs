using UnityEngine;
using Animancer;

/// <summary>
/// 跳跃动画态：起跳播放跳跃动画，滞空计时保证上升段播满再切下落；
/// 落地直接回 idle（下落/落地缓冲见 FallState）。
/// </summary>
public class JumpState : PlayerStateBase
{
    private AnimancerComponent _Animancer;
    private ClipTransition _JumpAnimation;

    // 滞空计时：防止"起跳瞬间被物理速度截断动画"
    private float _jumpTimer = 0f;
    private const float MinRiseDuration = 0.22f; // 上升段最短播满时长（秒）

    public override void Init(IStateOwner owner)
    {
        base.Init(owner);
        _Animancer = playerModel.animancer;
        _JumpAnimation = playerModel.AnimationSet.jump;
    }

    public override void Enter()
    {
        base.Enter();
        _jumpTimer = 0f;
        _Animancer.Play(_JumpAnimation);
    }

    public override void Update()
    {
        base.Update();
        _jumpTimer += Time.deltaTime;

        // 落地 → 回待机
        if (playerController.isGround)
        {
            playerModel.ChangeAnimationState(PlayerAnimationState.idle);
            return;
        }

        // 上升段保证播满，之后若开始下落才切 fall（避免跳跃动画被瞬间截断）
        if (_jumpTimer >= MinRiseDuration && playerModel.gravityVector.y <= 0.1f)
        {
            playerModel.ChangeAnimationState(PlayerAnimationState.fall);
        }
    }
}
