using System;
using UnityEngine;

public class WaitGameplayTagTask : AbilityTask
{
    public GameplayTagSO Tag;
    public enum TriggerType { Added, Removed, Changed }
    public TriggerType Trigger;
    public Action OnTriggered;
    public bool OnlyTriggerOnce = true;
    private bool _triggered;

    public static WaitGameplayTagTask Create(GameplayTagSO tag, TriggerType trigger,
        Action onTriggered, bool onlyOnce = true)
    {
        return new WaitGameplayTagTask
        {
            Tag = tag, Trigger = trigger, OnTriggered = onTriggered,
            OnlyTriggerOnce = onlyOnce, WaitState = EAbilityTaskWaitState.WaitingOnGame
        };
    }

    public override void Activate()
    {
        if (OwnerASC != null) OwnerASC.OnTagCountChanged += HandleTagChanged;
    }

    private void HandleTagChanged(GameplayTagSO changedTag, int count)
    {
        if (_triggered && OnlyTriggerOnce) return;
        if (IsFinished) return;
        if (changedTag != Tag && !Tag.HasChild(changedTag) && !changedTag.HasChild(Tag)) return;
        bool shouldFire = Trigger switch
        {
            TriggerType.Added => count > 0,
            TriggerType.Removed => count <= 0,
            TriggerType.Changed => true,
            _ => false
        };
        if (shouldFire)
        {
            _triggered = true;
            OnTriggered?.Invoke();
            if (OnlyTriggerOnce) EndTask();
        }
    }

    protected override void OnDestroy()
    {
        if (OwnerASC != null) OwnerASC.OnTagCountChanged -= HandleTagChanged;
    }
}
