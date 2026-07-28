using UnityEngine;

/// <summary>装饰节点 — 修饰单个子节点</summary>
public abstract class BTDecorator : BTNode
{
    [SerializeReference]
    public BTNode child;

    public override void OnEnter(BTreeRunner runner)
    {
        base.OnEnter(runner);
        child?.Reset();
    }

    public override void Reset()
    {
        base.Reset();
        child?.Reset();
    }
}

// ================================================================

/// <summary>Inverter — 翻转结果</summary>
[System.Serializable]
public class BTInverter : BTDecorator
{
    public override BTNodeState OnTick()
    {
        if (child == null) return _state = BTNodeState.Failure;

        if (child.State == BTNodeState.Inactive)
            child.OnEnter(_runner);

        var result = child.OnTick();

        switch (result)
        {
            case BTNodeState.Success:
                child.OnExit();
                return _state = BTNodeState.Failure;
            case BTNodeState.Failure:
                child.OnExit();
                return _state = BTNodeState.Success;
            default:
                return _state = BTNodeState.Running;
        }
    }
}

// ================================================================

/// <summary>Repeater — 重复执行 N 次（-1 = 无限）</summary>
[System.Serializable]
public class BTRepeater : BTDecorator
{
    [Tooltip("重复次数，-1 = 无限循环")]
    public int repeatCount = -1;

    private int _executedCount;

    public override void OnEnter(BTreeRunner runner)
    {
        base.OnEnter(runner);
        _executedCount = 0;
    }

    public override BTNodeState OnTick()
    {
        if (child == null) return _state = BTNodeState.Failure;
        if (repeatCount >= 0 && _executedCount >= repeatCount)
            return _state = BTNodeState.Success;

        if (child.State == BTNodeState.Inactive)
            child.OnEnter(_runner);

        var result = child.OnTick();

        if (result != BTNodeState.Running)
        {
            child.OnExit();
            child.Reset();
            _executedCount++;

            if (repeatCount >= 0 && _executedCount >= repeatCount)
            {
                OnExit();
                return _state = BTNodeState.Success;
            }
        }

        return _state = BTNodeState.Running;
    }

    public override void Reset()
    {
        base.Reset();
        _executedCount = 0;
    }
}

// ================================================================

/// <summary>Wait — 等待 N 秒后执行子节点</summary>
[System.Serializable]
public class BTWait : BTDecorator
{
    [Tooltip("等待秒数")]
    public float duration = 1f;

    private float _timer;

    public override void OnEnter(BTreeRunner runner)
    {
        base.OnEnter(runner);
        _timer = 0f;
    }

    public override BTNodeState OnTick()
    {
        _timer += Time.deltaTime;
        if (_timer < duration)
            return _state = BTNodeState.Running;

        // 等待结束，执行子节点
        if (child == null) return _state = BTNodeState.Success;

        if (child.State == BTNodeState.Inactive)
            child.OnEnter(_runner);

        var result = child.OnTick();
        if (result != BTNodeState.Running)
            child.OnExit();

        return _state = result;
    }

    public override void Reset()
    {
        base.Reset();
        _timer = 0f;
    }
}

// ================================================================

/// <summary>Succeeder — 永远返回成功</summary>
[System.Serializable]
public class BTSucceeder : BTDecorator
{
    public override BTNodeState OnTick()
    {
        if (child == null) return _state = BTNodeState.Success;

        if (child.State == BTNodeState.Inactive)
            child.OnEnter(_runner);

        var result = child.OnTick();

        if (result != BTNodeState.Running)
        {
            child.OnExit();
            OnExit();
            return _state = BTNodeState.Success;
        }

        return _state = BTNodeState.Running;
    }
}

// ================================================================

/// <summary>Cooldown — 执行子节点后进入冷却，冷却期间直接返回失败</summary>
[System.Serializable]
public class BTCooldown : BTDecorator
{
    [Tooltip("冷却秒数")]
    public float cooldownTime = 5f;

    private float _lastExecuteTime = -999f;

    public override BTNodeState OnTick()
    {
        if (child == null) return _state = BTNodeState.Failure;

        // 冷却中
        if (Time.time - _lastExecuteTime < cooldownTime)
            return _state = BTNodeState.Failure;

        if (child.State == BTNodeState.Inactive)
            child.OnEnter(_runner);

        var result = child.OnTick();

        if (result != BTNodeState.Running)
        {
            child.OnExit();
            _lastExecuteTime = Time.time; // 记录执行时间
            OnExit();
        }

        return _state = result;
    }

    public override void Reset()
    {
        base.Reset();
        _lastExecuteTime = -999f;
    }
}
