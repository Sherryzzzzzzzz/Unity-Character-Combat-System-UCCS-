using System;
using UnityEngine;

public class WaitOverlapTask : AbilityTask
{
    public LayerMask TargetLayers;
    public Collider ColliderRef;
    public Action<Collider> OnOverlap;
    public bool OnlyTriggerOnce = true;
    private bool _triggered;

    public static WaitOverlapTask Create(Collider collider, LayerMask layers, Action<Collider> onOverlap, bool once = true)
    {
        return new WaitOverlapTask
        {
            ColliderRef = collider, TargetLayers = layers, OnOverlap = onOverlap,
            OnlyTriggerOnce = once, WaitState = EAbilityTaskWaitState.WaitingOnGame
        };
    }

    public override void Activate()
    {
        if (ColliderRef == null) { EndTask(); return; }
        var helper = ColliderRef.gameObject.AddComponent<OverlapTaskHelper>();
        helper.Init(this);
    }

    public void NotifyOverlap(Collider other)
    {
        if (_triggered && OnlyTriggerOnce) return;
        if (IsFinished) return;
        if ((TargetLayers.value & (1 << other.gameObject.layer)) == 0) return;
        _triggered = true;
        OnOverlap?.Invoke(other);
        if (OnlyTriggerOnce) EndTask();
    }
}

internal class OverlapTaskHelper : MonoBehaviour
{
    private WaitOverlapTask _task;
    public void Init(WaitOverlapTask task) { _task = task; }
    private void OnTriggerEnter(Collider other) { _task?.NotifyOverlap(other); }
}
