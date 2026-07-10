using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Cue 翻译器 — 对应 UE5 UGameplayCueTranslator
/// 将一个 Cue Tag 翻译/路由到另一个 Tag
/// 例如: "GameplayCue.Weapon.Sword" → "GameplayCue.Weapon.Melee" (所有近战武器共用同一套 Cue)
/// </summary>
[CreateAssetMenu(menuName = "GAS-like/GameplayCueTranslator", fileName = "CueTranslator_")]
public class GameplayCueTranslator : ScriptableObject
{
    [Tooltip("源 Tag（匹配到此后翻译为目标Tag）")]
    public GameplayTagSO SourceTag;
    [Tooltip("目标 Tag（实际触发此Tag的Cue）")]
    public GameplayTagSO TargetTag;

    public bool Matches(GameplayTagSO tag) => SourceTag == tag || (SourceTag != null && SourceTag.HasChild(tag));
}

/// <summary>
/// Cue 集合 — 对应 UE5 UGameplayCueSet
/// 在 GameInstance 级别存在的 Tag → Cue Prefab 全局映射表
/// </summary>
[CreateAssetMenu(menuName = "GAS-like/GameplayCueSet", fileName = "GameplayCueSet")]
public class GameplayCueSet : ScriptableObject
{
    [System.Serializable]
    public struct CueEntry
    {
        public GameplayTagSO cueTag;
        public GameObject cuePrefab; // 必须实现 IGameplayCue
    }

    public List<CueEntry> cueEntries = new List<CueEntry>();
    public List<GameplayCueTranslator> translators = new List<GameplayCueTranslator>();

    /// <summary>
    /// 根据 Tag 查找对应的 Cue Prefab
    /// 会先尝试 CueTranslator 进行 Tag 翻译
    /// </summary>
    public GameObject FindCue(GameplayTagSO tag)
    {
        if (tag == null) return null;

        // 1) 先尝试精确匹配
        foreach (var entry in cueEntries)
            if (entry.cueTag == tag && entry.cuePrefab != null)
                return entry.cuePrefab;

        // 2) 尝试通过 CueTranslator 路由
        foreach (var translator in translators)
        {
            if (translator.Matches(tag))
                return FindCue(translator.TargetTag);
        }

        // 3) 尝试父Tag匹配
        foreach (var entry in cueEntries)
            if (entry.cueTag != null && entry.cueTag.HasChild(tag) && entry.cuePrefab != null)
                return entry.cuePrefab;

        return null;
    }
}

/// <summary>
/// Cue 参数 — 对应 UE5 FGameplayCueParameters
/// </summary>
[System.Serializable]
public class GameplayCueParameters
{
    public Vector3 Location;
    public Vector3 Normal = Vector3.up;
    public GameObject Instigator;
    public GameObject EffectCauser;
    public float Magnitude = 1f;
    public float NormalizedMagnitude;
}

/// <summary>
/// AnimNotify 触发 GameplayCue — 对应 UE5 UAnimNotify_GameplayCue
/// 挂在 Animancer 的 Event 序列中，在动画特定帧触发 Cue
/// </summary>
public class AnimNotifyGameplayCue : MonoBehaviour
{
    public GameplayTagSO cueTag;
    public GameplayCueParameters parameters;

    public void TriggerCue()
    {
        if (cueTag == null) return;
        var cueManager = GameplayCueManager.Instance;
        if (cueManager != null)
            cueManager.ExecuteCue(cueTag, gameObject, null);
    }
}

/// <summary>
/// 3D 瞄准准星 — 对应 UE5 AGameplayAbilityWorldReticle
/// 显示在目标位置的世界空间指示器
/// </summary>
public class GameplayAbilityWorldReticle : MonoBehaviour
{
    public GameObject reticleVisual;
    public Vector3 offset = Vector3.up * 1.5f;
    public float smoothSpeed = 15f;

    private Transform _target;
    private bool _isActive;

    public void SetTarget(Transform target)
    {
        _target = target;
        _isActive = target != null;
        if (reticleVisual != null)
            reticleVisual.SetActive(_isActive);
    }

    public void ClearTarget()
    {
        _target = null;
        _isActive = false;
        if (reticleVisual != null)
            reticleVisual.SetActive(false);
    }

    private void LateUpdate()
    {
        if (!_isActive || _target == null) return;
        Vector3 targetPos = _target.position + offset;
        transform.position = Vector3.Lerp(transform.position, targetPos, Time.deltaTime * smoothSpeed);
        transform.rotation = Camera.main != null
            ? Quaternion.LookRotation(Camera.main.transform.forward)
            : Quaternion.identity;
    }
}
