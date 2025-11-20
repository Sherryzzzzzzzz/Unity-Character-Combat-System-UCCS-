using UnityEngine;

[CreateAssetMenu(fileName = "New Gameplay Tag", menuName = "GAS-like/Gameplay Tag")]
public class GameplayTagSO : ScriptableObject
{
    [TextArea]
    public string Description;
}