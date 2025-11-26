using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Animancer;

public class ParryState : PlayerStateBase
{
    private enum SubState { None, Start, Loop, End }
    private SubState _subState;

    private ClipTransition _startAnim, _loopAnim, _endAnim;
    private AnimancerState _currentAnimState;

    public override void Init(IStateOwner owner)
    {
        base.Init(owner);
        _startAnim = playerModel.Parry_Start;
        _loopAnim = playerModel.Guard_Loop;
        _endAnim = playerModel.Parry_End;
    }

    public override void Enter()
    {

        _subState = SubState.Start;
        _currentAnimState = playerModel.animancer.Play(_startAnim);
        
        // 注册回调：当 Start 动画播放完毕时，调用 GoToLoopState
        _currentAnimState.Events(this).OnEnd = GoToLoopState;
    }

    public override void Update()
    {
        if (!playerController.defend && _subState != SubState.End)
        {
            GoToEndState();
        }
    }

    private void GoToLoopState()
    {
        if (!playerController.defend)
        {
            GoToEndState();
        }
    }

    private void GoToEndState()
    {
        if (_subState == SubState.End) return; 

        _subState = SubState.End;
        _currentAnimState = playerModel.animancer.Play(_endAnim);
        
        _currentAnimState.Events(this).OnEnd =()=> playerModel.ChangeAnimationState(PlayerAnimationState.idle);
    }
    
    public override void Exit()
    {
        if (_currentAnimState != null)
        {
            _currentAnimState.Events(this).OnEnd = null;
        }
        _subState = SubState.None;
    }
}
