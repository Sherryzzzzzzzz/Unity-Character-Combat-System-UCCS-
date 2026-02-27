using UnityEngine;

/// <summary>
/// GameplayCue 接口 — 效果的视觉/音效反馈解耦
/// 实现类应为 MonoBehaviour，挂载在场景中
/// </summary>
public interface IGameplayCue
{
    /// <summary>
    /// Instant 效果触发时调用（一次性效果，如受击特效）
    /// </summary>
    void OnExecute(GameObject target, GameplayEffectSpec spec);

    /// <summary>
    /// Duration/Infinite 效果施加时调用（持续效果开始，如燃烧特效）
    /// </summary>
    void OnAdd(GameObject target, GameplayEffectSpec spec);

    /// <summary>
    /// Duration/Infinite 效果移除时调用（持续效果结束，清理特效）
    /// </summary>
    void OnRemove(GameObject target);
}
