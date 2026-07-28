using UnityEngine;

/// <summary>Condition — 条件装饰器，满足条件才执行子节点，否则直接失败</summary>
[System.Serializable]
public class BTCondition : BTDecorator
{
    public enum ConditionType
    {
        Distance,        // 与目标的距离
        HPPercentage,    // 自身 HP 百分比
        HasGameplayTag,  // 是否拥有指定 GameplayTag
        BlackboardBool,  // 黑板 bool 值
        AlwaysTrue,      // 永远为真（占位/调试用）
    }

    [Header("条件类型")]
    public ConditionType type = ConditionType.AlwaysTrue;

    [Header("距离条件")]
    [Tooltip("黑板中目标 Transform 的键名")]
    public string targetKey = "player";
    [Tooltip("距离比较模式")]
    public CompareMode distanceCompare = CompareMode.LessThan;
    public float distanceValue = 2f;

    [Header("HP 条件")]
    public CompareMode hpCompare = CompareMode.LessThan;
    [Range(0, 1)] public float hpThreshold = 0.5f;

    [Header("GameplayTag 条件")]
    public GameplayTagSO requiredTag;

    [Header("黑板 Bool 条件")]
    public string blackboardKey = "";
    public bool expectedBool = true;

    public enum CompareMode { LessThan, GreaterThan, LessOrEqual, GreaterOrEqual }

    public override BTNodeState OnTick()
    {
        bool met = Evaluate();
        if (!met) return _state = BTNodeState.Failure;

        if (child == null) return _state = BTNodeState.Success;

        if (child.State == BTNodeState.Inactive)
            child.OnEnter(_runner);

        var result = child.OnTick();
        if (result != BTNodeState.Running)
            child.OnExit();

        return _state = result;
    }

    private bool Evaluate()
    {
        switch (type)
        {
            case ConditionType.AlwaysTrue:
                return true;

            case ConditionType.Distance:
            {
                var target = _runner?.Blackboard?.Get<Transform>(targetKey);
                if (target == null) return false;
                // 用 sqrMagnitude 避免 sqrt
                var d = _runner.transform.position - target.position;
                d.y = 0;
                return Compare(d.sqrMagnitude, distanceValue * distanceValue, distanceCompare);
            }

            case ConditionType.HPPercentage:
            {
                var attr = _runner?.GetComponent<UCCS.IAttributeProvider>();
                if (attr == null) return false;
                float pct = attr.Health / Mathf.Max(attr.HealthMax, 1f);
                return Compare(pct, hpThreshold, hpCompare);
            }

            case ConditionType.HasGameplayTag:
            {
                var tc = _runner?.Tags;
                return tc != null && requiredTag != null && tc.HasTag(requiredTag);
            }

            case ConditionType.BlackboardBool:
            {
                return _runner?.Blackboard?.GetBool(blackboardKey) == expectedBool;
            }

            default:
                return false;
        }
    }

    private static bool Compare(float a, float b, CompareMode mode)
    {
        return mode switch
        {
            CompareMode.LessThan => a < b,
            CompareMode.GreaterThan => a > b,
            CompareMode.LessOrEqual => a <= b,
            CompareMode.GreaterOrEqual => a >= b,
            _ => false
        };
    }

    public string Describe() => type switch
    {
        ConditionType.AlwaysTrue => "总是为真",
        ConditionType.Distance => $"距离{distanceCompare}{distanceValue}m",
        ConditionType.HPPercentage => $"血量{hpCompare}{hpThreshold*100:F0}%",
        ConditionType.HasGameplayTag => $"有标签:{requiredTag?.name}",
        ConditionType.BlackboardBool => $"黑板:{blackboardKey}={expectedBool}",
        _ => "未知"
    };
}
