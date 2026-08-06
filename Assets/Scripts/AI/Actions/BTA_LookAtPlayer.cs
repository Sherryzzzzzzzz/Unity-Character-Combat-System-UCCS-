using UnityEngine;

/// <summary>让敌人朝向玩家 — 在攻击前调用使敌人面对目标</summary>
[System.Serializable]
public class BTA_LookAtPlayer : BTAction
{
    [Header("参数")]
    [Tooltip("旋转速度(度/秒)，0=立即转向")]
    public float rotationSpeed = 720f;
    [Tooltip("黑板中玩家 Transform 的键名")]
    public string playerKey = "player";

    private Transform _playerTransform;
    private Transform _selfTransform;

    public override void OnEnter(IBTRunner runner)
    {
        base.OnEnter(runner);
        _selfTransform = runner.transform;
        _playerTransform = runner.Blackboard?.Get<Transform>(playerKey);

        if (_playerTransform == null)
        {
            _state = BTNodeState.Failure;
        }
    }

    public override BTNodeState OnTick()
    {
        if (_playerTransform == null)
            return _state = BTNodeState.Failure;

        Vector3 direction = _playerTransform.position - _selfTransform.position;
        direction.y = 0;

        if (direction.sqrMagnitude < 0.0001f)
            return _state = BTNodeState.Success;

        Quaternion targetRotation = Quaternion.LookRotation(direction);

        if (rotationSpeed <= 0f)
        {
            _selfTransform.rotation = targetRotation;
            return _state = BTNodeState.Success;
        }

        _selfTransform.rotation = Quaternion.RotateTowards(
            _selfTransform.rotation,
            targetRotation,
            rotationSpeed * Time.deltaTime
        );

        float angle = Quaternion.Angle(_selfTransform.rotation, targetRotation);
        return angle < 1f ? _state = BTNodeState.Success : (_state = BTNodeState.Running);
    }
}
