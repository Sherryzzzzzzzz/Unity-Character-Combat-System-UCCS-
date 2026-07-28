using UnityEngine;

/// <summary>
/// 游戏启动时自动应用性能优化设置。
/// 挂在任意场景 GameObject 上即可。
/// </summary>
public class FrameRateBootstrapper : MonoBehaviour
{
    [Header("帧率")]
    [Tooltip("目标帧率，0=不限制")]
    public int targetFrameRate = 60;

    [Header("阴影")]
    [Tooltip("阴影质量")]
    public ShadowQuality shadowQuality = ShadowQuality.HardOnly;
    [Tooltip("阴影距离")]
    public float shadowDistance = 25f;

    [Header("抗锯齿")]
    [Range(0, 8)] public int antiAliasing = 2;

    [Header("摄像机")]
    [Tooltip("主摄像机 FOV")]
    public float cameraFOV = 75f;
    [Tooltip("禁用 HDR")]
    public bool disableHDR = true;

    [Header("物理")]
    [Tooltip("物理更新间隔（秒），默认 0.03 = 33Hz")]
    public float fixedTimestep = 0.03f;

    void Awake()
    {
        DontDestroyOnLoad(gameObject);
        Apply();
    }

    void Apply()
    {
        Application.targetFrameRate = targetFrameRate;
        QualitySettings.vSyncCount = 0;
        QualitySettings.shadows = shadowQuality;
        QualitySettings.shadowDistance = shadowDistance;
        QualitySettings.shadowResolution = ShadowResolution.Low;
        QualitySettings.shadowCascades = 1;
        QualitySettings.antiAliasing = antiAliasing;
        QualitySettings.pixelLightCount = 1;
        QualitySettings.anisotropicFiltering = AnisotropicFiltering.Disable;
        QualitySettings.softParticles = false;

        Time.fixedDeltaTime = fixedTimestep;
        Time.maximumDeltaTime = 0.1f;

        var cam = Camera.main;
        if (cam != null)
        {
            cam.fieldOfView = cameraFOV;
            cam.allowHDR = !disableHDR;
            cam.allowMSAA = false;
        }

        Debug.Log($"[FrameRateBootstrapper] Target={targetFrameRate}fps VSync=off AA={antiAliasing}x Shadows={shadowQuality}/{shadowDistance}m");
    }
}
