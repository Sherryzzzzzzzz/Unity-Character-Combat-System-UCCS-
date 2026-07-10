using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 等待目标数据 — 对应 UE5 UAbilityTask_WaitTargetData
/// 驱动 TargetActor 瞄准 → 确认/取消 → 返回目标数据 的完整流程
/// </summary>
public class WaitTargetDataTask : AbilityTask
{
    public Action<TargetData> OnTargetDataReady;
    public Action OnTargetCancelled;
    public bool IsOneShot = true;

    private bool _dataReceived;

    public static WaitTargetDataTask Create(Action<TargetData> onReady, Action onCancelled = null)
    {
        return new WaitTargetDataTask
        {
            OnTargetDataReady = onReady,
            OnTargetCancelled = onCancelled,
            WaitState = EAbilityTaskWaitState.WaitingOnUser
        };
    }

    public void NotifyTargetDataReady(TargetData targetData)
    {
        if (IsFinished) return;
        _dataReceived = true;
        OnTargetDataReady?.Invoke(targetData);
        if (IsOneShot) EndTask();
    }

    public void NotifyTargetCancelled()
    {
        if (IsFinished) return;
        OnTargetCancelled?.Invoke();
        EndTask();
    }
}

/// <summary>
/// 重复执行子任务 — 对应 UE5 UAbilityTask_Repeat
/// </summary>
public class AbilityTask_Repeat : AbilityTask
{
    public Action OnEachIteration;
    public int RepeatCount = -1; // -1 = 无限循环直到外部取消
    public float IterationInterval = 0f;
    private int _iterationsDone;
    private float _timer;

    public static AbilityTask_Repeat Create(Action onEach, int count = -1, float interval = 0f)
    {
        return new AbilityTask_Repeat
        {
            OnEachIteration = onEach,
            RepeatCount = count,
            IterationInterval = interval,
            WaitState = EAbilityTaskWaitState.WaitingOnGame
        };
    }

    public override void Tick(float deltaTime)
    {
        if (IsFinished) return;
        _timer += deltaTime;
        if (_timer >= IterationInterval)
        {
            _timer -= IterationInterval;
            OnEachIteration?.Invoke();
            _iterationsDone++;
            if (RepeatCount > 0 && _iterationsDone >= RepeatCount)
                EndTask();
        }
    }
}
