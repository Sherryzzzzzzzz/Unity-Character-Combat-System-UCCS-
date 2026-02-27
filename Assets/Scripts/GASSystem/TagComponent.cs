using System.Collections.Generic;
using UnityEngine;

public class TagComponent : MonoBehaviour
{
    private HashSet<GameplayTagSO> activeTags = new HashSet<GameplayTagSO>();
    private HashSet<GameplayTagSO> transientTags = new HashSet<GameplayTagSO>();
    
    // --- 新增：Buff 管理系统 ---
    private readonly List<Buff> activeBuffs = new List<Buff>();
    // 用字典快速查找，避免重复添加
    private readonly Dictionary<BuffSO, Buff> _buffLookup = new Dictionary<BuffSO, Buff>();
    
    private class CachedTag { public GameplayTagSO Tag; public float Timestamp; }
    private readonly List<CachedTag> cachedTags = new List<CachedTag>();
    private const float CACHE_DURATION = 0.25f;

    void Update()
    {
        for (int i = cachedTags.Count - 1; i >= 0; i--)
        {
            if (Time.time - cachedTags[i].Timestamp > CACHE_DURATION)
            {
                cachedTags.RemoveAt(i);
            }
        }
        
        // 更新 Buff
        for (int i = activeBuffs.Count - 1; i >= 0; i--)
        {
            var buff = activeBuffs[i];
            buff.Tick(Time.deltaTime);
            if (buff.IsFinished)
            {
                RemoveBuff(buff);
            }
        }
    }

    void LateUpdate()
    {
        transientTags.Clear();
    }

    public void AddTransientTag(GameplayTagSO tag)
    {
        if (tag == null) return;
        transientTags.Add(tag);
        cachedTags.RemoveAll(t => t.Tag == tag);
        cachedTags.Add(new CachedTag { Tag = tag, Timestamp = Time.time });
    }

    /// <summary>
    /// (非消耗性) 检查是否拥有某个 Tag，主要用于 Strict 模式。
    /// </summary>
    public bool HasTag(GameplayTagSO tag)
    {
        if (tag == null) return false;
        return activeTags.Contains(tag) || transientTags.Contains(tag);
    }

    /// <summary>
    /// (消耗性) 尝试消耗一个 Tag。它会优先消耗瞬时 Tag，然后消耗缓存 Tag。
    /// 这是 Normal 模式的核心。
    /// </summary>
    /// <returns>如果成功消耗了 Tag，返回 true。</returns>
    public bool ConsumeTag(GameplayTagSO tag)
    {
        if (tag == null) return false;

        // 1. 优先检查并消耗瞬时 Tag
        if (transientTags.Remove(tag))
        {
            // 如果瞬时 Tag 被消耗，也必须从缓存中移除，确保完全清理
            cachedTags.RemoveAll(t => t.Tag == tag);
            return true;
        }

        // 2. 如果瞬时 Tag 中没有，再检查并消耗缓存 Tag
        for (int i = cachedTags.Count - 1; i >= 0; i--)
        {
            if (cachedTags[i].Tag == tag)
            {
                cachedTags.RemoveAt(i);
                return true;
            }
        }

        return false;
    }
    
    /// <summary>
    /// 授予一个永久性 Tag。
    /// </summary>
    public void AddTag(GameplayTagSO tag)
    {
        if (tag == null) return;
        activeTags.Add(tag);
    }

    /// <summary>
    /// 移除一个永久性 Tag。
    /// </summary>
    public void RemoveTag(GameplayTagSO tag)
    {
        if (tag == null) return;
        activeTags.Remove(tag);
    }
    
    public bool TryAddTransientTag(GameplayTagSO tag)
    {
        if (tag == null) return false;

        // 检查这个 Tag 是否已经存在于瞬时列表或缓存列表中
        if (HasTag(tag) || HasCachedTag(tag))
        {
            // 如果已经存在，则不进行任何操作，并返回 false
            return false;
        }

        // 如果不存在，则执行正常的添加逻辑
        AddTransientTag(tag);
        return true;
    }
    private bool HasCachedTag(GameplayTagSO tag)
    {
        if (tag == null) return false;
        foreach (var cachedTag in cachedTags)
        {
            if (cachedTag.Tag == tag)
            {
                return true;
            }
        }
        return false;
    }
    
    /// <summary>
    /// 向此角色施加一个 Buff。
    /// </summary>
    public void ApplyBuff(BuffSO buffData, GameObject instigator)
    {
        if (buffData == null) return;

        if (_buffLookup.TryGetValue(buffData, out var existingBuff))
        {
            // --- 处理已存在的 Buff (刷新或叠加) ---
            switch (buffData.stackingType)
            {
                case BuffStackingType.Refresh:
                    existingBuff.Refresh();
                    break;
                case BuffStackingType.Stack:
                    existingBuff.AddStack();
                    break;
                case BuffStackingType.None:
                default:
                    // 不可叠加，直接忽略
                    break;
            }
        }
        else
        {
            // --- 添加新的 Buff ---
            var newBuff = new Buff(buffData, instigator, this.gameObject);
            activeBuffs.Add(newBuff);
            _buffLookup.Add(buffData, newBuff);
            
            // 授予 Buff 附带的 Tag
            if (buffData.gameplayTag != null)
            {
                AddTag(buffData.gameplayTag);
            }
        }
    }

    /// <summary>
    /// 移除一个 Buff。
    /// </summary>
    public void RemoveBuff(BuffSO buffData)
    {
        if (buffData == null) return;
        if (_buffLookup.TryGetValue(buffData, out var buffToRemove))
        {
            RemoveBuff(buffToRemove);
        }
    }

    // 内部移除方法
    private void RemoveBuff(Buff buff)
    {
        // 移除 Buff 附带的 Tag
        if (buff.Data.gameplayTag != null)
        {
            RemoveTag(buff.Data.gameplayTag);
        }
        
        activeBuffs.Remove(buff);
        _buffLookup.Remove(buff.Data);
    }
}