using System.Collections;
using UnityEngine;

/// <summary>
/// 相机冲击特效 — FOV Kick + 屏幕震动增强
/// 挂载在 Main Camera 或 Cinemachine Virtual Camera 上
/// </summary>
public class CameraImpactEffects : MonoBehaviour
{
    public static CameraImpactEffects Instance { get; private set; }

    [Header("FOV Kick")]
    public AnimationCurve fovKickCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);
    public float fovKickRecoverySpeed = 8f;

    [Header("参数")]
    public float lightFOVAdd = 1f;
    public float mediumFOVAdd = 3f;
    public float heavyFOVAdd = 6f;
    public float blowFOVAdd = 10f;

    private Camera _cam;
    private float _baseFOV;
    private float _fovKickAmount;
    private float _fovKickTimer;

    private void Awake() { Instance = this; }
    private void OnDestroy() { if (Instance == this) Instance = null; }

    private void Start()
    {
        _cam = Camera.main;
        if (_cam != null) _baseFOV = _cam.fieldOfView;
    }

    /// <summary>
    /// 触发 FOV Kick
    /// </summary>
    public void ApplyFOVKick(AttackForceType forceType)
    {
        float amount = forceType switch
        {
            AttackForceType.Light => lightFOVAdd,
            AttackForceType.Medium => mediumFOVAdd,
            AttackForceType.Heavy => heavyFOVAdd,
            AttackForceType.Blow => blowFOVAdd,
            _ => 1f
        };
        _fovKickAmount = amount;
        _fovKickTimer = 0f;
    }

    /// <summary>
    /// 触发慢动作（斩杀特写）
    /// </summary>
    public void ApplySlowMotion(float intensity = 0.3f, float duration = 1.5f)
    {
        StartCoroutine(SlowMotionRoutine(intensity, duration));
    }

    private void LateUpdate()
    {
        if (_cam == null) return;

        // FOV Kick 衰减
        if (_fovKickAmount > 0.01f)
        {
            _fovKickTimer += Time.unscaledDeltaTime * fovKickRecoverySpeed;
            float curve = fovKickCurve.Evaluate(_fovKickTimer);
            _cam.fieldOfView = _baseFOV + _fovKickAmount * curve;

            if (_fovKickTimer >= 1f)
            {
                _cam.fieldOfView = _baseFOV;
                _fovKickAmount = 0f;
            }
        }
    }

    private IEnumerator SlowMotionRoutine(float intensity, float duration)
    {
        float origScale = Time.timeScale;
        Time.timeScale = intensity;
        Time.fixedDeltaTime = 0.02f * intensity;
        yield return new WaitForSecondsRealtime(duration);
        Time.timeScale = origScale;
        Time.fixedDeltaTime = 0.02f * origScale;
    }
}
