using System.Collections;
using UnityEngine;
using Cinemachine;

/// <summary>
/// 统一命中反馈管理器 — 协调 HitStop + VFX + SFX + Camera FX。
/// 挂载在每个可被击中的角色上。
/// 改进：震屏与 FOV Kick 通过 CombatCameraManager 统一调度，避免多源冲突。
/// </summary>
public class HitFeedbackManager : MonoBehaviour
{
    [Header("VFX")]
    public GameObject lightHitVFX;
    public GameObject mediumHitVFX;
    public GameObject heavyHitVFX;
    public GameObject blowHitVFX;

    [Header("SFX")]
    public AudioClip lightHitSound;
    public AudioClip mediumHitSound;
    public AudioClip heavyHitSound;
    public AudioClip blowHitSound;

    [Header("Hit Flash")]
    public Material hitFlashMaterial;
    public float flashDuration = 0.05f;

    [Header("Screen Shake (受击者镜头)")]
    public float lightShake = 0.15f;
    public float mediumShake = 0.35f;
    public float heavyShake = 0.7f;
    public float blowShake = 1.2f;

    private HitStopController _hitStop;
    private CinemachineImpulseSource _impulseSource; // fallback
    private AudioSource _audioSource;
    private SkinnedMeshRenderer[] _renderers;

    private void Awake()
    {
        _hitStop = GetComponent<HitStopController>() ?? gameObject.AddComponent<HitStopController>();
        _impulseSource = GetComponent<CinemachineImpulseSource>();
        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null) _audioSource = gameObject.AddComponent<AudioSource>();
        _renderers = GetComponentsInChildren<SkinnedMeshRenderer>();
    }

    /// <summary>
    /// 执行完整的命中反馈（被击中时调用）
    /// </summary>
    public void PlayHitFeedback(AttackForceType forceType, Vector3 hitPoint, Vector3 attackDir)
    {
        _hitStop.ApplyVictimHitStop(forceType);

        // VFX — 优先走全局 VFX 池（自带分档粒子 + 程序化冲击波），其次使用配置的 Prefab，最后默认粒子
        var pool = UnityEngine.Object.FindFirstObjectByType<GlobalVFXPool>();
        if (pool != null)
        {
            pool.SpawnHitVFX(forceType, hitPoint, Quaternion.LookRotation(-attackDir));
        }
        else
        {
            var vfx = GetVFX(forceType);
            if (vfx != null)
            {
                Instantiate(vfx, hitPoint, Quaternion.LookRotation(-attackDir));
            }
            else
            {
                PlayDefaultHitParticle(forceType, hitPoint, attackDir);
            }
        }

        // SFX
        var sfx = GetSFX(forceType);
        if (sfx != null && _audioSource != null)
            _audioSource.PlayOneShot(sfx);

        // ── Camera Shake：优先走 CombatCameraManager 统一调度 ──
        float shake = GetShake(forceType);
        if (shake > 0f)
        {
            var mgr = CombatCameraManager.Instance;
            if (mgr != null)
            {
                int priority = forceType switch
                {
                    AttackForceType.Blow => 3,
                    AttackForceType.Heavy => 2,
                    AttackForceType.Medium => 1,
                    _ => 0
                };
                mgr.TriggerShake(attackDir, shake, priority);
                mgr.TriggerFOVKick(forceType);
            }
            else if (_impulseSource != null)
            {
                // fallback：直接发 Impulse（如果场景没配 CombatCameraManager）
                _impulseSource.GenerateImpulseWithVelocity(attackDir * shake);
            }
        }

        // Hit Flash — 优先使用自定义材质，否则用 emission 闪白
        if (hitFlashMaterial != null && _renderers != null)
            StartCoroutine(HitFlashRoutine());
        else if (_renderers != null && _renderers.Length > 0)
            StartCoroutine(DefaultHitFlashRoutine(forceType));
    }

    /// <summary>
    /// 默认击中粒子：使用 Unity 基本粒子或 Debug 绘制
    /// </summary>
    private void PlayDefaultHitParticle(AttackForceType forceType, Vector3 hitPoint, Vector3 attackDir)
    {
        // 用 Debug.Draw 提供最低限度的视觉提示（在 Scene 视图可见）
        Color c = forceType switch
        {
            AttackForceType.Light => Color.yellow,
            AttackForceType.Medium => Color.white,
            AttackForceType.Heavy => new Color(1f, 0.5f, 0f),
            AttackForceType.Blow => Color.red,
            _ => Color.white
        };
        Debug.DrawRay(hitPoint, -attackDir * 0.5f, c, 0.5f);

        // 尝试使用 GlobalVFXPool（如果项目中存在）
        var pool = UnityEngine.Object.FindFirstObjectByType<GlobalVFXPool>();
        if (pool != null)
        {
            pool.SpawnHitVFX(forceType, hitPoint, Quaternion.LookRotation(-attackDir));
        }
    }

    /// <summary>
    /// 默认闪白效果（修改材质的 emission 颜色）
    /// </summary>
    private System.Collections.IEnumerator DefaultHitFlashRoutine(AttackForceType forceType)
    {
        if (_renderers == null || _renderers.Length == 0) yield break;

        Color flashColor = forceType switch
        {
            AttackForceType.Light => Color.yellow * 0.5f,
            AttackForceType.Medium => Color.white * 0.6f,
            AttackForceType.Heavy => Color.red * 0.7f,
            AttackForceType.Blow => new Color(1f, 0.2f, 0f) * 1f,
            _ => Color.white * 0.5f
        };

        var originalColors = new Color[_renderers.Length];
        for (int i = 0; i < _renderers.Length; i++)
        {
            originalColors[i] = _renderers[i].material.GetColor("_EmissionColor");
            _renderers[i].material.EnableKeyword("_EMISSION");
            _renderers[i].material.SetColor("_EmissionColor", flashColor);
        }

        float duration = forceType switch
        {
            AttackForceType.Light => 0.05f,
            AttackForceType.Medium => 0.08f,
            AttackForceType.Heavy => 0.12f,
            AttackForceType.Blow => 0.2f,
            _ => 0.05f
        };
        yield return new WaitForSecondsRealtime(duration);

        for (int i = 0; i < _renderers.Length; i++)
        {
            if (_renderers[i] != null)
                _renderers[i].material.SetColor("_EmissionColor", originalColors[i]);
        }
    }

    /// <summary>
    /// 攻击者反馈（轻量：轻微卡肉 + 小震动）
    /// </summary>
    public void PlayAttackerFeedback(AttackForceType forceType, Vector3 hitPoint)
    {
        _hitStop.ApplyAttackerHitStop(forceType);
    }

    private GameObject GetVFX(AttackForceType t) => t switch
    {
        AttackForceType.Light => lightHitVFX,
        AttackForceType.Medium => mediumHitVFX,
        AttackForceType.Heavy => heavyHitVFX,
        AttackForceType.Blow => blowHitVFX,
        _ => lightHitVFX
    };

    private AudioClip GetSFX(AttackForceType t) => t switch
    {
        AttackForceType.Light => lightHitSound,
        AttackForceType.Medium => mediumHitSound,
        AttackForceType.Heavy => heavyHitSound,
        AttackForceType.Blow => blowHitSound,
        _ => lightHitSound
    };

    private float GetShake(AttackForceType t) => t switch
    {
        AttackForceType.Light => lightShake,
        AttackForceType.Medium => mediumShake,
        AttackForceType.Heavy => heavyShake,
        AttackForceType.Blow => blowShake,
        _ => lightShake
    };

    private IEnumerator HitFlashRoutine()
    {
        var originalMats = new Material[_renderers.Length];
        for (int i = 0; i < _renderers.Length; i++)
        {
            originalMats[i] = _renderers[i].material;
            _renderers[i].material = hitFlashMaterial;
        }
        yield return new WaitForSecondsRealtime(flashDuration);
        for (int i = 0; i < _renderers.Length; i++)
            _renderers[i].material = originalMats[i];
    }
}
