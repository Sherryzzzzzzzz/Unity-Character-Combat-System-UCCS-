using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 目标数据 — 传递施法者/目标/碰撞等信息
/// </summary>
[System.Serializable]
public class TargetData
{
    public List<AbilitySystemComponent> TargetActors = new List<AbilitySystemComponent>();
    public List<RaycastHit2D> HitResults = new List<RaycastHit2D>();
    public Vector3 Origin;
    public Vector3 Direction;
    public float Range;

    public bool HasTargets => TargetActors != null && TargetActors.Count > 0;
    public AbilitySystemComponent FirstTarget => (TargetActors != null && TargetActors.Count > 0) ? TargetActors[0] : null;
}

/// <summary>
/// 搜索形状
/// </summary>
public enum SearchShape
{
    Circle,
    Sector,
    Line,
    Rectangle
}

/// <summary>
/// 搜索参数
/// </summary>
[System.Serializable]
public class SearchParameters
{
    public SearchShape Shape = SearchShape.Circle;
    public float Radius = 5f;
    public float Angle = 90f;        // Sector 用
    public float Length = 10f;        // Line / Rectangle 用
    public float Width = 2f;         // Rectangle 用
    public LayerMask TargetLayer = ~0;
    public int MaxTargets = 0;       // 0 = 无限制
    public bool ExcludeSelf = true;
}
