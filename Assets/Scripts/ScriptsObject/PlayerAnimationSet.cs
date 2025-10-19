using UnityEngine;
using Animancer;

[CreateAssetMenu(fileName = "PlayerAnimationSet", menuName = "Configs/PlayerAnimationSet")]
public class PlayerAnimationSet : ScriptableObject
{
    [Header("基础动作")]
    public ClipTransition idle;
    public ClipTransition walk;
    public ClipTransition jog;
    public ClipTransition run;
    public ClipTransition jump;
    public ClipTransition sky;
    public ClipTransition MtoI;
    public ClipTransition RtoI;
    
}