using UnityEngine;
using Animancer;

// ============================================================
// GameplayAbilityActorInfo.cs — 对应 UE5 FGameplayAbilityActorInfo
// 分离 Owner（逻辑持有者）和 Avatar（物理表现体）
// ============================================================

/// <summary>
/// 角色信息 — 对应 UE5 FGameplayAbilityActorInfo
///
/// UE GAS 的关键设计模式：Owner ≠ Avatar
/// - OwnerActor: 拥有ASC和Ability的逻辑实体（通常是PlayerController或AIController）
/// - AvatarActor: 物理表现体（通常是Character/Pawn）
///
/// 在简单的单机项目中，两者通常是同一个GameObject。
/// 但这个分离为将来扩展（如AI控制的角色/载具）留出了空间。
/// </summary>
public class GameplayAbilityActorInfo
{
    /// <summary>拥有此ASC的Actor（逻辑持有者，如PlayerController）</summary>
    public GameObject OwnerActor;

    /// <summary>物理表现体（通常就是Owner本身，或Avatar/Character）</summary>
    public GameObject AvatarActor;

    /// <summary>Owner上的AbilitySystemComponent</summary>
    public AbilitySystemComponent OwnerASC;

    /// <summary>Avatar上的AbilitySystemComponent（通常和OwnerASC相同）</summary>
    public AbilitySystemComponent AvatarASC;

    /// <summary>Avatar上的Animancer（动画播放）</summary>
    public AnimancerComponent Animancer;

    /// <summary>物理组件</summary>
    public CharacterController CharacterMovement;

    /// <summary>Owner的Transform</summary>
    public Transform OwnerTransform => OwnerActor != null ? OwnerActor.transform : null;

    /// <summary>Avatar的Transform</summary>
    public Transform AvatarTransform => AvatarActor != null ? AvatarActor.transform : null;

    /// <summary>骨骼根节点Transform</summary>
    public Transform SkeletalMeshRoot;

    /// <summary>
    /// 创建ActorInfo（Owner == Avatar 的简单模式）
    /// </summary>
    public static GameplayAbilityActorInfo FromSingleActor(GameObject ownerActor)
    {
        if (ownerActor == null) return null;

        var info = new GameplayAbilityActorInfo
        {
            OwnerActor = ownerActor,
            AvatarActor = ownerActor,
            OwnerASC = ownerActor.GetComponent<AbilitySystemComponent>(),
            Animancer = ownerActor.GetComponent<AnimancerComponent>(),
            CharacterMovement = ownerActor.GetComponent<CharacterController>(),
            SkeletalMeshRoot = ownerActor.transform
        };

        info.AvatarASC = info.OwnerASC;

        // 尝试找骨骼根节点
        var animancer = info.Animancer;
        if (animancer != null && animancer.Animator != null)
            info.SkeletalMeshRoot = animancer.Animator.transform;

        return info;
    }

    /// <summary>
    /// 创建ActorInfo（Owner ≠ Avatar 模式，如AI控制角色）
    /// </summary>
    public static GameplayAbilityActorInfo FromOwnerAndAvatar(GameObject ownerActor, GameObject avatarActor)
    {
        var info = new GameplayAbilityActorInfo
        {
            OwnerActor = ownerActor,
            AvatarActor = avatarActor,
            OwnerASC = ownerActor?.GetComponent<AbilitySystemComponent>(),
            AvatarASC = avatarActor?.GetComponent<AbilitySystemComponent>(),
            Animancer = avatarActor?.GetComponent<AnimancerComponent>(),
            CharacterMovement = avatarActor?.GetComponent<CharacterController>(),
            SkeletalMeshRoot = avatarActor?.transform
        };

        if (info.Animancer != null && info.Animancer.Animator != null)
            info.SkeletalMeshRoot = info.Animancer.Animator.transform;

        return info;
    }

    /// <summary>
    /// 获取实际用于Attribute查询的ASC（优先Avatar，fallback到Owner）
    /// </summary>
    public AbilitySystemComponent GetSuitableASC()
    {
        return AvatarASC != null ? AvatarASC : OwnerASC;
    }

    /// <summary>
    /// 检查是否本地控制
    /// </summary>
    public bool IsLocallyControlled()
    {
        if (OwnerActor == null) return false;
        return OwnerActor.CompareTag("Player");
    }
}
