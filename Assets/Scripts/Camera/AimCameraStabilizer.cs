using UnityEngine;
using Cinemachine;

/// <summary>
/// 挂在 CinemachineTargetGroup 所在 GameObject 上。
/// 当敌人太靠近玩家时，平滑降低敌人在 TargetGroup 中的权重，
/// 避免 LockOn 镜头因目标过近而剧烈晃动摇摆。
///
/// 相比旧版改进：
///   - 平滑过渡（SmoothDamp）而非突变
///   - 不再每帧新建 GameObject
///   - 正确处理 TargetGroup 成员查找
///   - 支持动态目标切换
/// </summary>
[RequireComponent(typeof(CinemachineTargetGroup))]
public class AimCameraStabilizer : MonoBehaviour
{
    [Header("冻结参数")]
    [Tooltip("低于此水平距离时开始降低敌人权重")]
    public float freezeDistance = 3.5f;
    [Tooltip("完全冻结的权重值（0 = 完全忽略敌人）")]
    public float frozenWeight = 0f;
    [Tooltip("正常跟随时的权重")]
    public float normalWeight = 1f;
    [Tooltip("权重平滑过渡速度")]
    public float weightSmoothSpeed = 6f;

    private CinemachineTargetGroup _group;
    private Transform _player;
    private TargetingSystem _targeting;
    private int _enemyMemberIndex = -1;
    private Transform _currentEnemy;
    private float _currentWeight = 1f;
    private float _targetWeight = 1f;

    void Awake()
    {
        _group = GetComponent<CinemachineTargetGroup>();
        var go = PlayerController.Instance != null ? PlayerController.Instance.gameObject : null;
        if (go != null) _player = go.transform;
        _targeting = FindFirstObjectByType<TargetingSystem>();
    }

    void LateUpdate()
    {
        if (_player == null || _targeting == null || _group == null) return;

        var enemy = _targeting.HasTarget ? _targeting.CurrentTarget : null;

        // ── 目标切换检测 ──
        if (enemy != _currentEnemy)
        {
            _currentEnemy = enemy;
            _enemyMemberIndex = FindEnemyIndex(enemy);
        }

        // ── 无目标：重置 ──
        if (enemy == null)
        {
            _targetWeight = normalWeight;
            _currentWeight = normalWeight;
            return;
        }

        // ── 距离判断 ──
        float dx = enemy.position.x - _player.position.x;
        float dz = enemy.position.z - _player.position.z;
        float hDist = Mathf.Sqrt(dx * dx + dz * dz);

        _targetWeight = (hDist < freezeDistance && hDist > 0.001f) ? frozenWeight : normalWeight;

        // ── 平滑过渡权重 ──
        float prevWeight = _currentWeight;
        _currentWeight = Mathf.Lerp(_currentWeight, _targetWeight,
            Time.unscaledDeltaTime * weightSmoothSpeed);

        // 只在权重发生变化时才写入
        if (Mathf.Abs(_currentWeight - prevWeight) > 0.0001f)
        {
            SetEnemyWeight(_currentWeight);
        }
    }

    /// <summary>设置敌人的 TargetGroup 权重</summary>
    private void SetEnemyWeight(float w)
    {
        if (_enemyMemberIndex < 0 || _enemyMemberIndex >= _group.m_Targets.Length) return;

        var targets = _group.m_Targets;
        targets[_enemyMemberIndex].weight = w;
        _group.m_Targets = targets;
    }

    /// <summary>在 TargetGroup 中查找特定 enemy 的索引</summary>
    private int FindEnemyIndex(Transform enemy)
    {
        if (enemy == null) return -1;

        var targets = _group.m_Targets;
        for (int i = 0; i < targets.Length; i++)
        {
            // 目标可能是 enemy 本身，也可能是一个 proxy
            if (targets[i].target == enemy) return i;
        }
        return -1;
    }

    void OnDestroy()
    {
        // 清理：不再需要，旧版的 proxy GameObject 已被移除
    }
}
