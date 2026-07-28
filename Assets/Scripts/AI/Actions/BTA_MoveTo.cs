using UnityEngine;

/// <summary>移动到目标位置 — 设置 EnemyModel.moveCommandTarget，帧级移动在 EnemyModel.Update 执行</summary>
[System.Serializable]
public class BTA_MoveTo : BTAction
{
    public enum MoveMode
    {
        [Tooltip("圆形区域 — 在玩家周围随机走位")]
        Circle,
        [Tooltip("左右平移 — 垂直于玩家方向走位")]
        Strafe,
        [Tooltip("冲向玩家")]
        Charge,
    }

    [Header("移动模式")]
    public MoveMode mode = MoveMode.Circle;

    [Header("参数")]
    public float radius = 8f;
    public float stoppingDistance = 1f;

    private EnemyModel _model;
    private EnemyAnimationData _animData;
    private Transform _player;
    private Vector3 _targetPos;
    private bool _started;

    public override void OnEnter(BTreeRunner runner)
    {
        base.OnEnter(runner);
        _model = runner.GetComponent<EnemyModel>();
        _animData = runner.GetComponent<EnemyAnimationData>();
        _player = runner.Blackboard?.Get<Transform>("player");
        _started = false;

        if (_animData != null)
            _animData.CurrentState = EnemyAnimationState.Move;
    }

    public override BTNodeState OnTick()
    {
        if (_model == null) return _state = BTNodeState.Failure;

        if (!_started)
        {
            _targetPos = CalcTarget();
            _model.moveCommandTarget = _targetPos;
            _model.moveCommandStopDist = stoppingDistance;
            _started = true;
            // 第一帧不检查距离，给移动至少一个 BT tick 的时间
            return _state = BTNodeState.Running;
        }

        float dx = _targetPos.x - _model.transform.position.x;
        float dz = _targetPos.z - _model.transform.position.z;
        if (dx * dx + dz * dz <= stoppingDistance * stoppingDistance)
        {
            OnExit();
            return _state = BTNodeState.Success;
        }

        return _state = BTNodeState.Running;
    }

    private Vector3 CalcTarget()
    {
        Vector3 o = _model.transform.position;
        switch (mode)
        {
            case MoveMode.Circle:
                var c = Random.insideUnitCircle * radius;
                return new Vector3(o.x + c.x, o.y, o.z + c.y);
            case MoveMode.Strafe:
                if (_player == null) return o;
                var toP = _player.position - o;
                float sign = Random.value > 0.5f ? 1f : -1f;
                return o + Vector3.Cross(Vector3.up, toP).normalized * sign * Random.Range(radius * 0.5f, radius);
            case MoveMode.Charge:
                return _player != null ? _player.position : o;
            default: return o;
        }
    }

    public override void OnExit()
    {
        if (_model != null)
        {
            _model.moveCommandTarget = null;
            _model.moveDir = Vector2.zero;
        }
        if (_animData != null)
            _animData.CurrentState = EnemyAnimationState.Idle;
        base.OnExit();
    }
}
