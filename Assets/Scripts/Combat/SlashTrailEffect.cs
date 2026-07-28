using System.Collections;
using UnityEngine;

/// <summary>
/// 挂在武器上：TrailRenderer 拖尾 + 子物体 VFX 粒子 toggle
/// VFX 粒子直接挂在武器下作为子物体，Activate/Deactivate 控制显隐
/// </summary>
[RequireComponent(typeof(TrailRenderer))]
public class SlashTrailEffect : MonoBehaviour
{
    [Header("Trail 拖尾")]
    public float trailTime = 0.2f;
    public float startWidth = 0.4f;
    public float endWidth = 0.1f;

    [Header("颜色")]
    public Color lightColor  = new Color(1f, 0.95f, 0.85f, 0.9f);
    public Color mediumColor = new Color(1f, 0.7f, 0.3f, 0.9f);
    public Color heavyColor  = new Color(1f, 0.4f, 0.1f, 0.95f);
    public Color blowColor   = new Color(1f, 0.15f, 0f, 1f);

    [Header("VFX 子物体（挂在武器下的粒子特效）")]
    [Tooltip("武器下的 VFX 子物体名（若有多个用逗号隔开），留空=全部子粒子系统")]
    public string vfxChildNames = "";

    [Header("材质")]
    public Material slashMaterial;

    private TrailRenderer _trail;
    private Material _runtimeMaterial;
    private ParticleSystem[] _vfxParticles;

    void Awake()
    {
        _trail = GetComponent<TrailRenderer>();

        // 编辑器预览时关掉 TrailRenderer，避免品红/黑色残留
        if (!Application.isPlaying)
        {
            _trail.enabled = false;
            _trail.Clear();
            return;
        }

        _trail.enabled = false;
        _vfxParticles = GetComponentsInChildren<ParticleSystem>(true);
    }

    void OnDestroy()
    {
        if (_runtimeMaterial != null)
            Destroy(_runtimeMaterial);
    }

    public void Activate(AttackForceType forceType)
    {
        // 编辑器预览模式下不显示任何特效
        if (!Application.isPlaying) return;

        // —— Trail ——
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
        _trail.enabled = false;
        _trail.Clear();
        ToggleVFX(false);
    }

    // ============================================

    void ToggleVFX(bool on)
    {
        if (_vfxParticles == null) return;

        foreach (var ps in _vfxParticles)
        {
            if (!string.IsNullOrEmpty(vfxChildNames))
            {
                // 只在名单里的才控制
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
        _trail.enabled = false;
        ToggleVFX(false);
    }

    IEnumerator FadeOut()
    {
        ToggleVFX(false);
        float elapsed = 0f;
        float duration = trailTime * 2f;
        float startT = _trail.time;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            _trail.time = Mathf.Max(0f, startT * (1f - elapsed / duration));
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
