using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 搜索目标 Task — 支持 Circle / Sector / Line 搜索形状
/// </summary>
public class SearchTargetTask : AbilityTask
{
    private SearchParameters _params;
    private TargetData _result;

    public TargetData Result => _result;

    public SearchTargetTask(SearchParameters searchParams)
    {
        _params = searchParams;
    }

    public override void Activate()
    {
        base.Activate();
        _result = ExecuteSearch();
        Complete();
    }

    private TargetData ExecuteSearch()
    {
        var data = new TargetData();
        if (OwnerASC == null) return data;

        var origin = OwnerASC.transform.position;
        var direction = OwnerASC.transform.right; // 默认朝右（2D）
        data.Origin = origin;
        data.Direction = direction;
        data.Range = _params.Radius;

        Collider2D[] hits = null;

        switch (_params.Shape)
        {
            case SearchShape.Circle:
                hits = Physics2D.OverlapCircleAll(origin, _params.Radius, _params.TargetLayer);
                break;

            case SearchShape.Sector:
                hits = Physics2D.OverlapCircleAll(origin, _params.Radius, _params.TargetLayer);
                if (hits != null)
                {
                    var filtered = new List<Collider2D>();
                    float halfAngle = _params.Angle / 2f;
                    foreach (var hit in hits)
                    {
                        Vector2 toTarget = ((Vector2)hit.transform.position - (Vector2)origin).normalized;
                        float angle = Vector2.Angle(direction, toTarget);
                        if (angle <= halfAngle)
                            filtered.Add(hit);
                    }
                    hits = filtered.ToArray();
                }
                break;

            case SearchShape.Line:
                var rayHits = Physics2D.RaycastAll(origin, direction, _params.Length, _params.TargetLayer);
                data.HitResults = new List<RaycastHit2D>(rayHits);
                hits = rayHits.Select(h => h.collider).ToArray();
                break;

            case SearchShape.Rectangle:
                var size = new Vector2(_params.Length, _params.Width);
                var center = (Vector2)origin + (Vector2)direction * (_params.Length / 2f);
                float angle2 = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                hits = Physics2D.OverlapBoxAll(center, size, angle2, _params.TargetLayer);
                break;
        }

        if (hits == null) return data;

        // 过滤和排序
        var targets = new List<(AbilitySystemComponent asc, float dist)>();
        foreach (var hit in hits)
        {
            var asc = hit.GetComponent<AbilitySystemComponent>();
            if (asc == null) asc = hit.GetComponentInParent<AbilitySystemComponent>();
            if (asc == null) continue;

            if (_params.ExcludeSelf && asc == OwnerASC) continue;

            float dist = Vector2.Distance(origin, hit.transform.position);
            targets.Add((asc, dist));
        }

        // 按距离排序
        targets.Sort((a, b) => a.dist.CompareTo(b.dist));

        // 去重
        var seen = new HashSet<AbilitySystemComponent>();
        foreach (var (asc, dist) in targets)
        {
            if (seen.Contains(asc)) continue;
            seen.Add(asc);
            data.TargetActors.Add(asc);

            if (_params.MaxTargets > 0 && data.TargetActors.Count >= _params.MaxTargets)
                break;
        }

        return data;
    }
}
