using System;
using UnityEngine;

public class WaitAttributeChangeTask : AbilityTask
{
    public GameplayAttribute Attribute;
    public Func<float, bool> Condition;
    public Action<float, float> OnAttributeChanged;
    public bool TriggerOnce = true;
    private bool _triggered;

    public static WaitAttributeChangeTask Create(GameplayAttribute attr, Func<float, bool> condition,
        Action<float, float> onChanged, bool triggerOnce = true)
    {
        return new WaitAttributeChangeTask
        {
            Attribute = attr, Condition = condition, OnAttributeChanged = onChanged,
            TriggerOnce = triggerOnce, WaitState = EAbilityTaskWaitState.WaitingOnGame
        };
    }

    public override void Activate()
    {
        var attrs = OwnerASC?.Attributes;
        if (attrs == null) { EndTask(); return; }
        attrs.OnAttributeChanged += HandleAttributeChanged;
    }

    private void HandleAttributeChanged(GameplayAttribute attr, float oldVal, float newVal)
    {
        if (_triggered && TriggerOnce) return;
        if (IsFinished) return;
        if (attr != Attribute) return;
        OnAttributeChanged?.Invoke(newVal, oldVal);
        if (Condition != null && Condition(newVal))
        {
            _triggered = true;
            if (TriggerOnce) EndTask();
        }
    }

    protected override void OnDestroy()
    {
        if (OwnerASC?.Attributes != null)
            OwnerASC.Attributes.OnAttributeChanged -= HandleAttributeChanged;
    }
}
