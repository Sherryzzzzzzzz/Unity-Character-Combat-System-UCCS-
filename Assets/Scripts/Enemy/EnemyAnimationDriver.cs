using UnityEngine;
using Animancer;

public enum EnemyAnimationState{Idle,Move,Death}

[RequireComponent(typeof(EnemyAnimationData), typeof(EnemyModel))]
public class EnemyAnimationDriver : MonoBehaviour
{
    private EnemyAnimationData animData;

    private void Awake()
    {
        animData = GetComponent<EnemyAnimationData>();
        InitializeAnimationData();
    }

    private void InitializeAnimationData()
    {
        animData.Animancer = GetComponent<AnimancerComponent>();
        animData.Model = GetComponent<EnemyModel>();
        if (animData.Model == null) { Debug.LogError("EnemyAnimationDriver: EnemyModel not found!", this); return; }
        var animSet = animData.Model.AnimationSet;

        if (animSet == null) { Debug.LogError("AnimationSet not assigned!"); return; }

        animData.IdleClip = animSet.GetClip("Idle");
        animData.RunClip = animSet.GetClip("Run");
        animData.DeathClip = animSet.GetClip("Death");

        animData.WalkMixer = new CartesianMixerState();
        animData.WalkMixer.Add(animSet.GetClip("Walk_F"),  new Vector2(0, 1));
        animData.WalkMixer.Add(animSet.GetClip("Walk_B"), new Vector2(0, -1));
        animData.WalkMixer.Add(animSet.GetClip("Walk_L"),     new Vector2(1, 0));
        animData.WalkMixer.Add(animSet.GetClip("Walk_R"),    new Vector2(-1, 0));
    }

    private void Update()
    {
        switch (animData.CurrentState)
        {
            case EnemyAnimationState.Idle:
                EnemyAnimationLogic.UpdateIdleState(animData);
                break;
            case EnemyAnimationState.Move:
                EnemyAnimationLogic.UpdateMoveState(animData);
                break;
            case EnemyAnimationState.Death:
                EnemyAnimationLogic.UpdateDeathState(animData);
                break;
        }
    }

    public void ChangeState(EnemyAnimationState newState)
    {
        if (animData.CurrentState == newState) return;
        animData.CurrentState = newState;
    }
}
