using UnityEngine;
using Cinemachine;

public class LockOnCameraSwitcher : MonoBehaviour
{
    [Header("Cinemachine 引用")]
    [SerializeField] private CinemachineVirtualCamera _lockOnVCam;
    [SerializeField] private CinemachineTargetGroup _targetGroup;

    [Header("优先级设置")]
    [SerializeField] private int _lockedOnPriority = 20; // 锁敌时的高优先级
    [SerializeField] private int _defaultPriority = 9;   // 锁敌相机的默认低优先级

    void Start()
    {
        // 确保启动时锁敌相机是低优先级
        if (_lockOnVCam != null)
        {
            _lockOnVCam.Priority = _defaultPriority;
        }
    }

    void Update()
    {
        // 检查锁敌状态并更新相机
        if (TargetingSystem.Instance != null && TargetingSystem.Instance.HasTarget)
        {
            EnterLockOnMode(TargetingSystem.Instance.CurrentTarget);
        }
        else
        {
            ExitLockOnMode();
        }
    }

    private void EnterLockOnMode(Transform target)
    {
        // 1. 提高锁敌相机的优先级，Cinemachine 会自动切换过去
        _lockOnVCam.Priority = _lockedOnPriority;
        
        // 2. 更新 TargetGroup，将敌人作为第二个目标
        if (_targetGroup.m_Targets.Length > 1)
        {
            _targetGroup.m_Targets[1].target = target;
            _targetGroup.m_Targets[1].weight = 1f; // 给予敌人权重
        }
    }

    private void ExitLockOnMode()
    {
        // 1. 恢复锁敌相机的默认优先级，Cinemachine 会自动切回 FreeLook
        _lockOnVCam.Priority = _defaultPriority;
        
        // 2. 清空 TargetGroup 中的敌人目标
        if (_targetGroup.m_Targets.Length > 1)
        {
            _targetGroup.m_Targets[1].target = null;
            _targetGroup.m_Targets[1].weight = 0f;
        }
    }
}