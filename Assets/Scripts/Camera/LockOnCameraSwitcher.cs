using UnityEngine;
using Cinemachine;

public class LockOnCameraSwitcher : MonoBehaviour
{
    [Header("依赖组件")]
    [Tooltip("玩家身上的 TargetingSystem 组件")]
    [SerializeField] private TargetingSystem _targetingSystem;
    
    [Header("Cinemachine 引用")]
    [SerializeField] private CinemachineVirtualCamera _lockOnVCam;
    [SerializeField] private CinemachineTargetGroup _targetGroup;

    [Header("优先级设置")]
    [SerializeField] private int _lockedOnPriority = 20;
    [SerializeField] private int _defaultPriority = 9;

    void Start()
    {
        _targetingSystem = GetComponent<TargetingSystem>();
        if (_lockOnVCam != null)
        {
            _lockOnVCam.Priority = _defaultPriority;
        }
    }

    // 在 LateUpdate 中检查，确保在目标被最终确定后再更新相机
    void LateUpdate()
    {
        if (_targetingSystem == null) return;
        if (_targetingSystem.HasTarget)
        {
            if (_lockOnVCam.Priority != _lockedOnPriority)
            {
                EnterLockOnMode(_targetingSystem.CurrentTarget);
            }
        }
        else
        {
            if (_lockOnVCam.Priority != _defaultPriority)
            {
                ExitLockOnMode();
            }
        }
    }

    private void EnterLockOnMode(Transform target)
    {
        _lockOnVCam.Priority = _lockedOnPriority;
        
        if (_targetGroup != null && _targetGroup.m_Targets.Length > 1)
        {
            _targetGroup.m_Targets[1].target = target;
            _targetGroup.m_Targets[1].weight = 1f;
        }
    }

    private void ExitLockOnMode()
    {
        _lockOnVCam.Priority = _defaultPriority;
        
        if (_targetGroup != null && _targetGroup.m_Targets.Length > 1)
        {
            _targetGroup.m_Targets[1].target = null;
            _targetGroup.m_Targets[1].weight = 0f;
        }
    }
}