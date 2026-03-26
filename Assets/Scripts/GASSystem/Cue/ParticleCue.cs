using UnityEngine;

/// <summary>
/// 粒子 Cue — 管理粒子效果的实例化和生命周期
/// </summary>
public class ParticleCue : MonoBehaviour, IGameplayCue
{
    [Tooltip("Instant 效果触发时实例化的粒子预制体")]
    public GameObject executeParticlePrefab;

    [Tooltip("Duration 效果施加时实例化的粒子预制体")]
    public GameObject addParticlePrefab;

    [Tooltip("粒子挂载偏移")]
    public Vector3 offset = Vector3.zero;

    [Tooltip("Instant 粒子自动销毁延迟（秒）")]
    public float autoDestroyDelay = 3f;

    private GameObject _activeParticleInstance;

    public void OnExecute(GameObject target, GameplayEffectSpec spec)
    {
        if (executeParticlePrefab == null) return;

        var instance = Instantiate(executeParticlePrefab, target.transform.position + offset, Quaternion.identity);
        if (autoDestroyDelay > 0f)
            Destroy(instance, autoDestroyDelay);
    }

    public void OnAdd(GameObject target, GameplayEffectSpec spec)
    {
        if (addParticlePrefab == null) return;

        _activeParticleInstance = Instantiate(addParticlePrefab, target.transform);
        _activeParticleInstance.transform.localPosition = offset;
    }

    public void OnRemove(GameObject target)
    {
        if (_activeParticleInstance != null)
        {
            Destroy(_activeParticleInstance);
            _activeParticleInstance = null;
        }
    }
}
