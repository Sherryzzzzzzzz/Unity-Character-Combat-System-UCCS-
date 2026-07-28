using UnityEngine;

/// <summary>
/// 全局 VFX 池 — 提供默认命中特效 fallback
/// 放在场景中任意位置即可被 HitFeedbackManager 自动发现
/// </summary>
public class GlobalVFXPool : MonoBehaviour
{
    [Header("Hit VFX (按力度)")]
    public GameObject lightHitVFX;
    public GameObject mediumHitVFX;
    public GameObject heavyHitVFX;
    public GameObject blowHitVFX;

    [Header("Perfect Dodge VFX")]
    public GameObject perfectDodgeVFX;

    [Header("Block Sparks VFX")]
    public GameObject blockSparksVFX;

    [Header("Clash VFX")]
    public GameObject clashVFX;

    [Header("气浪/剑气 (Shockwave/Slash)")]
    public bool useProceduralShockwave = true;

    [Header("Settings")]
    public int poolSize = 10;

    /// <summary>
    /// 按攻击力度生成命中 VFX
    /// </summary>
    public void SpawnHitVFX(AttackForceType forceType, Vector3 position, Quaternion rotation)
    {
        var prefab = forceType switch
        {
            AttackForceType.Light => lightHitVFX,
            AttackForceType.Medium => mediumHitVFX,
            AttackForceType.Heavy => heavyHitVFX,
            AttackForceType.Blow => blowHitVFX,
            _ => lightHitVFX
        };

        if (prefab != null)
            Destroy(Instantiate(prefab, position, rotation), 3f);

        // 剑气冲击波（Medium 及以上力度）
        if (useProceduralShockwave && forceType >= AttackForceType.Medium)
            ShockwaveEffect.SpawnSlashWave(position, forceType);
    }

    /// <summary>
    /// 生成完美闪避 VFX
    /// </summary>
    public void SpawnPerfectDodgeVFX(Vector3 position)
    {
        if (perfectDodgeVFX != null)
            Destroy(Instantiate(perfectDodgeVFX, position, Quaternion.identity), 3f);

        // 完美闪避波纹
        if (useProceduralShockwave)
            ShockwaveEffect.SpawnPerfectDodgeWave(position);
    }

    /// <summary>
    /// 生成格挡火花 VFX
    /// </summary>
    public void SpawnBlockSparks(Vector3 position, Quaternion rotation)
    {
        if (blockSparksVFX != null)
            Destroy(Instantiate(blockSparksVFX, position, rotation), 3f);
    }

    /// <summary>
    /// 生成破防冲击波
    /// </summary>
    public void SpawnGuardBreakWave(Vector3 position)
    {
        if (useProceduralShockwave)
            ShockwaveEffect.SpawnGuardBreak(position);
    }

    /// <summary>
    /// 生成拼刀 VFX
    /// </summary>
    public void SpawnClashVFX(Vector3 position)
    {
        if (clashVFX != null)
            Destroy(Instantiate(clashVFX, position, Quaternion.identity), 3f);

        // 拼刀冲击波
        if (useProceduralShockwave)
            ShockwaveEffect.SpawnSlashWave(position, AttackForceType.Heavy);
    }
}
