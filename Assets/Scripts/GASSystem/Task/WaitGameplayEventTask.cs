using System;
using UnityEngine;

public class WaitGameplayEventTask : AbilityTask
{
    public GameplayTagSO EventTag;
    public bool OnlyTriggerOnce = true;
    public bool OnlyMatchExact = true;
    public Action<GameplayEventData> OnEventReceived;
    private bool _received;

    public static WaitGameplayEventTask Create(GameplayTagSO eventTag, Action<GameplayEventData> callback,
        bool onlyTriggerOnce = true, bool onlyMatchExact = true)
    {
        return new WaitGameplayEventTask
        {
            EventTag = eventTag, OnEventReceived = callback,
            OnlyTriggerOnce = onlyTriggerOnce, OnlyMatchExact = onlyMatchExact,
            WaitState = EAbilityTaskWaitState.WaitingOnGame
        };
    }

    public override void Activate()
    {
        if (EventTag == null) { EndTask(); return; }
    }

    public void HandleEvent(GameplayTagSO eventTag, GameplayEventData payload)
    {
        if (_received && OnlyTriggerOnce) return;
        if (IsFinished) return;
        bool match = OnlyMatchExact
            ? eventTag == EventTag
            : eventTag == EventTag || EventTag.HasChild(eventTag) || eventTag.HasChild(EventTag);
        if (match)
        {
            _received = true;
            OnEventReceived?.Invoke(payload);
            if (OnlyTriggerOnce) EndTask();
        }
    }
}
