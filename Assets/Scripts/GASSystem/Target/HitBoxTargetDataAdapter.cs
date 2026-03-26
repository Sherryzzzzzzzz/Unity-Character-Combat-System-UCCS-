using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// HitBox/HurtBox 桥接适配器 — 将现有命中系统的结果转换为 TargetData
/// </summary>
public static class HitBoxTargetDataAdapter
{
    /// <summary>
    /// 从碰撞体列表构建 TargetData
    /// </summary>
    public static TargetData FromColliders(
        AbilitySystemComponent attacker,
        IEnumerable<Collider2D> colliders,
        Vector3 origin,
        Vector3 direction)
    {
        var data = new TargetData
        {
            Origin = origin,
            Direction = direction
        };

        if (colliders == null) return data;

        var seen = new HashSet<AbilitySystemComponent>();
        foreach (var col in colliders)
        {
            if (col == null) continue;
            var asc = col.GetComponent<AbilitySystemComponent>();
            if (asc == null) asc = col.GetComponentInParent<AbilitySystemComponent>();
            if (asc == null) continue;
            if (asc == attacker) continue; // 排除自身
            if (seen.Contains(asc)) continue;

            seen.Add(asc);
            data.TargetActors.Add(asc);
        }

        return data;
    }

    /// <summary>
    /// 从 RaycastHit2D 结果构建 TargetData
    /// </summary>
    public static TargetData FromRaycastHits(
        AbilitySystemComponent attacker,
        RaycastHit2D[] hits,
        Vector3 origin,
        Vector3 direction)
    {
        var data = new TargetData
        {
            Origin = origin,
            Direction = direction,
            HitResults = new List<RaycastHit2D>(hits)
        };

        var seen = new HashSet<AbilitySystemComponent>();
        foreach (var hit in hits)
        {
            if (hit.collider == null) continue;
            var asc = hit.collider.GetComponent<AbilitySystemComponent>();
            if (asc == null) asc = hit.collider.GetComponentInParent<AbilitySystemComponent>();
            if (asc == null) continue;
            if (asc == attacker) continue;
            if (seen.Contains(asc)) continue;

            seen.Add(asc);
            data.TargetActors.Add(asc);
        }

        return data;
    }
}
