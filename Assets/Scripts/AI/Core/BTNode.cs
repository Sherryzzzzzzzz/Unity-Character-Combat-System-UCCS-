using UnityEngine;

/// <summary>行为树节点抽象基类</summary>
[System.Serializable]
public abstract class BTNode
{
    /// <summary>编辑器唯一标识</summary>
    public string guid = System.Guid.NewGuid().ToString();

    /// <summary>编辑器画布位置（仅编辑器使用）</summary>
    public Vector2 editorPosition;

    /// <summary>运行时父节点（由 BTreeRunner 在 Play 时赋值）</summary>
    [System.NonSerialized] public BTNode parent;

    /// <summary>运行时状态</summary>
    [System.NonSerialized] protected BTNodeState _state = BTNodeState.Inactive;

    /// <summary>所属的 BTreeRunner 引用（用于访问黑板）</summary>
    [System.NonSerialized] protected BTreeRunner _runner;

    public BTNodeState State => _state;

    /// <summary>进入节点（首次 Tick 前调用）</summary>
    public virtual void OnEnter(BTreeRunner runner)
    {
        _runner = runner;
        _state = BTNodeState.Running;
    }

    /// <summary>每帧 Tick，子类必须实现</summary>
    public abstract BTNodeState OnTick();

    /// <summary>退出节点（执行完毕或被中断）</summary>
    public virtual void OnExit()
    {
        _state = BTNodeState.Inactive;
    }

    /// <summary>重置节点（用于重复执行）</summary>
    public virtual void Reset()
    {
        _state = BTNodeState.Inactive;
    }
}
