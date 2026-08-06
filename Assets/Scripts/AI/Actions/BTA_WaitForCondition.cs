using UnityEngine;

/// <summary>等待条件满足 — 检查黑板 bool、GameplayTag 或超时</summary>
[System.Serializable]
public class BTA_WaitForCondition : BTAction
{
    public enum WaitCondition
    {
        BlackboardBoolTrue,
        HasGameplayTag,
        TimeoutOnly
    }

    [Header("等待模式")]
    public WaitCondition condition = WaitCondition.TimeoutOnly;

    [Header("黑板条件")]
    public string blackboardKey;

    [Header("GameplayTag 条件")]
    public GameplayTagSO requiredTag;

    [Header("超时")]
    public float timeout = 10f;

    private float _timer;

    public override void OnEnter(IBTRunner runner)
    {
        base.OnEnter(runner);
        _timer = 0f;
    }

    public override BTNodeState OnTick()
    {
        _timer += Time.deltaTime;

        bool met = condition switch
        {
            WaitCondition.BlackboardBoolTrue => _runner?.Blackboard?.GetBool(blackboardKey) ?? false,
            WaitCondition.HasGameplayTag =>
                _runner?.GetComponent<TagComponent>()?.HasTag(requiredTag) ?? false,
            _ => false
        };

        if (met)
            return _state = BTNodeState.Success;

        if (_timer >= timeout)
            return _state = BTNodeState.Failure;

        return _state = BTNodeState.Running;
    }
}
