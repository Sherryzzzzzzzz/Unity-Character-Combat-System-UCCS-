using UnityEngine;

[CreateAssetMenu(fileName = "New Gameplay Tag", menuName = "GAS-like/Gameplay Tag")]
public class GameplayTagSO : ScriptableObject
{
    [TextArea]
    public string Description;

    [Tooltip("父标签引用，用于层级匹配（null 表示顶级标签）")]
    public GameplayTagSO parentTag;
}