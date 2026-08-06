using UnityEngine;
using Cinemachine;

/// <summary>
/// 锁敌镜头调度器。
///
/// 自由视角 ↔ 锁敌视角的平滑切换。
/// 锁敌时：镜头保持固定俯仰角，仅水平旋转，旋转速度受限。
///
/// 使用前提：
///   - 场景中有 FreeLookLockOn（处理输入屏蔽）
///   - 锁敌 VCam 的 Body 推荐用 3rdPersonFollow（天然水平轨道）
///     或 Transposer + Composer（垂直阻尼极高）
/// </summary>
public class LockOnCameraSwitcher : MonoBehaviour
{
    public static LockOnCameraSwitcher Instance { get; private set; }

    [Header("依赖")]
    [SerializeField] private TargetingSystem _targetingSystem;
    [SerializeField] private CinemachineVirtualCamera _lockOnVCam;
    [SerializeField] private CinemachineTargetGroup _targetGroup;

    [Header("优先级")]
    [SerializeField] private int _lockedOnPriority = 20;
    [SerializeField] private int _defaultPriority = 9;

    [Header("Blend")]
    [SerializeField] private CinemachineBrain _brain;
    [SerializeField] private float _blendDuration = 0.35f;
    [SerializeField] private CinemachineBlendDefinition.Style _blendStyle = CinemachineBlendDefinition.Style.EaseInOut;

    [Header("锁敌镜头参数")]
    [Tooltip("锁敌时 Body 的水平阻尼（越大越稳重）")]
    [SerializeField] private float _lockOnBodyDamping = 1.8f;

    [Tooltip("锁敌时 Body 的垂直阻尼。角色前后移动时防止摄像机上下浮动。建议 5~10")]
    [SerializeField] private float _lockOnBodyVerticalDamping = 8f;

    [Tooltip("锁敌时 Aim 的水平阻尼（越大转向越慢）")]
    [SerializeField] private float _lockOnAimDamping = 1.2f;

    [Tooltip("锁敌时 Aim 的垂直阻尼。不要设太高，否则敌人跳起时会被切出画面。推荐 1.5~4")]
    [SerializeField] private float _lockOnAimVerticalDamping = 1.5f;

    [Header("Target Group 权重")]
    [Tooltip("锁敌时玩家的权重（越大镜头越近玩家）")]
    [SerializeField] private float _playerWeight = 5f;
    [Tooltip("锁敌时敌人的权重")]
    [SerializeField] private float _enemyWeight = 1f;

    [Header("屏幕构图偏移")]
    [Tooltip("锁敌时构图中心在屏幕的水平位置。0.35=玩家偏左、敌人在右，0.5=正中心")]
    [Range(0.2f, 0.8f)]
    [SerializeField] private float _lockOnScreenCenterX = 0.35f;
    [Tooltip("锁敌时构图中心在屏幕的垂直位置。0.5=正中心")]
    [Range(0.3f, 0.7f)]
    [SerializeField] private float _lockOnScreenCenterY = 0.5f;

    [Header("GroupComposer 死区")]
    [SerializeField] private bool _configGroupComposer = true;
    [SerializeField] private float _deadZoneWidth = 0.08f;
    [SerializeField] private float _deadZoneHeight = 0.08f;
    [SerializeField] private float _softZoneWidth = 0.4f;
    [SerializeField] private float _softZoneHeight = 0.4f;

    // 内部状态
    private bool _isLockedOn;
    private int _enemyIdx = -1;
    private int _playerIdx = -1;
    private float _savedPlayerWeight;

    // 保存/恢复的 Body 参数
    private float _savedBodyXDamping, _savedBodyYDamping, _savedBodyZDamping;
    private float _savedAimHDamping, _savedAimVDamping;
    private float _savedAimScreenX, _savedAimScreenY;
    private float _savedCameraSide;
    private Vector3 _savedFollowOffset;

    // 锁敌时强制高度锁定
    private float _lockedHeightY;          // 当前镜头高度（缓动中）
    private float _baseLockedHeightY;      // ★ 锁敌瞬间的固定高度基准（防正反馈漂移）
    private float _followTargetBaseY;      // ★ 锁敌瞬间目标 Y（高度跟随的参考基准）
    private Transform _followTarget;       // VCam 的 Follow 目标
    private float _savedShoulderY;         // 3rdPersonFollow 初始 ShoulderOffset.y

    [Header("高度跟随 (P14)")]
    [Tooltip("★ P14: 锁敌时镜头高度的跟随速度（越大跟得越快）。目标跳跃/被击飞时镜头平滑上抬；平地小幅位移因低速缓动保持稳定")]
    [SerializeField] private float heightFollowSpeed = 3.5f;

    [Header("越肩视角 (P18)")]
    [Tooltip("★ 锁敌时镜头 FOV（越肩视角推荐 60~75）。锁敌瞬间应用，退出时还原\n（场景里锁敌 vcam 配置了 100 的广角，会让画面像俯视/无人机视角）")]
    [SerializeField] private float _lockOnFOV = 70f;
    private float _savedFOV = 100f;

    [Tooltip("★ 锁敌相机高度：相机在 follow 目标上方的高度（米）。\n改小=镜头更低更平视；改大=镜头更高更俯视")]
    [SerializeField] private float _lockOnCameraHeight = 1.2f;

    [Tooltip("★ 瞄准点抬高：镜头瞄准点向上抬的高度（米），让镜头平视敌人胸口而非低头看脚\n（越肩视角的灵魂，推荐 1.2~1.6）")]
    [SerializeField] private float _lockOnAimHeight = 1.4f;
    private Vector3 _savedTrackedOffset = Vector3.zero;

    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Start()
    {
        if (_targetingSystem == null)
            _targetingSystem = GetComponent<TargetingSystem>();

        if (_brain == null)
            _brain = Camera.main?.GetComponent<CinemachineBrain>();
        if (_brain != null)
            _brain.m_DefaultBlend = new CinemachineBlendDefinition(_blendStyle, _blendDuration);

        CacheGroupIndices();
        SaveVcamDefaults();
    }

    void LateUpdate()
    {
        if (_targetingSystem == null) return;

        if (_targetingSystem.HasTarget && !_isLockedOn)
            EnterLockOn(_targetingSystem.CurrentTarget);
        else if (!_targetingSystem.HasTarget && _isLockedOn)
            ExitLockOn();

        // ── 锁敌时每帧强制锁死摄像机高度 ──
        if (_isLockedOn && _lockOnVCam != null)
            EnforceFixedHeight();
    }

    // ================================================================
    // Enter / Exit
    // ================================================================

    private void EnterLockOn(Transform target)
    {
        _isLockedOn = true;

        // ── 0. 记录镜头高度基准（锁敌后每帧写入 vcam 偏移）──
        _followTarget = _lockOnVCam.Follow;
        var cam = Camera.main;
        float camY = cam != null ? cam.transform.position.y : _lockOnVCam.transform.position.y;
        float targetY = _followTarget != null ? _followTarget.position.y : 0f;

        // ★ 修复“锁敌瞬间镜头跳到俯视角”：高度基准必须用 vcam 自身配置的偏移
        //   （3rdPersonFollow 的 ShoulderOffset.y / Transposer 的 FollowOffset.y），
        //   绝不能用“渲染相机高度 - 目标高度”——FreeLook 相机若停在较高轨道，
        //   锁敌瞬间会把锁定镜头也拉到高处，视角直接变俯视。
        var tp = _lockOnVCam.GetCinemachineComponent<Cinemachine3rdPersonFollow>();
        if (tp != null)
        {
            _savedShoulderY = tp.ShoulderOffset.y;
            _lockedHeightY = _savedShoulderY;
        }
        else
        {
            var framing = _lockOnVCam.GetCinemachineComponent<CinemachineFramingTransposer>();
            var transposer = _lockOnVCam.GetCinemachineComponent<CinemachineTransposer>();
            var orbital = _lockOnVCam.GetCinemachineComponent<CinemachineOrbitalTransposer>();
            if (framing != null) _lockedHeightY = framing.m_TrackedObjectOffset.y;
            else if (transposer != null) _lockedHeightY = transposer.m_FollowOffset.y;
            else if (orbital != null) _lockedHeightY = orbital.m_FollowOffset.y;
            else _lockedHeightY = camY - targetY; // 兜底
        }
        // ★ 相机高度：直接以固定值覆盖预制体肩高，保证镜头在 follow 目标上方固定高度（不高不低）
        _lockedHeightY = _lockOnCameraHeight;
        _baseLockedHeightY = _lockedHeightY;
        _followTargetBaseY = targetY;

        // ── 1. 提升 VCam 优先级 ──
        _lockOnVCam.Priority = _lockedOnPriority;

        // ── 1.5 越肩视角：应用锁敌 FOV（锁敌 vcam 默认 100 广角，改成 70 更像越肩）──
        var lens = _lockOnVCam.m_Lens;
        lens.FieldOfView = _lockOnFOV;
        _lockOnVCam.m_Lens = lens;

        // ── 2. 配置 TargetGroup ──
        ConfigureTargetGroup(target);

        // ── 3. 锁定 Body：高阻尼 + 固定垂直 ──
        ConfigureBodyForLockOn();

        // ── 4. 锁定 Aim：高阻尼 ──
        ConfigureAimForLockOn();

        // ── 5. GroupComposer ──
        if (_configGroupComposer)
            ConfigureGroupComposer();

        // ── 6. CombatCameraManager 阻尼叠加 ──
        var mgr = CombatCameraManager.Instance;
        mgr?.EnterCombatDamping();
    }

    private void ExitLockOn()
    {
        _isLockedOn = false;
        _lockOnVCam.Priority = _defaultPriority;
        _followTarget = null;

        // ★ 还原锁敌 FOV
        var lens = _lockOnVCam.m_Lens;
        lens.FieldOfView = _savedFOV;
        _lockOnVCam.m_Lens = lens;

        RestoreTargetGroup();
        RestoreVcamDefaults();

        var mgr = CombatCameraManager.Instance;
        mgr?.ExitCombatDamping();
    }

    /// <summary>
    /// ★ P15: 拼刀/演出镜头结束后调用：按当前锁敌状态重新收敛机位与阻尼。
    /// 拼刀特写用 SetActive 切换会绕过锁敌状态机，结束时若不恢复会导致机位残留。
    /// </summary>
    public void RefreshLockOnState()
    {
        if (_targetingSystem == null)
        {
            _targetingSystem = GetComponent<TargetingSystem>();
            if (_targetingSystem == null) return;
        }

        if (_targetingSystem.HasTarget && !_isLockedOn)
            EnterLockOn(_targetingSystem.CurrentTarget);
        else if (!_targetingSystem.HasTarget && _isLockedOn)
            ExitLockOn();
    }

    // ================================================================
    // TargetGroup 管理
    // ================================================================

    private void CacheGroupIndices()
    {
        if (_targetGroup == null) return;
        var playerGo = PlayerController.Instance != null ? PlayerController.Instance.gameObject : null;
        var targets = _targetGroup.m_Targets;
        for (int i = 0; i < targets.Length; i++)
        {
            if (playerGo != null && targets[i].target == playerGo.transform)
            {
                _playerIdx = i;
                _savedPlayerWeight = targets[i].weight;
            }
        }
    }

    private void ConfigureTargetGroup(Transform enemy)
    {
        if (_targetGroup == null) return;

        var targets = _targetGroup.m_Targets;

        // ── 设置 enemy ──
        _enemyIdx = FindOrClaimSlot(enemy);
        if (_enemyIdx >= 0)
        {
            targets[_enemyIdx].target = enemy;
            targets[_enemyIdx].weight = _enemyWeight;
            targets[_enemyIdx].radius = 0.5f;
        }

        // ── 提升 player 权重 ──
        if (_playerIdx >= 0 && _playerIdx < targets.Length)
        {
            _savedPlayerWeight = targets[_playerIdx].weight;
            targets[_playerIdx].weight = _playerWeight;
        }

        _targetGroup.m_Targets = targets;
    }

    private void RestoreTargetGroup()
    {
        if (_targetGroup == null) return;

        var targets = _targetGroup.m_Targets;

        if (_enemyIdx >= 0 && _enemyIdx < targets.Length)
        {
            targets[_enemyIdx].weight = 0f;
            targets[_enemyIdx].target = null;
        }
        _enemyIdx = -1;

        if (_playerIdx >= 0 && _playerIdx < targets.Length)
            targets[_playerIdx].weight = _savedPlayerWeight;

        _targetGroup.m_Targets = targets;
    }

    private int FindOrClaimSlot(Transform target)
    {
        var targets = _targetGroup.m_Targets;

        // 已有同 target？
        for (int i = 0; i < targets.Length; i++)
            if (targets[i].target == target) return i;

        // 空槽位？
        for (int i = 0; i < targets.Length; i++)
            if (targets[i].target == null && i != _playerIdx) return i;

        // 扩容
        var list = new System.Collections.Generic.List<CinemachineTargetGroup.Target>(targets);
        list.Add(new CinemachineTargetGroup.Target { target = target, weight = _enemyWeight, radius = 0.5f });
        _targetGroup.m_Targets = list.ToArray();
        return list.Count - 1;
    }

    // ================================================================
    // Body / Aim 配置
    // ================================================================

    private void SaveVcamDefaults()
    {
        if (_lockOnVCam == null) return;

        _savedFOV = _lockOnVCam.m_Lens.FieldOfView;

        // Body
        var transposer = _lockOnVCam.GetCinemachineComponent<CinemachineTransposer>();
        if (transposer != null)
        {
            _savedBodyXDamping = transposer.m_XDamping;
            _savedBodyYDamping = transposer.m_YDamping;
            _savedBodyZDamping = transposer.m_ZDamping;
            _savedFollowOffset = transposer.m_FollowOffset;
        }

        var framing = _lockOnVCam.GetCinemachineComponent<CinemachineFramingTransposer>();
        if (framing != null)
        {
            _savedBodyXDamping = framing.m_XDamping;
            _savedBodyYDamping = framing.m_YDamping;
            _savedBodyZDamping = framing.m_ZDamping;
            _savedFollowOffset = framing.m_TrackedObjectOffset;
        }

        var orbital = _lockOnVCam.GetCinemachineComponent<CinemachineOrbitalTransposer>();
        if (orbital != null)
        {
            _savedBodyXDamping = orbital.m_XDamping;
            _savedBodyYDamping = orbital.m_YDamping;
            _savedBodyZDamping = orbital.m_ZDamping;
            _savedFollowOffset = orbital.m_FollowOffset;
        }

        var thirdPerson = _lockOnVCam.GetCinemachineComponent<Cinemachine3rdPersonFollow>();
        if (thirdPerson != null)
            _savedCameraSide = thirdPerson.CameraSide;

        // Aim
        var composer = _lockOnVCam.GetCinemachineComponent<CinemachineComposer>();
        if (composer != null)
        {
            _savedAimHDamping = composer.m_HorizontalDamping;
            _savedAimVDamping = composer.m_VerticalDamping;
            _savedAimScreenX = composer.m_ScreenX;
            _savedAimScreenY = composer.m_ScreenY;
            _savedTrackedOffset = composer.m_TrackedObjectOffset;
        }

        var groupComposer = _lockOnVCam.GetCinemachineComponent<CinemachineGroupComposer>();
        if (groupComposer != null)
        {
            _savedAimHDamping = groupComposer.m_HorizontalDamping;
            _savedAimVDamping = groupComposer.m_VerticalDamping;
            _savedAimScreenX = groupComposer.m_ScreenX;
            _savedAimScreenY = groupComposer.m_ScreenY;
            _savedTrackedOffset = groupComposer.m_TrackedObjectOffset;
        }
    }

    private void ConfigureBodyForLockOn()
    {
        if (_lockOnVCam == null) return;

        // ── 3rdPersonFollow：天然水平轨道，最佳选择 ──
        var thirdPerson = _lockOnVCam.GetCinemachineComponent<Cinemachine3rdPersonFollow>();
        if (thirdPerson != null)
        {
            // Body Y 阻尼独立设高，防止前后移动时摄像机上下浮
            thirdPerson.Damping = new Vector3(_lockOnBodyDamping, _lockOnBodyVerticalDamping, _lockOnBodyDamping);
            // CameraSide: 0=左肩 1=右肩。让玩家在屏幕左侧 → 摄像机偏右肩
            // _lockOnScreenCenterX=0.38 → CameraSide≈0.74（明显右肩）
            // _lockOnScreenCenterX=0.5  → CameraSide=0.5（居中）
            float t = Mathf.InverseLerp(0.5f, 0.2f, _lockOnScreenCenterX);
            thirdPerson.CameraSide = Mathf.Lerp(0.5f, 0.85f, t);
            return;
        }

        // ── FramingTransposer / Transposer / OrbitalTransposer ──
        SetTransposerDamping(_lockOnBodyDamping, _lockOnBodyVerticalDamping, _lockOnBodyDamping);

        // 侧向偏移：FollowOffset +X = 摄像机右移 = 玩家出现在屏幕左侧
        float lateralShift = (_lockOnScreenCenterX - 0.5f) * -5f;
        var framing = _lockOnVCam.GetCinemachineComponent<CinemachineFramingTransposer>();
        if (framing != null)
        {
            var offset = framing.m_TrackedObjectOffset;
            offset.x += lateralShift;
            framing.m_TrackedObjectOffset = offset;
            return;
        }

        var transposer = _lockOnVCam.GetCinemachineComponent<CinemachineTransposer>();
        if (transposer != null)
        {
            var offset = transposer.m_FollowOffset;
            offset.x += lateralShift;
            transposer.m_FollowOffset = offset;
            return;
        }

        var orbital = _lockOnVCam.GetCinemachineComponent<CinemachineOrbitalTransposer>();
        if (orbital != null)
        {
            var offset = orbital.m_FollowOffset;
            offset.x += lateralShift;
            orbital.m_FollowOffset = offset;
        }
    }

    private void ConfigureAimForLockOn()
    {
        if (_lockOnVCam == null) return;

        var composer = _lockOnVCam.GetCinemachineComponent<CinemachineComposer>();
        if (composer != null)
        {
            composer.m_HorizontalDamping = _lockOnAimDamping;
            // 垂直阻尼给高一些（防抖），但不要极端值，确保 GroupComposer 仍能取景
            composer.m_VerticalDamping = _lockOnAimVerticalDamping;
            composer.m_DeadZoneWidth = 0.02f;
            composer.m_DeadZoneHeight = 0.05f;
            composer.m_SoftZoneWidth = 0.3f;
            composer.m_SoftZoneHeight = 0.3f;
            // ★ 构图中心：玩家偏左、敌人偏右（0.35 = 组中心在屏幕 35% 处，玩家在其左、敌人在其右）
            composer.m_ScreenX = _lockOnScreenCenterX;
            composer.m_ScreenY = _lockOnScreenCenterY;
            // ★ 越肩：瞄准点抬高到胸口，镜头平视敌人而不是低头看脚
            composer.m_TrackedObjectOffset = new Vector3(0f, _lockOnAimHeight, 0f);
        }

        var groupComposer = _lockOnVCam.GetCinemachineComponent<CinemachineGroupComposer>();
        if (groupComposer != null)
        {
            groupComposer.m_HorizontalDamping = _lockOnAimDamping;
            // GroupComposer 需要合理的垂直阻尼来自动框住双目标
            groupComposer.m_VerticalDamping = _lockOnAimVerticalDamping;
            groupComposer.m_DeadZoneWidth = _deadZoneWidth;
            groupComposer.m_DeadZoneHeight = _deadZoneHeight;
            groupComposer.m_SoftZoneWidth = _softZoneWidth;
            groupComposer.m_SoftZoneHeight = _softZoneHeight;
            // ★ 构图中心：玩家偏左、敌人偏右
            groupComposer.m_ScreenX = _lockOnScreenCenterX;
            groupComposer.m_ScreenY = _lockOnScreenCenterY;
            // ★ 越肩：瞄准点抬高到胸口
            groupComposer.m_TrackedObjectOffset = new Vector3(0f, _lockOnAimHeight, 0f);
        }

        var hardLock = _lockOnVCam.GetCinemachineComponent<CinemachineHardLockToTarget>();
        if (hardLock != null)
        {
            // HardLock 自身不漂，不需要额外配置
        }
    }

    private void RestoreVcamDefaults()
    {
        if (_lockOnVCam == null) return;

        var transposer = _lockOnVCam.GetCinemachineComponent<CinemachineTransposer>();
        if (transposer != null)
        {
            transposer.m_XDamping = _savedBodyXDamping;
            transposer.m_YDamping = _savedBodyYDamping;
            transposer.m_ZDamping = _savedBodyZDamping;
            transposer.m_FollowOffset = _savedFollowOffset;
        }

        var framing = _lockOnVCam.GetCinemachineComponent<CinemachineFramingTransposer>();
        if (framing != null)
        {
            framing.m_XDamping = _savedBodyXDamping;
            framing.m_YDamping = _savedBodyYDamping;
            framing.m_ZDamping = _savedBodyZDamping;
            framing.m_TrackedObjectOffset = _savedFollowOffset;
        }

        var orbital = _lockOnVCam.GetCinemachineComponent<CinemachineOrbitalTransposer>();
        if (orbital != null)
        {
            orbital.m_XDamping = _savedBodyXDamping;
            orbital.m_YDamping = _savedBodyYDamping;
            orbital.m_ZDamping = _savedBodyZDamping;
            orbital.m_FollowOffset = _savedFollowOffset;
        }

        var thirdPerson = _lockOnVCam.GetCinemachineComponent<Cinemachine3rdPersonFollow>();
        if (thirdPerson != null)
            thirdPerson.CameraSide = _savedCameraSide;

        var composer = _lockOnVCam.GetCinemachineComponent<CinemachineComposer>();
        if (composer != null)
        {
            composer.m_HorizontalDamping = _savedAimHDamping;
            composer.m_VerticalDamping = _savedAimVDamping;
            composer.m_DeadZoneWidth = 0.05f;
            composer.m_DeadZoneHeight = 0.05f;
            composer.m_ScreenX = _savedAimScreenX;
            composer.m_ScreenY = _savedAimScreenY;
            composer.m_TrackedObjectOffset = _savedTrackedOffset;
        }

        var groupComposer = _lockOnVCam.GetCinemachineComponent<CinemachineGroupComposer>();
        if (groupComposer != null)
        {
            groupComposer.m_HorizontalDamping = _savedAimHDamping;
            groupComposer.m_VerticalDamping = _savedAimVDamping;
            groupComposer.m_DeadZoneWidth = 0.1f;
            groupComposer.m_DeadZoneHeight = 0.1f;
            groupComposer.m_ScreenX = _savedAimScreenX;
            groupComposer.m_ScreenY = _savedAimScreenY;
            groupComposer.m_TrackedObjectOffset = _savedTrackedOffset;
        }
    }

    private void SetTransposerDamping(float x, float y, float z)
    {
        var framing = _lockOnVCam.GetCinemachineComponent<CinemachineFramingTransposer>();
        if (framing != null) { framing.m_XDamping = x; framing.m_YDamping = y; framing.m_ZDamping = z; return; }

        var transposer = _lockOnVCam.GetCinemachineComponent<CinemachineTransposer>();
        if (transposer != null) { transposer.m_XDamping = x; transposer.m_YDamping = y; transposer.m_ZDamping = z; return; }

        var orbital = _lockOnVCam.GetCinemachineComponent<CinemachineOrbitalTransposer>();
        if (orbital != null) { orbital.m_XDamping = x; orbital.m_YDamping = y; orbital.m_ZDamping = z; }
    }

    /// <summary>
    /// 每帧强制复位摄像机高度，杜绝前后移动时上下漂浮。
    /// </summary>
    private void EnforceFixedHeight()
    {
        // ★ P14 修正：期望高度 = 固定基准 + 目标高度变化量，再缓动逼近。
        //   绝不把渲染出的相机位置（含阻尼滞后）喂回去——那会形成正反馈循环，
        //   表现为 aim 状态下视角一直往上飘。
        if (_followTarget != null)
        {
            float targetDeltaY = _followTarget.position.y - _followTargetBaseY;
            float desiredHeight = _baseLockedHeightY + targetDeltaY;
            _lockedHeightY = Mathf.Lerp(_lockedHeightY, desiredHeight, Time.deltaTime * heightFollowSpeed);
        }

        // ── 3rdPersonFollow：用跟随高度写 ShoulderOffset.y ──
        var thirdPerson = _lockOnVCam.GetCinemachineComponent<Cinemachine3rdPersonFollow>();
        if (thirdPerson != null)
        {
            var so = thirdPerson.ShoulderOffset;
            so.y = _lockedHeightY;
            thirdPerson.ShoulderOffset = so;
            return;
        }

        // ── Transposer / FramingTransposer：强制复位 FollowOffset.y ──
        var framing = _lockOnVCam.GetCinemachineComponent<CinemachineFramingTransposer>();
        if (framing != null)
        {
            var offset = framing.m_TrackedObjectOffset;
            offset.y = _lockedHeightY;
            framing.m_TrackedObjectOffset = offset;
            return;
        }

        var transposer = _lockOnVCam.GetCinemachineComponent<CinemachineTransposer>();
        if (transposer != null)
        {
            var offset = transposer.m_FollowOffset;
            offset.y = _lockedHeightY;
            transposer.m_FollowOffset = offset;
            return;
        }

        var orbital = _lockOnVCam.GetCinemachineComponent<CinemachineOrbitalTransposer>();
        if (orbital != null)
        {
            var offset = orbital.m_FollowOffset;
            offset.y = _lockedHeightY;
            orbital.m_FollowOffset = offset;
        }
    }

    private void ConfigureGroupComposer()
    {
        if (_lockOnVCam == null) return;
        var gc = _lockOnVCam.GetCinemachineComponent<CinemachineGroupComposer>();
        if (gc != null)
        {
            gc.m_FramingMode = CinemachineGroupComposer.FramingMode.HorizontalAndVertical;
            gc.m_DeadZoneWidth = _deadZoneWidth;
            gc.m_DeadZoneHeight = _deadZoneHeight;
            gc.m_SoftZoneWidth = _softZoneWidth;
            gc.m_SoftZoneHeight = _softZoneHeight;
        }
    }
}
