using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class HitImpactEntry
{
    public string PhysicalMaterialName;
    public GameObject HitVFX;
    public AudioClip HitSFX;
    public float VolumeMult = 1f;
}

public class HitImpactCue : MonoBehaviour, IGameplayCue
{
    public List<HitImpactEntry> impactEntries;
    public GameObject defaultVFX;
    public AudioClip defaultSFX;
    public Transform vfxSpawnPoint;
    private AudioSource _audioSource;

    private void Awake()
    {
        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null) _audioSource = gameObject.AddComponent<AudioSource>();
    }

    public void OnExecute(GameObject target, GameplayEffectSpec spec)
    {
        PlayImpact(target, spec);
    }

    public void OnAdd(GameObject target, GameplayEffectSpec spec)
    {
        PlayImpact(target, spec);
    }

    public void OnRemove(GameObject target) { }

    private void PlayImpact(GameObject target, GameplayEffectSpec spec)
    {
        string materialName = "Default";
        if (spec?.Context?.HitResult != null)
            materialName = spec.Context.HitResult.PhysicalMaterial ?? "Default";

        var entry = impactEntries.Find(e => e.PhysicalMaterialName == materialName)
                    ?? impactEntries.Find(e => e.PhysicalMaterialName == "Default");

        Vector3 spawnPos = vfxSpawnPoint != null ? vfxSpawnPoint.position : target.transform.position;

        var vfx = entry?.HitVFX ?? defaultVFX;
        if (vfx != null)
        {
            Quaternion rot = spec?.Context != null
                ? Quaternion.LookRotation(spec.Context.Normal)
                : Quaternion.identity;
            Instantiate(vfx, spawnPos, rot);
        }

        var sfx = entry?.HitSFX ?? defaultSFX;
        if (sfx != null && _audioSource != null)
            _audioSource.PlayOneShot(sfx, entry?.VolumeMult ?? 1f);
    }
}

public class LoopingGameplayCue : MonoBehaviour, IGameplayCue
{
    public GameObject loopingVFX;
    public AudioClip loopingSFX;
    public bool attachToTarget = true;
    private GameObject _spawnedVFX;
    private AudioSource _audioSource;

    private void Awake() { _audioSource = gameObject.AddComponent<AudioSource>(); }

    public void OnExecute(GameObject target, GameplayEffectSpec spec) { OnAdd(target, spec); }

    public void OnAdd(GameObject target, GameplayEffectSpec spec)
    {
        if (loopingVFX != null)
        {
            _spawnedVFX = attachToTarget
                ? Instantiate(loopingVFX, target.transform)
                : Instantiate(loopingVFX, target.transform.position, Quaternion.identity);

            if (!attachToTarget && _spawnedVFX != null)
            {
                var follower = _spawnedVFX.AddComponent<VFXFollowTarget>();
                follower.target = target.transform;
            }
        }
        if (loopingSFX != null && _audioSource != null)
        {
            _audioSource.clip = loopingSFX;
            _audioSource.loop = true;
            _audioSource.Play();
        }
    }

    public void OnRemove(GameObject target)
    {
        if (_spawnedVFX != null) { Destroy(_spawnedVFX); _spawnedVFX = null; }
        if (_audioSource != null) { _audioSource.Stop(); _audioSource.clip = null; }
    }
}

internal class VFXFollowTarget : MonoBehaviour
{
    public Transform target;
    private void LateUpdate() { if (target != null) transform.position = target.position; else Destroy(gameObject); }
}
