// 文件名: ExpandableAnimationSet.cs
// 修复了 NullReferenceException 的最终版本
using UnityEngine;
using System.Collections.Generic;
using Animancer;

// 确保这个类定义存在且可序列化
[System.Serializable]
public class AnimationEntry
{
    public string animationName;
    public ClipTransition animationClip;
}

[CreateAssetMenu(fileName = "ExpandableAnimationSet", menuName = "Configs/ExpandableAnimationSet")]
public class ExpandableAnimationSet : ScriptableObject
{
    // 即使在Inspector里是空的，这个字段的初始值也可能是null
    public List<AnimationEntry> animations;
    
    private Dictionary<string, ClipTransition> _animationDictionary;

    private void OnEnable()
    {
        // OnEnable 是一个比较好的初始化时机，但我们也需要在GetClip里做检查
        if (_animationDictionary == null)
        {
            Initialize();
        }
    }

    private void Initialize()
    {
        // *** 核心修复：在这里添加对 animations 列表的 null 检查 ***
        if (animations == null)
        {
            // 如果列表是 null，就创建一个新的空列表，防止后续代码崩溃
            Debug.LogWarning($"Animation list in '{this.name}' was null. Initializing as empty.", this);
            animations = new List<AnimationEntry>();
        }

        // 创建字典
        _animationDictionary = new Dictionary<string, ClipTransition>(animations.Count);
        
        // 现在可以安全地遍历了
        foreach (var entry in animations)
        {
            if (string.IsNullOrEmpty(entry.animationName)) continue;
            if (entry.animationClip == null) continue; // (可选) 增加对Clip的检查

            if (!_animationDictionary.ContainsKey(entry.animationName))
            {
                _animationDictionary.Add(entry.animationName, entry.animationClip);
            }
            else
            {
                Debug.LogWarning($"Duplicate animation name '{entry.animationName}' in '{this.name}'.", this);
            }
        }
    }

    public ClipTransition GetClip(string name)
    {
        // 使用字典实例是否为null作为初始化检查，这比bool标志位更可靠
        if (_animationDictionary == null)
        {
            Initialize();
        }

        if (_animationDictionary.TryGetValue(name, out ClipTransition clip))
        {
            return clip;
        }

        Debug.LogWarning($"Animation clip with name '{name}' not found in '{this.name}'.", this);
        return null;
    }
}