using UnityEngine;

namespace UCCS
{
    /// <summary>技能播放器接口 — 解耦行为树和具体技能实现</summary>
    public interface ISkillPlayer
    {
        bool IsPlaying { get; }
        void PlaySkill(SkillTimelineAsset skill);
        void ManualUpdate();
        void StopAndCleanup();
        event System.Action OnSkillEnd;
    }

    /// <summary>移动控制器接口 — 解耦行为树和具体移动实现</summary>
    public interface IMovementController
    {
        Vector3? MoveTarget { get; set; }
        float MoveStopDistance { get; set; }
        float Speed { get; }
        bool IsMoving { get; }
        Transform transform { get; }
        void MoveTowards(Vector3 target, float stopDistance);
        void StopMoving();
    }

    /// <summary>受击处理接口 — 解耦攻击系统和具体受击实现</summary>
    public interface IHitHandler
    {
        void ProcessHit(AttackEvent hit, GameObject attacker, AbilitySystemComponent attackerASC = null);
        bool IsInvincible { get; }
    }

    /// <summary>防御状态提供者 — 解耦 HurtBoxManager 和 PlayerModel</summary>
    public interface IDefenseStateProvider
    {
        bool IsDefending { get; }
    }
}
