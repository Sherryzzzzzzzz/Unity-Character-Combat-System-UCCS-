using UnityEngine;

/// <summary>动作节点（叶子）— 没有子节点，干具体的事</summary>
public abstract class BTAction : BTNode
{
    // 可被子类覆盖以在 Inspector 里显示描述
    public virtual string GetDescription() => GetType().Name;
}
