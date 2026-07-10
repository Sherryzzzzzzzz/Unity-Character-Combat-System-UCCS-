using UnityEngine;

// ============================================================
// GameplayEffectContext.cs — 对应 UE5 FGameplayEffectContext
// 效果上下文：携带"谁发起的"、"从哪里"、"打在谁身上"等信息
// ============================================================

/// <summary>
/// 效果上下文 — 对应 UE5 FGameplayEffectContext
///
/// 在 GE 应用时携带完整的上下文信息，包括：
/// - 发起者(Instigator): 谁造成了这个效果
/// - 来源对象(SourceObject): 用什么武器/技能造成的
/// - 命中点: 世界空间位置
/// - 本地控制标记: 用于判断是否可以预测
/// </summary>
public class GameplayEffectContext
{
    /// <summary>发起者的ASC（可为null）</summary>
    public AbilitySystemComponent InstigatorASC;

    /// <summary>发起者Actor（可为null）</summary>
    public GameObject Instigator;

    /// <summary>直接来源对象（如武器、技能特效等）</summary>
    public GameObject SourceObject;

    /// <summary>效果来源世界位置</summary>
    public Vector3 Origin = Vector3.zero;

    /// <summary>命中方向（法线）</summary>
    public Vector3 Normal = Vector3.forward;

    /// <summary>命中结果（可携带Collider、HitPoint等信息）</summary>
    public HitResultInfo HitResult;

    /// <summary>本地玩家是否控制了发起者</summary>
    public bool IsInstigatorLocallyControlled;

    /// <summary>效果等级</summary>
    public float EffectLevel = 1f;

    /// <summary>SetByCaller magnitudes (Tag → Value)</summary>
    public System.Collections.Generic.Dictionary<GameplayTagSO, float> SetByCallerMagnitudes =
        new System.Collections.Generic.Dictionary<GameplayTagSO, float>();

    /// <summary>创建一个空上下文</summary>
    public GameplayEffectContext() { }

    /// <summary>从发起者ASC创建</summary>
    public GameplayEffectContext(AbilitySystemComponent inInstigatorASC, GameObject inInstigator)
    {
        InstigatorASC = inInstigatorASC;
        Instigator = inInstigator;
        IsInstigatorLocallyControlled = IsLocallyControlled(inInstigator);
    }

    /// <summary>
    /// 从已有上下文复制创建
    /// </summary>
    public GameplayEffectContext Clone()
    {
        var clone = new GameplayEffectContext
        {
            InstigatorASC = this.InstigatorASC,
            Instigator = this.Instigator,
            SourceObject = this.SourceObject,
            Origin = this.Origin,
            Normal = this.Normal,
            HitResult = this.HitResult,
            IsInstigatorLocallyControlled = this.IsInstigatorLocallyControlled,
            EffectLevel = this.EffectLevel,
        };

        foreach (var kvp in SetByCallerMagnitudes)
            clone.SetByCallerMagnitudes[kvp.Key] = kvp.Value;

        return clone;
    }

    /// <summary>
    /// 获取"效果发起者"的Actor
    /// </summary>
    public GameObject GetInstigator()
    {
        return Instigator != null ? Instigator :
               (InstigatorASC != null ? InstigatorASC.gameObject : null);
    }

    /// <summary>
    /// 获取效果发起者的ASC
    /// </summary>
    public AbilitySystemComponent GetInstigatorASC()
    {
        if (InstigatorASC != null) return InstigatorASC;
        if (Instigator != null) return Instigator.GetComponent<AbilitySystemComponent>();
        return null;
    }

    /// <summary>
    /// 添加 SetByCaller Magnitude
    /// </summary>
    public void AddSetByCallerMagnitude(GameplayTagSO tag, float magnitude)
    {
        if (tag != null)
            SetByCallerMagnitudes[tag] = magnitude;
    }

    /// <summary>
    /// 获取 SetByCaller Magnitude
    /// </summary>
    public float GetSetByCallerMagnitude(GameplayTagSO tag, float defaultIfNotFound = 0f)
    {
        if (tag != null && SetByCallerMagnitudes.TryGetValue(tag, out var magnitude))
            return magnitude;
        return defaultIfNotFound;
    }

    /// <summary>
    /// 判断一个GameObject是否由本地玩家控制
    /// </summary>
    private static bool IsLocallyControlled(GameObject go)
    {
        if (go == null) return false;
        var playerModel = go.GetComponent<PlayerModel>();
        if (playerModel != null) return true; // Player始终是本地控制的

        // 简单判断：PlayerController控制的 = 本地
        var asc = go.GetComponent<AbilitySystemComponent>();
        return asc != null && asc.CompareTag("Player");
    }
}

/// <summary>
/// 命中结果信息 — 对应 UE5 FHitResult 的精简版
/// </summary>
[System.Serializable]
public class HitResultInfo
{
    public Vector3 Location;
    public Vector3 Normal;
    public Vector3 ImpactPoint;
    public Collider HitCollider;
    public GameObject HitObject;
    public float Distance;
    public string PhysicalMaterial;
}
