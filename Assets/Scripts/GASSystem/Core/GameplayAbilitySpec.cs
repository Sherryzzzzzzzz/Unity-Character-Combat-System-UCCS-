using System.Collections.Generic;
using UnityEngine;

// ============================================================
// GameplayAbilitySpec.cs — 对应 UE5 FGameplayAbilitySpec
// 能力运行时描述符，存储在 ASC 中
// ============================================================

/// <summary>
/// 能力实例化策略 — 对应 UE5 EGameplayAbilityInstancingPolicy
/// </summary>
public enum InstancingPolicy
{
    /// <summary>每个Actor只创建一个实例，多次激活重用</summary>
    InstancedPerActor,
    /// <summary>每次激活创建新实例</summary>
    InstancedPerExecution,
    /// <summary>不实例化，直接调用CDO函数(Blueprint不支持)</summary>
    NonInstanced
}

/// <summary>
/// 网络执行策略 — 对应 UE5 EGameplayAbilityNetExecutionPolicy
/// </summary>
public enum NetExecutionPolicy
{
    LocalOnly,          // 仅本地
    LocalPredicted,     // 客户端预测 + 服务器验证
    ServerOnly,         // 仅服务器
    ServerInitiated     // 服务器发起
}

/// <summary>
/// 能力激活模式 — 对应 UE5 EGameplayAbilityActivationMode
/// </summary>
public enum AbilityActivationMode
{
    Authority,      // 权威端激活
    Predicting,     // 客户端预测
    Confirmed,      // 服务器已确认
    Rejected        // 服务器拒绝
}

/// <summary>
/// GE授予能力后的移除策略 — 对应 UE5 EGameplayEffectGrantedAbilityRemovePolicy
/// </summary>
public enum GrantedAbilityRemovePolicy
{
    CancelAbilityImmediately,   // 立即取消并移除
    RemoveAbilityOnEnd,         // 等待当前激活结束再移除
    DoNothing                   // 不处理
}

/// <summary>
/// 能力激活信息 — 对应 UE5 FGameplayAbilityActivationInfo
/// </summary>
public class GameplayAbilityActivationInfo
{
    public AbilityActivationMode ActivationMode = AbilityActivationMode.Authority;
    public bool bCanBeEndedByOtherInstance = false;

    public void SetActivationConfirmed() => ActivationMode = AbilityActivationMode.Confirmed;
    public void SetActivationRejected() => ActivationMode = AbilityActivationMode.Rejected;
    public void SetPredicting() => ActivationMode = AbilityActivationMode.Predicting;
}

/// <summary>
/// 能力运行时描述符 — 对应 UE5 FGameplayAbilitySpec
///
/// 这是 UE GAS 的核心数据结构。每个被授予(授予)的 Ability 在 ASC 中都有一个 Spec。
/// Spec 存储了 Ability 的等级、输入绑定、来源对象、活跃实例等元数据。
/// </summary>
public class GameplayAbilitySpec
{
    /// <summary>全局唯一Handle，由ASC分配</summary>
    public int Handle;

    /// <summary>能力的CDO（Class Default Object）引用</summary>
    public GameplayAbility Ability;

    /// <summary>能力等级（影响数值缩放）</summary>
    public int Level = 1;

    /// <summary>输入绑定ID（-1 = 不绑定输入）</summary>
    public int InputID = -1;

    /// <summary>授予此能力的来源对象（可以是GE、Actor等）</summary>
    public object SourceObject;

    /// <summary>当前活跃实例数（激活次数 - 结束次数）</summary>
    public int ActiveCount;

    /// <summary>输入是否当前被按下</summary>
    public bool InputPressed;

    /// <summary>激活完毕后自动移除此Spec</summary>
    public bool RemoveAfterActivation;

    /// <summary>授予时自动激活一次</summary>
    public bool bActivateOnce;

    /// <summary>是否待移除（因scope lock延迟）</summary>
    public bool PendingRemove;

    /// <summary>动态来源标签（通过GE复制到EffectSpec的SourceTags中）</summary>
    public List<GameplayTagSO> DynamicAbilityTags = new List<GameplayTagSO>();

    /// <summary>非网络实例列表</summary>
    public List<GameplayAbility> NonReplicatedInstances = new List<GameplayAbility>();

    /// <summary>网络实例列表（单机模式下等同于上面的列表）</summary>
    public List<GameplayAbility> ReplicatedInstances = new List<GameplayAbility>();

    /// <summary>激活信息（单机模式下始终为Authority）</summary>
    public GameplayAbilityActivationInfo ActivationInfo = new GameplayAbilityActivationInfo();

    /// <summary>
    /// 创建一个空的Spec（用于反序列化等场景）
    /// </summary>
    public GameplayAbilitySpec() { }

    /// <summary>
    /// 从Ability类创建Spec
    /// </summary>
    public GameplayAbilitySpec(GameplayAbility inAbility, int inLevel = 1, int inInputID = -1, object inSourceObject = null)
    {
        Ability = inAbility;
        Level = inLevel;
        InputID = inInputID;
        SourceObject = inSourceObject;
        ActiveCount = 0;
        InputPressed = false;
    }

    /// <summary>
    /// 获取所有活跃的能力实例（用于遍历当前执行的实例）
    /// </summary>
    public List<GameplayAbility> GetActiveInstances()
    {
        var result = new List<GameplayAbility>();
        result.AddRange(ReplicatedInstances);
        result.AddRange(NonReplicatedInstances);
        return result;
    }

    /// <summary>
    /// 获取当前激活信息
    /// </summary>
    public GameplayAbilityActivationInfo GetActivationInfo()
    {
        // 单机模式始终返回Authority
        return ActivationInfo;
    }
}

/// <summary>
/// 能力Spec定义 — 对应 UE5 FGameplayAbilitySpecDef
/// 用于在 GameplayEffect 中定义要授予的能力
/// </summary>
[System.Serializable]
public class GameplayAbilitySpecDef
{
    [Tooltip("要授予的能力")]
    public GameplayAbilitySO Ability;

    [Tooltip("授予的等级")]
    public int Level = 1;

    [Tooltip("输入绑定ID（-1 = 不绑定输入）")]
    public int InputID = -1;

    [Tooltip("GE移除时的策略")]
    public GrantedAbilityRemovePolicy RemovalPolicy = GrantedAbilityRemovePolicy.CancelAbilityImmediately;

    [Tooltip("来源对象")]
    public Object SourceObject;
}
