using System.Collections;
using UnityEngine;

/// <summary>
/// 挂在武器上：挥砍特效控制器。
/// 支持两种模式：
///   A. 火花拖尾（默认，★ 拼刀规格）：世界空间火花粒子沿武器轨迹飞散，
///      Stretch 拉伸成细长火花，替代传统光带拖尾。
///   B. 传统光带拖尾（TrailRenderer）：保留原实现，useSparkTrail = false 时启用。
/// 两种模式都会切换武器下的 VFX 子粒子。
/// </summary>
[RequireComponent(typeof(TrailRenderer))]
public class SlashTrailEffect : MonoBehaviour
{
    [Header("模式")]
    [Tooltip("true = 火花拖尾（拼刀规格，替代光带）；false = 传统 TrailRenderer 光带")]
    public bool useSparkTrail = true;

    [Header("Trail 拖尾（传统模式）")]
    public float trailTime = 0.2f;
    public float startWidth = 0.4f;
    public float endWidth = 0.1f;

    [Header("颜色")]
    public Color lightColor  = new Color(1f, 0.95f, 0.85f, 0.9f);
    public Color mediumColor = new Color(1f, 0.7f, 0.3f, 0.9f);
    public Color heavyColor  = new Color(1f, 0.4f, 0.1f, 0.95f);
    public Color blowColor   = new Color(1f, 0.15f, 0f, 1f);

    [Header("火花拖尾（拼刀规格）")]
    [Tooltip("火花发射速率（每秒粒子数）")]
    public float sparkEmissionRate = 150f;
    [Tooltip("火花寿命（秒）")]
    public float sparkLifetime = 0.35f;
    [Tooltip("火花初始速度范围（米/秒，向四周飞散）")]
    public float sparkMinSpeed = 2.5f;
    public float sparkMaxSpeed = 6f;
    [Tooltip("火花大小范围（白色圆点）")]
    public float sparkMinSize = 0.04f;
    public float sparkMaxSize = 0.09f;
    [Tooltip("火花重力（>0 下落）")]
    public float sparkGravity = 0.5f;
    [Tooltip("火花亮度（1 = 纯白）")]
    public float sparkBrightness = 1f;

    [Header("VFX 子物体（挂在武器下的粒子特效）")]
    [Tooltip("武器下的 VFX 子物体名（若有多个用逗号隔开），留空=全部子粒子系统")]
    public string vfxChildNames = "";

    [Header("材质")]
    public Material slashMaterial;

    private TrailRenderer _trail;
    private Material _runtimeMaterial;
    private ParticleSystem[] _vfxParticles;

    // 火花拖尾粒子系统（程序化创建，挂武器下）
    private ParticleSystem _sparkPS;
    private ParticleSystemRenderer _sparkRenderer;
    private Material _sparkMaterial;
    private bool _sparkActive;

    void Awake()
    {
        _trail = GetComponent<TrailRenderer>();

        if (!Application.isPlaying)
        {
            _trail.enabled = false;
            _trail.Clear();
            return;
        }

        _trail.enabled = false;
        _vfxParticles = GetComponentsInChildren<ParticleSystem>(true);

        if (useSparkTrail)
            CreateSparkTrailSystem();
    }

    void OnDestroy()
    {
        if (_runtimeMaterial != null)
            Destroy(_runtimeMaterial);
        if (_sparkMaterial != null)
            Destroy(_sparkMaterial);
    }

    public void Activate(AttackForceType forceType)
    {
        if (!Application.isPlaying) return;

        // —— 模式 A：火花拖尾 ——
        if (useSparkTrail)
        {
            ApplySparkColor(forceType);
            if (_sparkPS != null && !_sparkActive)
            {
                _sparkPS.Play();
                _sparkActive = true;
            }
            _trail.enabled = false;
            _trail.Clear();

            // ★ 挥砍瞬间：小型冲击波（拼刀规格，与火花拖尾叠加）
            Vector3 sparkPos = transform.position + transform.forward * 0.3f + Vector3.up * 0.1f;
            ShockwaveEffect.SpawnSlashWave(sparkPos, forceType);
        }
        // —— 模式 B：传统光带拖尾 ——
        else
        {
            _trail.time = trailTime;
            _trail.startWidth = startWidth;
            _trail.endWidth = endWidth;
            _trail.Clear();
            _trail.enabled = true;

            var mat = slashMaterial != null ? slashMaterial : GetRuntimeMaterial();
            if (mat != null) _trail.material = mat;

            Color c = GetTrailColor(forceType);
            _trail.startColor = c;
            _trail.endColor = c * 0.3f;
        }

        // —— 子物体 VFX ——
        ToggleVFX(true);

        StopAllCoroutines();
        StartCoroutine(AutoDeactivate());
    }

    public void Deactivate()
    {
        if (!Application.isPlaying) return;
        StopAllCoroutines();
        StartCoroutine(FadeOut());
    }

    public void ForceClear()
    {
        StopAllCoroutines();
        if (_sparkPS != null && _sparkActive)
        {
            _sparkPS.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            _sparkActive = false;
        }
        _trail.enabled = false;
        _trail.Clear();
        ToggleVFX(false);
    }

    // ============================================

    /// <summary>
    /// 程序化创建火花拖尾粒子系统：
    /// 世界空间（武器移动 → 粒子留轨迹）+ Stretch 拉伸（细长火花）+ 向外飞散 + 重力。
    /// </summary>
    private void CreateSparkTrailSystem()
    {
        var go = new GameObject("SparkTrail");
        go.transform.SetParent(transform, false);

        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); // 先停再配参

        var main = ps.main;
        main.playOnAwake = false;
        main.loop = true;
        main.startLifetime = sparkLifetime;
        main.startSpeed = new ParticleSystem.MinMaxCurve(sparkMinSpeed, sparkMaxSpeed);
        main.startSize = new ParticleSystem.MinMaxCurve(sparkMinSize, sparkMaxSize);
        main.startColor = Color.white;
        main.simulationSpace = ParticleSystemSimulationSpace.World; // ★ 关键：武器移动留下轨迹
        main.gravityModifier = sparkGravity;
        main.maxParticles = 800;

        var emit = ps.emission;
        emit.rateOverTime = sparkEmissionRate;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 40f;
        shape.radius = 0.05f;

        var vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.radial = 0.6f;               // 向外飞散
        vel.space = ParticleSystemSimulationSpace.Local;

        var rot = ps.rotationOverLifetime;
        rot.enabled = true;
        rot.z = new ParticleSystem.MinMaxCurve(-180f, 180f); // 旋转增加火花翻滚感

        var col = ps.colorOverLifetime;
        col.enabled = true;
        var g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.3f), new GradientAlphaKey(0f, 1f) }
        );
        col.color = g;

        // —— Renderer：Billboard 白色圆点粒子（拼刀火花）——
        _sparkRenderer = ps.GetComponent<ParticleSystemRenderer>();
        _sparkRenderer.renderMode = ParticleSystemRenderMode.Billboard;
        _sparkRenderer.alignment = ParticleSystemRenderSpace.View;

        var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader != null)
        {
            _sparkMaterial = new Material(shader) { name = "SparkTrail" };
            _sparkMaterial.SetFloat("_Surface", 1f);   // Transparent
            _sparkMaterial.SetFloat("_Blend", 1f);     // Additive（发光火花）
            _sparkMaterial.SetFloat("_Cull", 0f);
            _sparkMaterial.SetFloat("_ZWrite", 0f);
            _sparkMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            _sparkMaterial.EnableKeyword("_BLENDMODE_ADD");
            _sparkMaterial.renderQueue = 3000;
            // 程序化圆形渐变纹理（白色圆点，不依赖资源）
            _sparkMaterial.mainTexture = CreateSparkDotTexture();
            _sparkRenderer.material = _sparkMaterial;
        }

        _sparkPS = ps;
    }

    /// <summary>
    /// 程序化生成白色径向渐变圆点纹理（64×64，边缘柔和）
    /// </summary>
    private Texture2D CreateSparkDotTexture()
    {
        const int size = 64;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        float half = size * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f - half) / half;
                float dy = (y + 0.5f - half) / half;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                // 径向渐变：中心亮、边缘柔和衰减
                float a = Mathf.Clamp01(1f - d);
                a = a * a * (3f - 2f * a); // smoothstep 软化边缘
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }
        tex.Apply();
        return tex;
    }

    /// <summary>
    /// 火花颜色：统一白色粒子（力度仅微调亮度，sparkBrightness=1 为纯白）
    /// </summary>
    private void ApplySparkColor(AttackForceType forceType)
    {
        if (_sparkPS == null) return;
        float bright = forceType switch
        {
            AttackForceType.Light => 0.95f,
            AttackForceType.Medium => 1f,
            AttackForceType.Heavy => 1.05f,
            AttackForceType.Blow => 1.15f,
            _ => 1f
        };
        var main = _sparkPS.main;
        main.startColor = new ParticleSystem.MinMaxGradient(Color.white * (sparkBrightness * bright));
    }

    void ToggleVFX(bool on)
    {
        if (_vfxParticles == null) return;

        foreach (var ps in _vfxParticles)
        {
            // 排除程序化创建的火花系统（它由 Activate/Deactivate 单独控制）
            if (ps == _sparkPS) continue;

            if (!string.IsNullOrEmpty(vfxChildNames))
            {
                bool match = false;
                foreach (var name in vfxChildNames.Split(','))
                    if (ps.name.Trim() == name.Trim()) { match = true; break; }
                if (!match) continue;
            }

            if (on)
            {
                var m = ps.main;
                m.simulationSpace = ParticleSystemSimulationSpace.Local;
                if (!ps.isPlaying) ps.Play();
            }
            else
            {
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }

            ps.gameObject.SetActive(on);
        }
    }

    Color GetTrailColor(AttackForceType t) => t switch
    {
        AttackForceType.Light  => lightColor,
        AttackForceType.Medium => mediumColor,
        AttackForceType.Heavy  => heavyColor,
        AttackForceType.Blow   => blowColor,
        _ => lightColor
    };

    Material GetRuntimeMaterial()
    {
        if (_runtimeMaterial != null) return _runtimeMaterial;

        var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null) return null;

        _runtimeMaterial = new Material(shader) { name = "SlashTrail" };
        _runtimeMaterial.SetFloat("_Surface", 1f);
        _runtimeMaterial.SetFloat("_Blend", 1f);
        _runtimeMaterial.SetFloat("_Cull", 0f);
        _runtimeMaterial.SetFloat("_ZWrite", 0f);
        _runtimeMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        _runtimeMaterial.EnableKeyword("_BLENDMODE_ADD");
        _runtimeMaterial.renderQueue = 3000;
        return _runtimeMaterial;
    }

    IEnumerator AutoDeactivate()
    {
        yield return new WaitForSeconds(trailTime * 3f);
        ForceClear();
    }

    IEnumerator FadeOut()
    {
        ToggleVFX(false);

        // 火花拖尾：渐停发射并快速清空
        if (useSparkTrail && _sparkPS != null)
        {
            var emit = _sparkPS.emission;
            float startRate = emit.rateOverTime.constant;
            float elapsed = 0f;
            float duration = 0.15f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                emit.rateOverTime = Mathf.Lerp(startRate, 0f, elapsed / duration);
                yield return null;
            }
            _sparkPS.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            _sparkActive = false;
            emit.rateOverTime = sparkEmissionRate;
            yield break;
        }

        // 传统光带：渐隐
        float elapsed2 = 0f;
        float fadeDuration = trailTime * 2f;
        float startT = _trail.time;
        while (elapsed2 < fadeDuration)
        {
            elapsed2 += Time.deltaTime;
            _trail.time = Mathf.Max(0f, startT * (1f - elapsed2 / fadeDuration));
            yield return null;
        }
        _trail.enabled = false;
        _trail.Clear();
    }

    public static void DeactivateAllOn(GameObject root)
    {
        if (root == null) return;
        foreach (var st in root.GetComponentsInChildren<SlashTrailEffect>())
            st.Deactivate();
    }
}
