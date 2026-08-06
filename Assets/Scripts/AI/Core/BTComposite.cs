using System.Collections.Generic;
using UnityEngine;

/// <summary>组合节点 — 持有多个子节点</summary>
public abstract class BTComposite : BTNode
{
    [SerializeReference]
    public List<BTNode> children = new();

    /// <summary>当前执行到第几个子节点</summary>
    protected int _currentIndex;

    public override void OnEnter(IBTRunner runner)
    {
        base.OnEnter(runner);
        _currentIndex = 0;
    }

    public override void Reset()
    {
        base.Reset();
        _currentIndex = 0;
        foreach (var c in children)
            c.Reset();
    }
}

// ================================================================

/// <summary>Sequence — 顺序执行，任一失败则失败，全部成功则成功</summary>
[System.Serializable]
public class BTSequence : BTComposite
{
    public override BTNodeState OnTick()
    {
        while (_currentIndex < children.Count)
        {
            var child = children[_currentIndex];

            if (child.State == BTNodeState.Inactive)
                child.OnEnter(_runner);

            var result = child.OnTick();

            if (result == BTNodeState.Failure)
            {
                child.OnExit();
                OnExit();
                return _state = BTNodeState.Failure;
            }

            if (result == BTNodeState.Running)
                return _state = BTNodeState.Running;

            // Success → 下一个
            child.OnExit();
            _currentIndex++;
        }

        OnExit();
        return _state = BTNodeState.Success;
    }
}

// ================================================================

/// <summary>Selector — 依次尝试，任一成功则成功，全部失败则失败</summary>
[System.Serializable]
public class BTSelector : BTComposite
{
    public override BTNodeState OnTick()
    {
        while (_currentIndex < children.Count)
        {
            var child = children[_currentIndex];

            if (child.State == BTNodeState.Inactive)
                child.OnEnter(_runner);

            var result = child.OnTick();

            if (result == BTNodeState.Success)
            {
                child.OnExit();
                OnExit();
                return _state = BTNodeState.Success;
            }

            if (result == BTNodeState.Running)
                return _state = BTNodeState.Running;

            // Failure → 下一个
            child.OnExit();
            _currentIndex++;
        }

        OnExit();
        return _state = BTNodeState.Failure;
    }
}

// ================================================================

/// <summary>RandomSelector — 随机选一个子节点执行（支持权重）</summary>
[System.Serializable]
public class BTRandomSelector : BTComposite
{
    [Tooltip("每个子节点的权重（与 children 一一对应，长度不符则等权重）")]
    public List<float> weights = new();

    private int _selectedIndex = -1;

    public override void OnEnter(IBTRunner runner)
    {
        base.OnEnter(runner);
        _selectedIndex = PickWeightedRandom();
    }

    public override BTNodeState OnTick()
    {
        if (_selectedIndex < 0 || _selectedIndex >= children.Count)
        {
            OnExit();
            return _state = BTNodeState.Failure;
        }

        var child = children[_selectedIndex];

        if (child.State == BTNodeState.Inactive)
            child.OnEnter(_runner);

        var result = child.OnTick();

        if (result != BTNodeState.Running)
        {
            child.OnExit();
            OnExit();
            _state = result;
        }

        return _state;
    }

    private int PickWeightedRandom()
    {
        if (children.Count == 0) return -1;

        // 就地计算，避免 new List
        float total = 0f;
        for (int i = 0; i < children.Count; i++)
        {
            float w = (i < weights.Count) ? Mathf.Max(0f, weights[i]) : 1f;
            total += w;
        }

        if (total <= 0f) return Random.Range(0, children.Count);

        float roll = Random.Range(0f, total);
        float cumulative = 0f;
        for (int i = 0; i < children.Count; i++)
        {
            float w = (i < weights.Count) ? Mathf.Max(0f, weights[i]) : 1f;
            cumulative += w;
            if (roll <= cumulative) return i;
        }
        return children.Count - 1;
    }

    public override void Reset()
    {
        base.Reset();
        _selectedIndex = -1;
    }
}

// ================================================================

/// <summary>PrioritySelector — 每帧重新从第一个子节点评估，用于条件持续判断</summary>
[System.Serializable]
public class BTPrioritySelector : BTComposite
{
    public override BTNodeState OnTick()
    {
        for (int i = 0; i < children.Count; i++)
        {
            var child = children[i];

            // PrioritySelector 每帧重置非 Running 的子节点
            if (child.State != BTNodeState.Running)
                child.Reset();

            if (child.State == BTNodeState.Inactive)
                child.OnEnter(_runner);

            var result = child.OnTick();

            if (result == BTNodeState.Success)
            {
                // 中断之前可能还在 Running 的兄弟节点
                for (int j = 0; j < children.Count; j++)
                {
                    if (j != i && children[j].State == BTNodeState.Running)
                        children[j].OnExit();
                }
                child.OnExit();
                return _state = BTNodeState.Success;
            }

            if (result == BTNodeState.Running)
                return _state = BTNodeState.Running;

            child.OnExit();
        }

        return _state = BTNodeState.Failure;
    }
}
