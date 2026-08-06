using UnityEngine;

/// <summary>
/// SubTree — 引用另一个 BTreeAsset 作为子树。
/// （依赖 BTreeAsset，位于运行时程序集 Assembly-CSharp）
/// </summary>
[System.Serializable]
public class BTSubTree : BTNode
{
    [Tooltip("引用的子树资产")]
    public BTreeAsset subTreeAsset;

    private BTNode _subTreeRoot;
    private bool _subTreeRunning;

    public override void OnEnter(IBTRunner runner)
    {
        base.OnEnter(runner);
        _subTreeRunning = false;

        if (subTreeAsset != null && subTreeAsset.rootNode != null)
        {
            _subTreeRoot = CloneNode(subTreeAsset.rootNode);
            _subTreeRoot.OnEnter(runner);
            _subTreeRunning = true;
        }
    }

    public override BTNodeState OnTick()
    {
        if (!_subTreeRunning || _subTreeRoot == null)
            return _state = BTNodeState.Failure;

        var result = _subTreeRoot.OnTick();
        if (result != BTNodeState.Running)
        {
            _subTreeRoot.OnExit();
            _subTreeRunning = false;
        }
        return _state = result;
    }

    public override void Reset()
    {
        base.Reset();
        _subTreeRoot = null;
        _subTreeRunning = false;
    }

    /// <summary>简单深拷贝节点树结构（仅复制数据，不复制运行时状态）</summary>
    private static BTNode CloneNode(BTNode node)
    {
        if (node == null) return null;

        var json = JsonUtility.ToJson(node);
        var clone = JsonUtility.FromJson(json, node.GetType()) as BTNode;
        return clone;
    }
}
