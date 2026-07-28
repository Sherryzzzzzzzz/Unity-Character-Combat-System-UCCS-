using UnityEngine;
using Cinemachine;

/// <summary>挂在 CameraTargetGroup 上。敌人太近时冻结 LookAt 朝向，避免摄像头乱晃。</summary>
[RequireComponent(typeof(CinemachineTargetGroup))]
public class AimCameraStabilizer : MonoBehaviour
{
    [Tooltip("低于此距离冻结 LookAt")]
    public float freezeDistance = 3.5f;

    private CinemachineTargetGroup _group;
    private Transform _player;
    private TargetingSystem _targeting;
    private Transform _enemyProxy;
    private int _enemyIdx = -1;
    private Transform _realEnemy;
    private bool _frozen;

    void Awake()
    {
        _group = GetComponent<CinemachineTargetGroup>();
        var go = GameObject.FindGameObjectWithTag("Player");
        if (go != null) _player = go.transform;
        _targeting = FindFirstObjectByType<TargetingSystem>();

        var pGo = new GameObject("__EnemyProxy__") { hideFlags = HideFlags.HideAndDontSave };
        _enemyProxy = pGo.transform;
    }

    void LateUpdate()
    {
        if (_player == null || _targeting == null) return;
        var enemy = _targeting.HasTarget ? _targeting.CurrentTarget : null;

        if (enemy != _realEnemy)
        {
            _realEnemy = enemy;
            RebuildMember(enemy);
            _frozen = false;
        }

        if (enemy == null) return;

        float dx = enemy.position.x - _player.position.x;
        float dz = enemy.position.z - _player.position.z;
        float hDist = Mathf.Sqrt(dx * dx + dz * dz);

        if (hDist < freezeDistance && hDist > 0.001f)
        {
            // 太近→ weight=0，TargetGroup 中心 = 纯玩家位置，不跟随敌人
            if (!_frozen)
            {
                SetEnemyWeight(0f);
                _frozen = true;
            }
        }
        else
        {
            // 正常跟随
            _frozen = false;
            SetEnemyWeight(1f);
            _enemyProxy.position = new Vector3(
                enemy.position.x,
                (_player.position.y + enemy.position.y) * 0.5f,
                enemy.position.z
            );
        }
    }

    void SetEnemyWeight(float w)
    {
        if (_enemyIdx < 0 || _enemyIdx >= _group.m_Targets.Length) return;
        var t = _group.m_Targets;
        t[_enemyIdx].weight = w;
        _group.m_Targets = t;
    }

    void RebuildMember(Transform enemy)
    {
        var list = new System.Collections.Generic.List<CinemachineTargetGroup.Target>(_group.m_Targets);
        if (_enemyIdx >= 0 && _enemyIdx < list.Count) list.RemoveAt(_enemyIdx);

        if (enemy != null)
        {
            list.Add(new CinemachineTargetGroup.Target { target = _enemyProxy, weight = 1f, radius = 0.5f });
            _enemyIdx = list.Count - 1;
        }
        else _enemyIdx = -1;

        _group.m_Targets = list.ToArray();
    }

    void OnDestroy() { if (_enemyProxy) Destroy(_enemyProxy.gameObject); }
}
