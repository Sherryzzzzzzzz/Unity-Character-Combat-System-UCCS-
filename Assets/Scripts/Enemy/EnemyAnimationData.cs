using UnityEngine;
using Animancer;

public class EnemyAnimationData : MonoBehaviour
{
    [Header("组件引用")]
    public AnimancerComponent Animancer;
    public EnemyModel Model;

    [Header("运行时状态")]
    public EnemyAnimationState CurrentState; // 当前的动画状态

    // 移动状态所需的数据
    public CartesianMixerState WalkMixer;
    public Vector2 MixerParameter;
    public ClipTransition RunClip;
    public ClipTransition IdleClip;
    [Header("死亡")]
    public ClipTransition DeathClip;
}