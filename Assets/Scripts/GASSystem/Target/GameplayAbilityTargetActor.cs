using UnityEngine;
using System;
using System.Collections.Generic;

/// <summary>
/// 能力瞄准Actor — 对应 UE5 AGameplayAbilityTargetActor
/// 提供可视化瞄准 + 确认/取消 的交互式目标选择
/// </summary>
public class GameplayAbilityTargetActor : MonoBehaviour
{
    public LayerMask TargetLayers = -1;
    public float MaxRange = 20f;
    public bool DebugDraw = true;

    public Func<GameObject, bool> TargetFilter;
    public Action<List<GameObject>> OnTargetsAcquired;
    public Action OnCancelled;

    protected bool IsTargetingActive;
    protected List<GameObject> CurrentTargets = new List<GameObject>();

    public virtual void StartTargeting(GameplayAbility ability)
    {
        IsTargetingActive = true;
        CurrentTargets.Clear();
    }

    public virtual void ConfirmTargeting()
    {
        if (!IsTargetingActive) return;
        IsTargetingActive = false;
        OnTargetsAcquired?.Invoke(new List<GameObject>(CurrentTargets));
    }

    public virtual void CancelTargeting()
    {
        IsTargetingActive = false;
        CurrentTargets.Clear();
        OnCancelled?.Invoke();
    }

    public virtual bool IsConfirmAllowed() => CurrentTargets.Count > 0;

    protected bool PassesFilter(GameObject obj)
    {
        if (obj == null) return false;
        if (TargetFilter != null && !TargetFilter(obj)) return false;
        var asc = obj.GetComponentInParent<AbilitySystemComponent>();
        return asc != null;
    }

    protected virtual void OnDrawGizmosSelected()
    {
        if (!DebugDraw) return;
    }
}

/// <summary>
/// 球形瞄准 — 对应 UE5 AGameplayAbilityTargetActor_Radius
/// </summary>
public class TargetActor_Radius : GameplayAbilityTargetActor
{
    public float Radius = 5f;
    public Transform AimOrigin;

    public override void StartTargeting(GameplayAbility ability)
    {
        base.StartTargeting(ability);
        if (AimOrigin == null) AimOrigin = transform;
    }

    private void Update()
    {
        if (!IsTargetingActive) return;
        var origin = AimOrigin != null ? AimOrigin.position : transform.position;
        var hits = Physics.OverlapSphere(origin, Radius, TargetLayers);
        CurrentTargets.Clear();
        foreach (var hit in hits)
            if (PassesFilter(hit.gameObject))
                CurrentTargets.Add(hit.gameObject);
    }

    protected override void OnDrawGizmosSelected()
    {
        if (!DebugDraw) return;
        var origin = AimOrigin != null ? AimOrigin.position : transform.position;
        Gizmos.color = new Color(0, 1, 0, 0.3f);
        Gizmos.DrawWireSphere(origin, Radius);
    }
}

/// <summary>
/// 射线瞄准 — 对应 UE5 AGameplayAbilityTargetActor_SingleLineTrace
/// </summary>
public class TargetActor_Trace : GameplayAbilityTargetActor
{
    public float TraceRadius = 0.5f;
    public Transform AimOrigin;
    public Vector3 AimDirection = Vector3.forward;

    public override void StartTargeting(GameplayAbility ability)
    {
        base.StartTargeting(ability);
        if (AimOrigin == null) AimOrigin = transform;
    }

    private void Update()
    {
        if (!IsTargetingActive) return;
        var origin = AimOrigin != null ? AimOrigin.position : transform.position;
        var dir = AimOrigin != null ? AimOrigin.forward : transform.TransformDirection(AimDirection);

        CurrentTargets.Clear();

        if (TraceRadius > 0.01f)
        {
            var hits = Physics.SphereCastAll(origin, TraceRadius, dir, MaxRange, TargetLayers);
            foreach (var hit in hits)
                if (PassesFilter(hit.collider.gameObject))
                    CurrentTargets.Add(hit.collider.gameObject);
        }
        else
        {
            var hits = Physics.RaycastAll(origin, dir, MaxRange, TargetLayers);
            foreach (var hit in hits)
                if (PassesFilter(hit.collider.gameObject))
                    CurrentTargets.Add(hit.collider.gameObject);
        }
    }

    protected override void OnDrawGizmosSelected()
    {
        if (!DebugDraw) return;
        var origin = AimOrigin != null ? AimOrigin.position : transform.position;
        var dir = AimOrigin != null ? AimOrigin.forward : transform.TransformDirection(AimDirection);
        Gizmos.color = Color.red;
        Gizmos.DrawRay(origin, dir * MaxRange);
        if (TraceRadius > 0.01f)
            Gizmos.DrawWireSphere(origin + dir * MaxRange, TraceRadius);
    }
}
