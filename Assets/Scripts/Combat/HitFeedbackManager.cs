using System.Collections;
using UnityEngine;
using Cinemachine;

/// <summary>
/// 统一命中反馈管理器 — 协调 HitStop + VFX + SFX + Camera FX
/// 挂载在每个可被击中的角色上
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
    private CinemachineImpulseSource _impulseSource;
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

        // VFX
        var vfx = GetVFX(forceType);
        if (vfx != null)
            Instantiate(vfx, hitPoint, Quaternion.LookRotation(-attackDir));

        // SFX
        var sfx = GetSFX(forceType);
        if (sfx != null && _audioSource != null)
            _audioSource.PlayOneShot(sfx);

        // Camera Shake
        float shake = GetShake(forceType);
        if (_impulseSource != null && shake > 0f)
            _impulseSource.GenerateImpulseWithVelocity(attackDir * shake);

        // Hit Flash
        if (hitFlashMaterial != null && _renderers != null)
            StartCoroutine(HitFlashRoutine());
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
