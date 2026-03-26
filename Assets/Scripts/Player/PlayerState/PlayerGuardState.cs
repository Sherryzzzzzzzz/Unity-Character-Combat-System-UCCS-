using UnityEngine;
using Animancer;

public class PlayerGuardState : PlayerStateBase
{
    private AnimancerLayer _attackLayer;
    private int _guardEffectHandle = -1;
    private bool _isExiting = false;

    public override void Enter(object parameter = null)
    {
        base.Enter();
        _isExiting = false;

        // 标记为 attacking 状态，防止 StopAndCleanup 的 triggerDefaultStateChange 覆盖状态
        playerModel.isAttacking = true;

        var animancer = playerModel.animancer;
        _attackLayer = animancer.Layers[1];

        // Play guard loop animation on the attack layer
        if (playerModel.guardAnimation != null && playerModel.guardAnimation.Clip != null)
        {
            var state = _attackLayer.Play(playerModel.guardAnimation);
            // 强制攻击层权重为1（对抗 StopAndCleanup 的淡出）
            _attackLayer.SetWeight(1f);
        }

        // Apply guard GameplayEffect via ASC
        var asc = playerModel.GetComponent<AbilitySystemComponent>();
        if (asc != null && playerModel.guardEffect != null)
        {
            _guardEffectHandle = asc.ApplyGameplayEffect(playerModel.guardEffect, asc);
        }

        playerModel.isDefending = true;
    }

    public override void Update()
    {
        base.Update();

        if (_isExiting) return;

        // Face lock-on target
        if (playerModel.ts != null && playerModel.ts.HasTarget)
        {
            Vector3 dir = playerModel.ts.CurrentTarget.position - playerModel.transform.position;
            dir.y = 0;
            if (dir.sqrMagnitude > 0.001f)
            {
                Quaternion targetRot = Quaternion.LookRotation(dir);
                playerModel.transform.rotation = Quaternion.Slerp(
                    playerModel.transform.rotation, targetRot, Time.deltaTime * 30f);
            }
        }

        // Dodge cancel
        if (playerController.dodge)
        {
            ExitToGround();
            return;
        }

        // Release defend → play end animation → exit
        if (!playerController.defendHeld)
        {
            if (playerModel.guardEndAnimation != null && playerModel.guardEndAnimation.Clip != null)
            {
                _isExiting = true;
                var state = _attackLayer.Play(playerModel.guardEndAnimation);
                state.Events(playerModel).OnEnd = () => ExitToGround();
            }
            else
            {
                ExitToGround();
            }
        }
    }

    public override void Exit()
    {
        base.Exit();

        // Remove guard GE
        if (_guardEffectHandle > 0)
        {
            var asc = playerModel.GetComponent<AbilitySystemComponent>();
            if (asc != null)
            {
                asc.RemoveActiveEffectByHandle(_guardEffectHandle);
            }
            _guardEffectHandle = -1;
        }

        playerModel.isAttacking = false;
        playerModel.isDefending = false;

        // Fade out attack layer
        if (_attackLayer != null)
        {
            _attackLayer.StartFade(0f, 0.25f);
        }
    }

    private void ExitToGround()
    {
        if (playerController.isGround)
            playerModel.ChangePlayerState(PlayerState.ground);
        else
            playerModel.ChangePlayerState(PlayerState.sky);
    }
}
