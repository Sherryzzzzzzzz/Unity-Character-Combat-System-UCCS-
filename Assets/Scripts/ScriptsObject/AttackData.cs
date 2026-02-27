using UnityEngine;

[CreateAssetMenu(menuName = "GAS-like/Attack Data", fileName = "NewAttackData")]
public class AttackData : ScriptableObject
{
    [Header("Gameplay Effect")]
    public GameplayEffect effect;

    [Header("Force Settings")]
    public float hitForce = 100f;
    public float hitFrame = 5f;
    public Vector3 hitPosition;
    public AttackForceType forceType = AttackForceType.Light;
    
    [Header("Attack Shape Settings")]
    public AttackShape shape;
    public float radius;
    public float length;
    public float angle;

    [Header("Layer Filter")]
    [Tooltip("攻击检测的目标层，默认 Everything")]
    public LayerMask hitLayerMask = -1;
}