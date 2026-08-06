using UnityEngine;
using Animancer;

/// <summary>
/// 移动动画态：LinearMixer 四段混合（idle/walk/jog/run）+ SmoothDamp 起步停步。
/// 字段 protected 供 AimState（锁敌态）继承复用。
/// </summary>
public class MoveState : PlayerStateBase
{
    protected AnimancerComponent _Animancer;
    protected LinearMixerState _moveMixer;
    private ClipTransition _WalkAnimation;
    private ClipTransition _JogAnimation;
    private ClipTransition _RunAnimation;
    protected ClipTransition _IdleAnimation;
    protected ClipTransition _MToIAnimation;
    protected ClipTransition _RToIAnimation;

    // 动画混合参数
    protected float _animBlend = 0f;
    private float _blendSmoothSpeed = 5f; // 越大越快

    // 起步/停步加速插值
    protected float _currentSpeed = 0f;
    private float _speedSmoothVelocity = 0f;
    private float _smoothTime = 0.15f; // 越小响应越快

    public override void Init(IStateOwner owner)
    {
        base.Init(owner);
        _Animancer = playerModel.animancer;

        _IdleAnimation = playerModel.AnimationSet.idle;
        _WalkAnimation = playerModel.AnimationSet.walk;
        _JogAnimation = playerModel.AnimationSet.jog;
        _RunAnimation = playerModel.AnimationSet.run;
        _MToIAnimation = playerModel.AnimationSet.MtoI;
        _RToIAnimation = playerModel.AnimationSet.RtoI;

        _moveMixer = new LinearMixerState()
        {
            { _IdleAnimation, 0f },
            { _WalkAnimation, 0.7f },
            { _JogAnimation, 1f },
            { _RunAnimation, 2f }
        };
    }

    public override void Enter()
    {
        base.Enter();

        _Animancer.Play(_moveMixer, 0.25f, FadeMode.FixedSpeed);

        _animBlend = 0f;
        _moveMixer.Parameter = 0f;
        _currentSpeed = 0f;
    }

    public override void Update()
    {
        base.Update();

        Vector2 moveInput = playerController.movement;
        float moveMagnitude = moveInput.magnitude;

        // 计算期望速度（跑 or 走）
        float targetSpeed = 0f;
        if (moveMagnitude > 0.1f)
            targetSpeed = playerController.running ? 2f : 1f;

        // SmoothDamp 平滑起步与停步
        _currentSpeed = Mathf.SmoothDamp(_currentSpeed, targetSpeed, ref _speedSmoothVelocity, _smoothTime);

        // 混合参数平滑插值
        _animBlend = Mathf.MoveTowards(_animBlend, _currentSpeed, _blendSmoothSpeed * Time.deltaTime);
        _moveMixer.Parameter = _animBlend;

        // === 跳跃 / 下落切换 ===
        if (playerController.jump)
        {
            playerModel.ChangeAnimationState(PlayerAnimationState.jump);
            return;
        }
        if (!playerController.isGround)
        {
            playerModel.ChangeAnimationState(PlayerAnimationState.fall);
            return;
        }

        // === 停止：速度真正归零后播停步过渡动画再进 idle（单一逻辑） ===
        if (moveMagnitude < 0.05f && _currentSpeed < 0.05f)
        {
            if (playerController.running)
                _Animancer.Play(_RToIAnimation, 0.1f, FadeMode.FixedSpeed);
            else
                _Animancer.Play(_MToIAnimation, 0.1f, FadeMode.FixedSpeed);
            playerModel.ChangeAnimationState(PlayerAnimationState.idle);
        }
    }

    public override void Exit()
    {
        base.Exit();
        if (_Animancer != null && _IdleAnimation != null)
        {
            _Animancer.Play(_IdleAnimation, 0.2f);
        }
    }
}
