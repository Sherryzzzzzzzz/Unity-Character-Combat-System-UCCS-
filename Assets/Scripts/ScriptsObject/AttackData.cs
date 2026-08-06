using UnityEngine;

[CreateAssetMenu(menuName = "GAS-like/Attack Data", fileName = "NewAttackData")]
public class AttackData : ScriptableObject
{
    [Header("Gameplay Effect")]
    public GameplayEffect effect;

    [Header("Stagger")]
    [Tooltip("Optional stagger effect to apply to the attacker when this attack is blocked")]
    public GameplayEffect staggerEffect;

    [Header("Perfect Dodge")]
    [Tooltip("Optional punishment effect to apply to the attacker when this attack hits a target that performed a perfect dodge")]
    public GameplayEffect perfectDodgePunishEffect;

    [Header("Force Settings")]
    public float hitForce = 100f;
    public float hitFrame = 5f;
    public Vector3 hitPosition;
    public AttackForceType forceType = AttackForceType.Light;

    [Header("Launch (P10 击飞)")]
    [Tooltip("★ P10: 击飞高度（米）。>0 时该攻击把受击方打飞（上抛+水平击退，重力回落），可空中追击连段")]
    public float launchHeight = 0f;
    [Tooltip("最低滞空时间（秒）。击飞后保证至少浮空这么久再允许落地——控制连招窗口；0 = 用 HitReactionController 的全局默认值")]
    public float minAirTime = 0f;
    
    [Header("Attack Shape Settings")]
    public AttackShape shape;
    public float radius;
    public float length;
    public float angle;

    [Header("Layer Filter")]
    [Tooltip("攻击检测的目标层，默认 Everything")]
    public LayerMask hitLayerMask = -1;
}