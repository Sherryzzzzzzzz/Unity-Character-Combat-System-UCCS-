using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// GameplayCue 分发管理器 — 通过标签查找并调用 IGameplayCue 实现
/// 单例 MonoBehaviour，挂载在场景中
/// </summary>
public class GameplayCueManager : MonoBehaviour
{
    private static GameplayCueManager _instance;

    public static GameplayCueManager Instance
    {
        get
        {
            if (_instance == null)
                _instance = FindObjectOfType<GameplayCueManager>();
            return _instance;
        }
    }

    private readonly Dictionary<GameplayTagSO, IGameplayCue> _cueRegistry =
        new Dictionary<GameplayTagSO, IGameplayCue>();

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
    }

    /// <summary>
    /// 注册一个 Cue 实现到指定标签
    /// </summary>
    public void RegisterCue(GameplayTagSO tag, IGameplayCue cue)
    {
        if (tag == null || cue == null) return;
        _cueRegistry[tag] = cue;
    }

    /// <summary>
    /// 取消注册指定标签的 Cue
    /// </summary>
    public void UnregisterCue(GameplayTagSO tag)
    {
        if (tag == null) return;
        _cueRegistry.Remove(tag);
    }

    /// <summary>
    /// 分发 Instant 效果的 Cue（OnExecute）
    /// </summary>
    public void ExecuteCue(GameplayTagSO tag, GameObject target, GameplayEffectSpec spec)
    {
        if (tag != null && _cueRegistry.TryGetValue(tag, out var cue))
        {
            try { cue.OnExecute(target, spec); }
            catch (System.Exception e) { Debug.LogWarning($"ExecuteCue handler threw: {e}"); }
        }
    }

    /// <summary>
    /// 分发 Duration/Infinite 效果施加的 Cue（OnAdd）
    /// </summary>
    public void AddCue(GameplayTagSO tag, GameObject target, GameplayEffectSpec spec)
    {
        if (tag != null && _cueRegistry.TryGetValue(tag, out var cue))
        {
            try { cue.OnAdd(target, spec); }
            catch (System.Exception e) { Debug.LogWarning($"AddCue handler threw: {e}"); }
        }
    }

    /// <summary>
    /// 分发 Duration/Infinite 效果移除的 Cue（OnRemove）
    /// </summary>
    public void RemoveCue(GameplayTagSO tag, GameObject target)
    {
        if (tag != null && _cueRegistry.TryGetValue(tag, out var cue))
        {
            try { cue.OnRemove(target); }
            catch (System.Exception e) { Debug.LogWarning($"RemoveCue handler threw: {e}"); }
        }
    }
}
