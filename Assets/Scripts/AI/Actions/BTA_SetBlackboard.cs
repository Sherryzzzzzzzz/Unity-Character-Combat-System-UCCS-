using UnityEngine;

/// <summary>修改黑板值</summary>
[System.Serializable]
public class BTA_SetBlackboard : BTAction
{
    public enum ValueType { Bool, Float, Int, Vector3, GameObject }

    [Header("键名")]
    public string key;

    [Header("值类型")]
    public ValueType valueType = ValueType.Bool;

    [Header("值（按类型填一项）")]
    public float floatValue;
    public int intValue;
    public bool boolValue;
    public Vector3 vectorValue;
    public GameObject goValue;

    public override BTNodeState OnTick()
    {
        var bb = _runner?.Blackboard;
        if (bb == null || string.IsNullOrEmpty(key))
            return _state = BTNodeState.Failure;

        switch (valueType)
        {
            case ValueType.Bool:   bb.Set(key, boolValue); break;
            case ValueType.Float:  bb.Set(key, floatValue); break;
            case ValueType.Int:    bb.Set(key, intValue); break;
            case ValueType.Vector3: bb.Set(key, vectorValue); break;
            case ValueType.GameObject: bb.Set(key, goValue); break;
        }

        return _state = BTNodeState.Success;
    }
}
