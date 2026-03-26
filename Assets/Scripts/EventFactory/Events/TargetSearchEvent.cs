using UnityEngine;

[System.Serializable]
public class TargetSearchEvent : TimelineEventBase, ITimelineEventRuntime
{
    [Header("搜索配置")]
    public SearchParameters searchParameters = new SearchParameters();

    public override TimelineEventType Type => TimelineEventType.TargetSearch;

    public override string GetSummary()
    {
        return $"Search [{StartFrame}-{EndFrame}] {searchParameters.Shape} R:{searchParameters.Radius}";
    }

    public void OnStart(GameObject owner)
    {
        var ownerASC = owner.GetComponent<AbilitySystemComponent>();
        if (ownerASC == null)
        {
            Debug.LogWarning("TargetSearchEvent: Owner 缺少 AbilitySystemComponent");
            return;
        }

        var origin = owner.transform.position;
        var forward = owner.transform.forward;
        var sp = searchParameters;

        var data = new TargetData { Origin = origin, Direction = forward, Range = sp.Radius };
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
                    var asc = rh.collider.GetComponentInParent<AbilitySystemComponent>();
                    if (asc != null && !(sp.ExcludeSelf && asc == ownerASC))
                        data.TargetActors.Add(asc);
                }
                return; // RaycastAll 路径直接返回

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
            var asc = col.GetComponentInParent<AbilitySystemComponent>();
            if (asc == null || (sp.ExcludeSelf && asc == ownerASC))
                continue;

            if (sp.Shape == SearchShape.Sector)
            {
                Vector3 dir = (col.transform.position - origin).normalized;
                if (Vector3.Angle(forward, dir) > sp.Angle * 0.5f) continue;
            }

            data.TargetActors.Add(asc);
        }

        // 按距离排序 + MaxTargets 限制
        if (data.TargetActors.Count > 1)
        {
            data.TargetActors.Sort((a, b) =>
                Vector3.Distance(origin, a.transform.position)
                    .CompareTo(Vector3.Distance(origin, b.transform.position)));
        }

        if (sp.MaxTargets > 0 && data.TargetActors.Count > sp.MaxTargets)
            data.TargetActors.RemoveRange(sp.MaxTargets, data.TargetActors.Count - sp.MaxTargets);

        Debug.Log($"[TargetSearchEvent] Found {data.TargetActors.Count} targets with {sp.Shape}");
    }

    public void OnEnd(GameObject owner) { }

    public override TimelineEventBase Clone()
    {
        var clone = new TargetSearchEvent();
        clone.StartFrame = StartFrame;
        clone.EndFrame = EndFrame;
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
}
