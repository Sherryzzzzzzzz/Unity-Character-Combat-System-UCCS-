using UnityEngine;

/// <summary>
/// 相机冲击特效 — 委托给 CombatCameraManager 统一调度。
/// 保留此组件作为对外 API 入口（兼容旧调用），实际效果由 CombatCameraManager 执行。
///
/// 挂载在 Main Camera 上即可。
/// </summary>
[RequireComponent(typeof(CombatCameraManager))]
public class CameraImpactEffects : MonoBehaviour
{
    public static CameraImpactEffects Instance { get; private set; }

    private CombatCameraManager _manager;

    private void Awake()
    {
        Instance = this;
        _manager = GetComponent<CombatCameraManager>();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>触发 FOV Kick</summary>
    public void ApplyFOVKick(AttackForceType forceType)
    {
        if (_manager != null)
            _manager.TriggerFOVKick(forceType);
    }

    /// <summary>触发慢动作（斩杀特写）</summary>
    public void ApplySlowMotion(float intensity = 0.3f, float duration = 1.5f)
    {
        if (_manager != null)
            _manager.TriggerSlowMotion(intensity, duration);
    }

    /// <summary>按自定义数值触发 FOV Kick</summary>
    public void ApplyFOVKickRaw(float fovAdd)
    {
        if (_manager != null)
            _manager.TriggerFOVKickRaw(fovAdd);
    }
}
