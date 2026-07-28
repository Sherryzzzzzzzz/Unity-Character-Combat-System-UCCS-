using UnityEngine;

/// <summary>设置动画状态（Idle / Move）</summary>
[System.Serializable]
public class BTA_SetAnimationState : BTAction
{
    public EnemyAnimationState targetState = EnemyAnimationState.Idle;

    public override BTNodeState OnTick()
    {
        var animData = _runner?.GetComponent<EnemyAnimationData>();
        if (animData != null)
            animData.CurrentState = targetState;

        return _state = BTNodeState.Success;
    }
}
