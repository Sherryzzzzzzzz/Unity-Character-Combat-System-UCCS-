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

    [Header("Blend Settings")]
    [Tooltip("CinemachineBrain on the main camera. Auto-detected if left null.")]
    [SerializeField] private CinemachineBrain _brain;
    [Tooltip("Duration of the blend when switching camera modes (seconds).")]
    [SerializeField] private float _blendDuration = 0.3f;
    [Tooltip("Blend style: EaseInOut feels smooth; Cut is instant.")]
    [SerializeField] private CinemachineBlendDefinition.Style _blendStyle = CinemachineBlendDefinition.Style.EaseInOut;

    [Header("Lock-on Tracking Damping")]
    [Tooltip("Extra damping applied to the vcam body/aim when locked on. "
           + "Higher = smoother orbit when player rolls past the enemy. "
           + "Prevents the hard camera flip when crossing the target.")]
    [SerializeField] private float _lockOnDamping = 1.5f;
    [Tooltip("Damping when NOT locked on (restored from vcam defaults). "
           + "Keep at 0 to leave the vcam's own inspector values untouched.")]
    [SerializeField] private float _defaultDamping = 0f;

    [Header("Target Group Weighting")]
    [Tooltip("Weight multiplier for the player in the target group when locked on. "
           + "Higher = camera stays closer to player, keeping them centered on screen. "
           + "Default 1:1 with enemy pushes the player to the screen edge.")]
    [SerializeField] private float _playerLockOnWeight = 5f;
    [Tooltip("Weight for the enemy in the target group when locked on.")]
    [SerializeField] private float _enemyLockOnWeight = 1f;

    // Cached original values for restore on exit
    private float _originalPlayerWeight;
    private float _originalEnemyWeight;

    void Start()
    {
        _targetingSystem = GetComponent<TargetingSystem>();
        if (_lockOnVCam != null)
        {
            _lockOnVCam.Priority = _defaultPriority;
        }

        // 【FIX 1】Smooth blend between free-look ↔ lock-on virtual cameras
        if (_brain == null)
            _brain = Camera.main?.GetComponent<CinemachineBrain>();
        if (_brain != null)
        {
            _brain.m_DefaultBlend = new CinemachineBlendDefinition(_blendStyle, _blendDuration);
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
            // Cache original weights so we can restore them on exit
            _originalPlayerWeight = _targetGroup.m_Targets[0].weight;
            _originalEnemyWeight = _targetGroup.m_Targets[1].weight;

            // Set enemy as the second target
            _targetGroup.m_Targets[1].target = target;
            _targetGroup.m_Targets[1].weight = _enemyLockOnWeight;

            // 【FIX 3】Give the player higher weight so the camera stays centered on them.
            // A 1:1 weight with the enemy pushes the player to the screen edge.
            // With 3:1, the group center is much closer to the player, keeping them visible.
            _targetGroup.m_Targets[0].weight = _playerLockOnWeight;
        }

        // 【FIX 2】Apply heavy damping to the lock-on vcam body/aim so the
        // camera ORBITS smoothly instead of doing a hard 180° flip when the
        // player rolls past the locked enemy.
        ApplyVcamDamping(_lockOnDamping);

        // 【FIX 4】Configure GroupComposer dead zones to keep all targets on screen
        ApplyGroupComposerFraming();
    }

    private void ExitLockOnMode()
    {
        _lockOnVCam.Priority = _defaultPriority;

        if (_targetGroup != null && _targetGroup.m_Targets.Length > 1)
        {
            _targetGroup.m_Targets[1].target = null;
            _targetGroup.m_Targets[1].weight = 0f;

            // Restore original player weight
            _targetGroup.m_Targets[0].weight = _originalPlayerWeight;
        }

        // Restore default damping values
        ApplyVcamDamping(_defaultDamping);
    }

    /// <summary>
    /// Ensures the GroupComposer uses horizontal+vertical framing with a dead zone
    /// so both the player and the locked enemy stay visible on screen.
    /// </summary>
    private void ApplyGroupComposerFraming()
    {
        if (_lockOnVCam == null) return;

        var groupComposer = _lockOnVCam.GetCinemachineComponent<CinemachineGroupComposer>();
        if (groupComposer != null)
        {
            // FramingMode.HorizontalAndVertical keeps all targets within the dead zone
            groupComposer.m_FramingMode = CinemachineGroupComposer.FramingMode.HorizontalAndVertical;
            // Small dead zone so the camera doesn't drift on tiny movements
            groupComposer.m_DeadZoneWidth  = 0.1f;
            groupComposer.m_DeadZoneHeight = 0.1f;
            // Soft zone catches targets before they hit the edge
            groupComposer.m_SoftZoneWidth  = 0.5f;
            groupComposer.m_SoftZoneHeight = 0.5f;
        }
    }

    /// <summary>
    /// Applies a damping override to every body/aim component on the lock-on vcam.
    /// This smooths out sudden camera rotations when the player crosses the locked target.
    /// </summary>
    private void ApplyVcamDamping(float damping)
    {
        if (_lockOnVCam == null) return;

        // Aim: CinemachineComposer / GroupComposer / HardLockToTarget
        var composer = _lockOnVCam.GetCinemachineComponent<CinemachineComposer>();
        if (composer != null)
        {
            composer.m_HorizontalDamping = damping;
            composer.m_VerticalDamping   = damping;
            // Small dead zone so player stays more centered
            composer.m_DeadZoneWidth  = 0.05f;
            composer.m_DeadZoneHeight = 0.05f;
        }

        var groupComposer = _lockOnVCam.GetCinemachineComponent<CinemachineGroupComposer>();
        if (groupComposer != null)
        {
            groupComposer.m_HorizontalDamping = damping;
            groupComposer.m_VerticalDamping   = damping;
        }

        // Body: 3rdPersonFollow / FramingTransposer / Transposer / OrbitalTransposer
        var thirdPerson = _lockOnVCam.GetCinemachineComponent<Cinemachine3rdPersonFollow>();
        if (thirdPerson != null)
        {
            thirdPerson.Damping = new Vector3(damping, damping, damping);
        }

        var framing = _lockOnVCam.GetCinemachineComponent<CinemachineFramingTransposer>();
        if (framing != null)
        {
            framing.m_XDamping = damping;
            framing.m_YDamping = damping;
            framing.m_ZDamping = damping;
            framing.m_LookaheadSmoothing = damping;
        }

        var transposer = _lockOnVCam.GetCinemachineComponent<CinemachineTransposer>();
        if (transposer != null)
        {
            transposer.m_XDamping = damping;
            transposer.m_YDamping = damping;
            transposer.m_ZDamping = damping;
        }

        var orbital = _lockOnVCam.GetCinemachineComponent<CinemachineOrbitalTransposer>();
        if (orbital != null)
        {
            orbital.m_XDamping = damping;
            orbital.m_YDamping = damping;
            orbital.m_ZDamping = damping;
        }
    }
}