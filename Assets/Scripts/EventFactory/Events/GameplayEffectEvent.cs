using UnityEngine;

public enum EffectTargetType
{
    Self,
    NearestEnemy,
    AllInRange
}

[System.Serializable]
public class GameplayEffectEvent : TimelineEventBase, ITimelineEventRuntime
{
    [Header("GAS Effect 配置")]
    public GameplayEffect gameplayEffect;
    public EffectTargetType effectTarget = EffectTargetType.Self;

    [Header("范围搜索（AllInRange 时生效）")]
    public SearchParameters searchParameters = new SearchParameters();

    public override TimelineEventType Type => TimelineEventType.GASEffect;

    public override string GetSummary()
    {
        string effectName = gameplayEffect != null ? gameplayEffect.name : "None";
        return $"GASEffect [{StartFrame}-{EndFrame}] {effectName} → {effectTarget}";
    }

    public void OnStart(GameObject owner)
    {
        if (gameplayEffect == null) return;

        var ownerASC = owner.GetComponent<AbilitySystemComponent>();
        if (ownerASC == null)
        {
            Debug.LogWarning("GameplayEffectEvent: Owner 缺少 AbilitySystemComponent");
            return;
        }

        switch (effectTarget)
        {
            case EffectTargetType.Self:
                ownerASC.ApplyGameplayEffect(gameplayEffect, ownerASC);
                break;

            case EffectTargetType.NearestEnemy:
                var model = owner.GetComponent<PlayerModel>();
                if (model?.nearestEnemy != null)
                {
                    var targetASC = model.nearestEnemy.GetComponent<AbilitySystemComponent>();
                    if (targetASC != null)
                        targetASC.ApplyGameplayEffect(gameplayEffect, ownerASC);
                }
                break;

            case EffectTargetType.AllInRange:
                ExecuteAllInRange(owner, ownerASC);
                break;
        }
    }

    public void OnEnd(GameObject owner) { }

    public override TimelineEventBase Clone()
    {
        var clone = new GameplayEffectEvent();
        clone.StartFrame = StartFrame;
        clone.EndFrame = EndFrame;
        clone.gameplayEffect = gameplayEffect;
        clone.effectTarget = effectTarget;
        clone.searchParameters = new SearchParameters
        {
            Shape = searchParameters.Shape,
            Radius = searchParameters.Radius,
            Angle = searchParameters.Angle,
            Length = searchParameters.Length,
            Width = searchParameters.Width,
            TargetLayer = searchParameters.TargetLayer,
            MaxTargets = searchParameters.MaxTargets,
            ExcludeSelf = searchParameters.ExcludeSelf
        };
        return clone;
    }

    private void ExecuteAllInRange(GameObject owner, AbilitySystemComponent ownerASC)
    {
        var origin = owner.transform.position;
        var forward = owner.transform.forward;
        var sp = searchParameters;

        Collider[] hits = null;

        switch (sp.Shape)
        {
            case SearchShape.Circle:
                hits = Physics.OverlapSphere(origin, sp.Radius, sp.TargetLayer);
                break;

            case SearchShape.Sector:
                hits = Physics.OverlapSphere(origin, sp.Radius, sp.TargetLayer);
                break;

            case SearchShape.Line:
                var rayHits = Physics.RaycastAll(origin, forward, sp.Length, sp.TargetLayer);
                foreach (var rh in rayHits)
                {
                    var targetASC = rh.collider.GetComponentInParent<AbilitySystemComponent>();
                    if (targetASC != null && targetASC != ownerASC)
                        targetASC.ApplyGameplayEffect(gameplayEffect, ownerASC);
                }
                return;

            case SearchShape.Rectangle:
                var boxCenter = origin + forward * (sp.Length / 2f);
                var boxSize = new Vector3(sp.Width, 2f, sp.Length);
                var rot = Quaternion.LookRotation(forward);
                hits = Physics.OverlapBox(boxCenter, boxSize / 2f, rot, sp.TargetLayer);
                break;
        }

        if (hits == null) return;

        foreach (var col in hits)
        {
            var targetASC = col.GetComponentInParent<AbilitySystemComponent>();
            if (targetASC == null || (sp.ExcludeSelf && targetASC == ownerASC))
                continue;

            // Sector 过滤
            if (sp.Shape == SearchShape.Sector)
            {
                Vector3 dir = (col.transform.position - origin).normalized;
                float angle = Vector3.Angle(forward, dir);
                if (angle > sp.Angle * 0.5f) continue;
            }

            targetASC.ApplyGameplayEffect(gameplayEffect, ownerASC);
        }
    }
}
