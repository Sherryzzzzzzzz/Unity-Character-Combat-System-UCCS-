using System;
using UnityEngine;

/// <summary>
/// AbilityTask 基类 — 技能异步子任务框架。
/// Task 绑定到 Ability Handle，Ability End 时自动取消。
/// 对应 UE5 UAbilityTask
/// </summary>
public abstract class AbilityTask
{
    /// <summary>所属技能</summary>
    public GameplayAbility OwnerAbility { get; private set; }

    /// <summary>所属 ASC</summary>
    public AbilitySystemComponent OwnerASC { get; private set; }

    /// <summary>所属的 GameObject（便捷属性）</summary>
    public GameObject Owner => OwnerASC != null ? OwnerASC.gameObject : null;

    /// <summary>是否处于活跃状态</summary>
    public bool IsActive { get; protected set; }

    /// <summary>是否已完成（完成或取消后为 true）</summary>
    public bool IsFinished { get; protected set; }

    /// <summary>等待状态 — 对应 UE5 EAbilityTaskWaitState</summary>
    public EAbilityTaskWaitState WaitState = EAbilityTaskWaitState.WaitingOnGame;

    /// <summary>所属 Ability Handle</summary>
    public int AbilityHandle;

    /// <summary>角色信息</summary>
    public GameplayAbilityActorInfo ActorInfo;

    /// <summary>Task 完成事件</summary>
    public event Action OnTaskCompleted;

    /// <summary>Task 被取消事件</summary>
    public event Action OnTaskCancelled;

    /// <summary>初始化 Task（由 ASC 调用）</summary>
    public void Initialize(GameplayAbility ability, AbilitySystemComponent asc)
    {
        OwnerAbility = ability;
        OwnerASC = asc;
    }

    /// <summary>启动 Task</summary>
    public virtual void Activate()
    {
        IsActive = true;
        IsFinished = false;
    }

    /// <summary>每帧更新（由 ASC 或 GASHost 驱动）</summary>
    public virtual void Tick(float deltaTime) { }

    /// <summary>取消 Task</summary>
    public virtual void Cancel()
    {
        if (IsFinished) return;
        IsActive = false;
        IsFinished = true;
        OnDestroy();
        OnTaskCancelled?.Invoke();
    }

    /// <summary>清理资源</summary>
    protected virtual void OnDestroy() { }

    /// <summary>标记完成（子类调用）</summary>
    protected void Complete()
    {
        if (IsFinished) return;
        IsActive = false;
        IsFinished = true;
        OnDestroy();
        OnTaskCompleted?.Invoke();
    }

    /// <summary>便捷方法：EndTask() → 调用 Complete()</summary>
    protected void EndTask() { Complete(); }
}

/// <summary>
/// Task 等待状态 — 对应 UE5 EAbilityTaskWaitState
/// </summary>
public enum EAbilityTaskWaitState
{
    WaitingOnGame = 0x01,
    WaitingOnUser = 0x02,
    WaitingOnAvatar = 0x04
}
