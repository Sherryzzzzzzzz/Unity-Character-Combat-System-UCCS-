using System.Collections.Generic;
using UnityEngine;

public class TagComponent : MonoBehaviour
{
    private readonly Dictionary<GameplayTagSO, int> _tagRefCounts = new Dictionary<GameplayTagSO, int>();
    private HashSet<GameplayTagSO> transientTags = new HashSet<GameplayTagSO>();
    
    // --- 新增：Buff 管理系统 ---
    private readonly List<Buff> activeBuffs = new List<Buff>();
    // 用字典快速查找，避免重复添加
    private readonly Dictionary<BuffSO, Buff> _buffLookup = new Dictionary<BuffSO, Buff>();
    
    private class CachedTag { public GameplayTagSO Tag; public float Timestamp; }
    private readonly List<CachedTag> cachedTags = new List<CachedTag>();
    private const float CACHE_DURATION = 0.25f;
    public System.Action<GameplayTagSO> OnTagAdded;

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
    /// 获取某个Tag的当前引用计数
    /// </summary>
    public int GetTagCount(GameplayTagSO tag)
    {
        if (tag == null) return 0;
        _tagRefCounts.TryGetValue(tag, out var count);
        // 加上瞬态标签
        if (transientTags.Contains(tag)) count++;
        return count;
    }

    /// <summary>
    /// (非消耗性) 检查是否拥有某个 Tag，主要用于 Strict 模式。
    /// </summary>
    public bool HasTag(GameplayTagSO tag)
    {
        if (tag == null) return false;
        if (transientTags.Contains(tag)) return true;
        if (_tagRefCounts.TryGetValue(tag, out var count) && count > 0) return true;
        return false;
    }

    /// <summary>
    /// (消耗性) 尝试消耗一个 Tag。它会优先消耗瞬时 Tag，然后减少引用计数或移除缓存 Tag。
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

        // 2. 尝试减少引用计数
        if (_tagRefCounts.TryGetValue(tag, out var count) && count > 0)
        {
            _tagRefCounts[tag] = count - 1;
            if (_tagRefCounts[tag] <= 0)
                _tagRefCounts.Remove(tag);
            return true;
        }

        // 3. 如果瞬时 Tag 中没有，再检查并消耗缓存 Tag
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
    /// 授予一个永久性 Tag（引用计数）
    /// </summary>
    public void AddTag(GameplayTagSO tag)
    {
        if (tag == null) return;

        if (_tagRefCounts.TryGetValue(tag, out var count))
        {
            _tagRefCounts[tag] = count + 1;
        }
        else
        {
            _tagRefCounts[tag] = 1;
            OnTagAdded?.Invoke(tag);
        }
    }

    /// <summary>
    /// 移除一个永久性 Tag（减少引用计数）
    /// </summary>
    public void RemoveTag(GameplayTagSO tag)
    {
        if (tag == null) return;
        if (_tagRefCounts.TryGetValue(tag, out var count))
        {
            count--;
            if (count <= 0)
                _tagRefCounts.Remove(tag);
            else
                _tagRefCounts[tag] = count;
        }
    }

    /// <summary>
    /// 层级匹配：检查是否拥有指定标签本身或其任意子标签
    /// 遍历所有活跃标签和瞬态标签，检查每个标签的 parentTag 链是否包含目标标签
    /// </summary>
    public bool HasTagOrChild(GameplayTagSO tag)
    {
        if (tag == null) return false;

        // Check permanent tags via reference-count dictionary
        foreach (var kvp in _tagRefCounts)
        {
            var ownedTag = kvp.Key;
            if (IsTagOrChild(ownedTag, tag)) return true;
        }

        // Check transient tags
        foreach (var ownedTag in transientTags)
        {
            if (IsTagOrChild(ownedTag, tag)) return true;
        }

        // Check cached tags (recently seen transient tags)
        foreach (var cached in cachedTags)
        {
            if (IsTagOrChild(cached.Tag, tag)) return true;
        }

        return false;
    }

    /// <summary>
    /// 检查 ownedTag 是否等于 targetTag 或是 targetTag 的子标签
    /// </summary>
    private bool IsTagOrChild(GameplayTagSO ownedTag, GameplayTagSO targetTag)
    {
        var current = ownedTag;
        while (current != null)
        {
            if (current == targetTag) return true;
            current = current.parentTag;
        }
        return false;
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