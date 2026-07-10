using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "New Gameplay Tag", menuName = "GAS-like/Gameplay Tag")]
public class GameplayTagSO : ScriptableObject
{
    [TextArea]
    public string Description;

    [Tooltip("父标签引用，用于层级匹配（null 表示顶级标签）")]
    public GameplayTagSO parentTag;

    /// <summary>
    /// 检查 otherTag 是否是我（或我的任意祖先）的子标签
    /// </summary>
    public bool HasChild(GameplayTagSO otherTag)
    {
        if (otherTag == null) return false;
        var current = otherTag.parentTag;
        while (current != null)
        {
            if (current == this) return true;
            current = current.parentTag;
        }
        return false;
    }

    /// <summary>
    /// 获取此Tag的完整层级路径（例如 "State.Combat.Guarding"）
    /// </summary>
    public string GetFullPath()
    {
        var parts = new List<string>();
        var current = this;
        while (current != null)
        {
            parts.Insert(0, current.name);
            current = current.parentTag;
        }
        return string.Join(".", parts);
    }
}