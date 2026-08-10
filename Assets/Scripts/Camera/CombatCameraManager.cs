using UnityEngine;
using Cinemachine;
using System.Collections.Generic;

/// <summary>
/// 战斗摄像头总调度器 — 统一管理 FOV Kick、震屏、阻尼、切换。
/// 挂在 Main Camera 上，配合 CinemachineBrain + ImpulseListener 使用。
///
/// 设计原则：
///   1. 所有震屏走 Cinemachine Impulse 系统（不再直接操作 Transform）
///   2. FOV 通过 CinemachineCameraOffset 或 lens 参数间接控制
///   3. 优先级机制：高优先级效果打断/覆盖低优先级
///   4. LateUpdate 中执行，确保在 Cinemachine 之前完成参数注入
/// </summary>
[RequireComponent(typeof(CinemachineBrain))]
public class CombatCameraManager : MonoBehaviour
{
    public static CombatCameraManager Instance { get; private set; }

    // ============================================================
    // Inspector
    // ============================================================
    [Header("Impulse Listener（震屏接收端）")]
    [Tooltip("主相机的 CinemachineImpulseListener。如果为空，自动从 Main Camera 上查找。")]
    public CinemachineImpulseListener impulseListener;

    [Header("Impulse Source（震屏发射端）")]
    [Tooltip("用于发送震屏信号的 ImpulseSource。如果为空，自动 AddComponent。")]
    public CinemachineImpulseSource impulseSource;

    [Header("FOV Kick")]
    public AnimationCurve fovKickCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);
    public float fovKickRecoverySpeed = 8f;
    public float lightFOVAdd = 1f;
    public float mediumFOVAdd = 3f;
    public float heavyFOVAdd = 6f;
    public float blowFOVAdd = 10f;

    [Header("Impulse Profiles（按力度分级）")]
    public CinemachineImpulseDefinition lightImpulse;
    public CinemachineImpulseDefinition mediumImpulse;
    public CinemachineImpulseDefinition heavyImpulse;
    public CinemachineImpulseDefinition blowImpulse;

    [Header("LookAt Damping Override")]
    [Tooltip("战斗中额外叠加的阻尼，让镜头更稳。")]
    public float combatDampingBoost = 0.8f;
    [Tooltip("阻尼平滑过渡速度")]
    public float dampingTransitionSpeed = 5f;

    [Header("Slow Motion")]
    [Tooltip("慢动作默认强度")]
    public float defaultSlowMoIntensity = 0.3f;
    [Tooltip("慢动作默认持续时间")]
    public float defaultSlowMoDuration = 1.5f;

    // ============================================================
    // Internal State
    // ============================================================
    private Camera _cam;
    private float _baseFOV;
    private float _currentFOVKick;
    private float _fovKickTimer;
    private float _targetDampingBoost;
    private float _appliedDampingBoost;
    private Coroutine _slowMoRoutine;

    // 当前活跃的震屏请求（按优先级）
    private class ShakeRequest
    {
        public int Priority;
        public float Amplitude;
        public Vector3 Direction;
        public float ExpireTime;
    }
    private ShakeRequest _activeShake;
    private float _shakeCooldownEnd;

    // 引用管理
    private ICinemachineCamera _currentLiveCam;

    // ============================================================
    // Unity Lifecycle
    // ============================================================
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        _cam = GetComponent<Camera>();
        if (_cam != null) _baseFOV = _cam.fieldOfView;

        // 自动装配 ImpulseListener
        if (impulseListener == null)
        {
            impulseListener = GetComponent<CinemachineImpulseListener>();
            if (impulseListener == null)
            {
                impulseListener = gameObject.AddComponent<CinemachineImpulseListener>();
                // 默认配置：听所有 6 个方向
                impulseListener.m_ChannelMask = 1;
                impulseListener.m_Gain = 1f;
                impulseListener.m_Use2DDistance = false;
            }
        }

        // 自动装配 ImpulseSource
        if (impulseSource == null)
        {
            impulseSource = GetComponent<CinemachineImpulseSource>();
            if (impulseSource == null)
                impulseSource = gameObject.AddComponent<CinemachineImpulseSource>();
        }
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void LateUpdate()
    {
        if (_cam == null) return;

        // ── FOV Kick 衰减 ──
        UpdateFOVKick();

        // ── 震屏过期清理 ──
        if (_activeShake != null && Time.unscaledTime > _activeShake.ExpireTime)
            _activeShake = null;

        // ── 阻尼平滑过渡 ──
        UpdateDamping();

        // ── 追踪当前活跃的 VCam ──
        TrackActiveVCam();
    }

    // ============================================================
    // Public API — FOV Kick
    // ============================================================
    /// <summary>触发 FOV Kick（被击中 / 斩杀等）</summary>
    public void TriggerFOVKick(AttackForceType forceType)
    {
        float amount = forceType switch
        {
            AttackForceType.Light => lightFOVAdd,
            AttackForceType.Medium => mediumFOVAdd,
            AttackForceType.Heavy => heavyFOVAdd,
            AttackForceType.Blow => blowFOVAdd,
            _ => 1f
        };
        _currentFOVKick = amount;
        _fovKickTimer = 0f;
    }

    /// <summary>按数值触发 FOV Kick（自定义力度）</summary>
    public void TriggerFOVKickRaw(float fovAdd)
    {
        _currentFOVKick = fovAdd;
        _fovKickTimer = 0f;
    }

    // ============================================================
    // Public API — 震屏（统一走 Cinemachine Impulse）
    // ============================================================
    /// <summary>
    /// 触发震屏。自动按 priority 判断是否覆盖当前震动。
    /// priority 越大越优先，同优先级 amplitude 大的胜出。
    /// </summary>
    public void TriggerShake(Vector3 direction, float amplitude, int priority = 0)
    {
        // 冷却期内忽略低优先级请求
        if (Time.unscaledTime < _shakeCooldownEnd && priority < (_activeShake?.Priority ?? 0))
            return;

        // 同优先级且振幅更小 → 忽略
        if (_activeShake != null && priority <= _activeShake.Priority && amplitude <= _activeShake.Amplitude)
            return;

        _activeShake = new ShakeRequest
        {
            Priority = priority,
            Amplitude = amplitude,
            Direction = direction.normalized,
            ExpireTime = Time.unscaledTime + 0.5f // 最长 0.5s 自动过期
        };

        _shakeCooldownEnd = Time.unscaledTime + 0.05f; // 50ms 冷却防抖

        if (impulseSource != null)
        {
            impulseSource.m_ImpulseDefinition = GetImpulseDefinition(amplitude);
            impulseSource.GenerateImpulseWithVelocity(direction * amplitude);
        }
    }

    /// <summary>按 AttackForceType 触发震屏</summary>
    public void TriggerShakeByForce(Vector3 direction, AttackForceType forceType, int priority = 0)
    {
        float amplitude = forceType switch
        {
            AttackForceType.Light => 0.15f,
            AttackForceType.Medium => 0.35f,
            AttackForceType.Heavy => 0.7f,
            AttackForceType.Blow => 1.2f,
            _ => 0.2f
        };
        TriggerShake(direction, amplitude, priority);
    }

    // ============================================================
    // Public API — 阻尼控制
    // ============================================================
    /// <summary>进入战斗状态，叠加额外阻尼</summary>
    public void EnterCombatDamping()
    {
        _targetDampingBoost = combatDampingBoost;
    }

    /// <summary>退出战斗状态，移除额外阻尼</summary>
    public void ExitCombatDamping()
    {
        _targetDampingBoost = 0f;
    }

    /// <summary>手动设置阻尼叠加值</summary>
    public void SetDampingBoost(float boost)
    {
        _targetDampingBoost = boost;
    }

    // ============================================================
    // Public API — 慢动作
    // ============================================================
    public void TriggerSlowMotion(float? intensity = null, float? duration = null)
    {
        float i = intensity ?? defaultSlowMoIntensity;
        float d = duration ?? defaultSlowMoDuration;

        if (_slowMoRoutine != null) StopCoroutine(_slowMoRoutine);
        _slowMoRoutine = StartCoroutine(SlowMotionRoutine(i, d));
    }

    // ============================================================
    // Public API — 瞬时冻结（拼刀 / 斩杀）
    // ============================================================
    public void TriggerFreeze(float duration)
    {
        if (_slowMoRoutine != null) StopCoroutine(_slowMoRoutine);
        _slowMoRoutine = StartCoroutine(FreezeRoutine(duration));
    }

    // ============================================================
    // Internal — FOV Kick
    // ============================================================
    private void UpdateFOVKick()
    {
        if (_currentFOVKick > 0.01f)
        {
            _fovKickTimer += Time.unscaledDeltaTime * fovKickRecoverySpeed;
            float curve = fovKickCurve.Evaluate(_fovKickTimer);

            // ★ P2: 兼容 FreeLook（活跃相机为 FreeLook 时旧代码强转 null，FOV Kick 完全失效）
            float targetFOV = _baseFOV + _currentFOVKick * curve;
            if (_currentLiveCam is CinemachineVirtualCamera vcam)
            {
                var lens = vcam.m_Lens;
                lens.FieldOfView = targetFOV;
                vcam.m_Lens = lens;
            }
            else if (_currentLiveCam is CinemachineFreeLook freeLook)
            {
                SetFreeLookFOV(freeLook, targetFOV);
            }
            else if (_cam != null)
            {
                _cam.fieldOfView = targetFOV;
            }

            if (_fovKickTimer >= 1f)
            {
                ResetFOV();
                _currentFOVKick = 0f;
            }
        }
    }

    private void ResetFOV()
    {
        if (_cam != null) _cam.fieldOfView = _baseFOV;
        if (_currentLiveCam is CinemachineVirtualCamera vcam)
        {
            var lens = vcam.m_Lens;
            lens.FieldOfView = _baseFOV;
            vcam.m_Lens = lens;
        }
        else if (_currentLiveCam is CinemachineFreeLook freeLook)
        {
            SetFreeLookFOV(freeLook, _baseFOV);
        }
    }

    /// <summary>同时写入 FreeLook 本体与全部 rig 的 lens（活跃 rig 由内部决定，写全部最稳）</summary>
    private void SetFreeLookFOV(CinemachineFreeLook freeLook, float fov)
    {
        if (freeLook == null) return;
        var lens = freeLook.m_Lens;
        lens.FieldOfView = fov;
        freeLook.m_Lens = lens;

        if (freeLook.m_Orbits == null) return;
        for (int i = 0; i < freeLook.m_Orbits.Length; i++)
        {
            var rig = freeLook.GetRig(i);
            if (rig == null) continue;
            var rigLens = rig.m_Lens;
            rigLens.FieldOfView = fov;
            rig.m_Lens = rigLens;
        }
    }

    // ============================================================
    // Internal — 阻尼过渡
    // ============================================================
    private void UpdateDamping()
    {
        // ★ P2 修复：旧版每帧 “+= boost” 会让阻尼无限累加（战斗越久镜头越钝）。
        //    改为只累加增量：每帧加 (current - prev)，boost 归 0 时自动归还。
        float prevBoost = _appliedDampingBoost;
        _appliedDampingBoost = Mathf.Lerp(_appliedDampingBoost, _targetDampingBoost,
            Time.unscaledDeltaTime * dampingTransitionSpeed);
        float delta = _appliedDampingBoost - prevBoost;
        if (Mathf.Abs(delta) < 0.0001f) return;

        if (_currentLiveCam == null) return;

        // 对当前活跃相机的所有 Composer / Transposer 组件叠加阻尼增量
        ApplyDampingToComposer(delta);
        ApplyDampingToTransposer(delta);
    }

    private void ApplyDampingToComposer(float extraDamping)
    {
        if (_currentLiveCam == null) return;

        // FreeLook：对 3 条轨道 rig 的 Composer 叠加（活跃 rig 生效）
        if (_currentLiveCam is CinemachineFreeLook freeLook)
        {
            if (freeLook.m_Orbits == null) return;
            for (int i = 0; i < freeLook.m_Orbits.Length; i++)
            {
                var rig = freeLook.GetRig(i);
                if (rig == null) continue;
                var rigComposer = rig.GetCinemachineComponent<CinemachineComposer>();
                if (rigComposer != null)
                {
                    rigComposer.m_HorizontalDamping += extraDamping;
                    rigComposer.m_VerticalDamping += extraDamping;
                }
            }
            return;
        }

        var vcam = _currentLiveCam as CinemachineVirtualCamera;
        if (vcam == null) return;

        var composer = vcam.GetCinemachineComponent<CinemachineComposer>();
        if (composer != null)
        {
            composer.m_HorizontalDamping += extraDamping;
            composer.m_VerticalDamping += extraDamping;
        }

        var groupComposer = vcam.GetCinemachineComponent<CinemachineGroupComposer>();
        if (groupComposer != null)
        {
            groupComposer.m_HorizontalDamping += extraDamping;
            groupComposer.m_VerticalDamping += extraDamping;
        }
    }

    private void ApplyDampingToTransposer(float extraDamping)
    {
        if (_currentLiveCam == null) return;

        // FreeLook：对 3 条轨道 rig 的 Transposer 系组件叠加
        if (_currentLiveCam is CinemachineFreeLook freeLook)
        {
            if (freeLook.m_Orbits == null) return;
            for (int i = 0; i < freeLook.m_Orbits.Length; i++)
            {
                var rig = freeLook.GetRig(i);
                if (rig == null) continue;

                var rigFraming = rig.GetCinemachineComponent<CinemachineFramingTransposer>();
                if (rigFraming != null)
                {
                    rigFraming.m_XDamping += extraDamping;
                    rigFraming.m_YDamping += extraDamping;
                    rigFraming.m_ZDamping += extraDamping;
                }

                var rigOrbital = rig.GetCinemachineComponent<CinemachineOrbitalTransposer>();
                if (rigOrbital != null)
                {
                    rigOrbital.m_XDamping += extraDamping;
                    rigOrbital.m_YDamping += extraDamping;
                    rigOrbital.m_ZDamping += extraDamping;
                }
            }
            return;
        }

        var vcam = _currentLiveCam as CinemachineVirtualCamera;
        if (vcam == null) return;

        var framing = vcam.GetCinemachineComponent<CinemachineFramingTransposer>();
        if (framing != null)
        {
            framing.m_XDamping += extraDamping;
            framing.m_YDamping += extraDamping;
            framing.m_ZDamping += extraDamping;
        }

        var transposer = vcam.GetCinemachineComponent<CinemachineTransposer>();
        if (transposer != null)
        {
            transposer.m_XDamping += extraDamping;
            transposer.m_YDamping += extraDamping;
            transposer.m_ZDamping += extraDamping;
        }

        var orbital = vcam.GetCinemachineComponent<CinemachineOrbitalTransposer>();
        if (orbital != null)
        {
            orbital.m_XDamping += extraDamping;
            orbital.m_YDamping += extraDamping;
            orbital.m_ZDamping += extraDamping;
        }
    }

    // ============================================================
    // Internal — VCam 追踪
    // ============================================================
    private void TrackActiveVCam()
    {
        var brain = GetComponent<CinemachineBrain>();
        if (brain == null) return;

        var activeCam = brain.ActiveVirtualCamera;
        if (activeCam == null)
        {
            _currentLiveCam = null;
            return;
        }

        // ★ P2 修复：旧代码强转 CinemachineVirtualCamera，FreeLook 活跃时为 null，
        //    导致 FOV Kick / 战斗阻尼叠加全部失效。改用 ICinemachineCamera 兼容两者。
        if (activeCam != _currentLiveCam)
        {
            _currentLiveCam = activeCam;

            // 相机切换时重新校准基准 FOV（FreeLook 的 m_Lens 才是真实基准；
            // Camera.fieldOfView 会被 CinemachineBrain 每帧覆盖）
            if (_currentFOVKick <= 0.01f)
                RefreshBaseFOVFromLiveCam();
        }
    }

    private void RefreshBaseFOVFromLiveCam()
    {
        if (_currentLiveCam is CinemachineVirtualCamera vcam)
            _baseFOV = vcam.m_Lens.FieldOfView;
        else if (_currentLiveCam is CinemachineFreeLook freeLook)
            _baseFOV = freeLook.m_Lens.FieldOfView;
        else if (_cam != null)
            _baseFOV = _cam.fieldOfView;
    }

    // ============================================================
    // Internal — Impulse 配置
    // ============================================================
    private CinemachineImpulseDefinition GetImpulseDefinition(float amplitude)
    {
        if (amplitude >= 1.0f && blowImpulse != null) return blowImpulse;
        if (amplitude >= 0.5f && heavyImpulse != null) return heavyImpulse;
        if (amplitude >= 0.25f && mediumImpulse != null) return mediumImpulse;
        if (lightImpulse != null) return lightImpulse;

        // fallback: create a default impulse definition
        var def = new CinemachineImpulseDefinition();
        return def;
    }

    // ============================================================
    // Coroutines
    // ============================================================
    private System.Collections.IEnumerator SlowMotionRoutine(float intensity, float duration)
    {
        float origScale = Time.timeScale;
        Time.timeScale = Mathf.Max(intensity, 0.05f);
        Time.fixedDeltaTime = 0.02f * Time.timeScale;
        yield return new WaitForSecondsRealtime(duration);
        Time.timeScale = origScale;
        Time.fixedDeltaTime = 0.02f * origScale;
    }

    private System.Collections.IEnumerator FreezeRoutine(float duration)
    {
        float origScale = Time.timeScale;
        Time.timeScale = 0f;
        yield return new WaitForSecondsRealtime(duration);
        Time.timeScale = origScale;
        Time.fixedDeltaTime = 0.02f * origScale;
    }
}
