using System;
using UnityEngine;

/// <summary>
/// 等待输入按下 — 对应 UE5 UAbilityTask_WaitInputPress
/// </summary>
public class WaitInputPressTask : AbilityTask
{
    public Func<bool> InputCheck;  // Lambda: () => Input.GetKeyDown(...)
    public Action OnPressed;
    public bool OnlyTriggerOnce = true;
    private bool _triggered;

    public static WaitInputPressTask Create(Func<bool> inputCheck, Action onPressed, bool once = true)
    {
        return new WaitInputPressTask
        {
            InputCheck = inputCheck,
            OnPressed = onPressed,
            OnlyTriggerOnce = once,
            WaitState = EAbilityTaskWaitState.WaitingOnUser
        };
    }

    public override void Activate() { }
    public override void Tick(float deltaTime)
    {
        if (_triggered && OnlyTriggerOnce) return;
        if (IsFinished) return;
        if (InputCheck != null && InputCheck())
        {
            _triggered = true;
            OnPressed?.Invoke();
            if (OnlyTriggerOnce) EndTask();
        }
    }
}

/// <summary>
/// 等待输入释放 — 对应 UE5 UAbilityTask_WaitInputRelease
/// </summary>
public class WaitInputReleaseTask : AbilityTask
{
    public Func<bool> InputCheck;
    public Action OnReleased;
    public bool OnlyTriggerOnce = true;
    private bool _triggered;
    private bool _wasPressed;

    public static WaitInputReleaseTask Create(Func<bool> inputCheck, Action onReleased, bool once = true)
    {
        return new WaitInputReleaseTask
        {
            InputCheck = inputCheck,
            OnReleased = onReleased,
            OnlyTriggerOnce = once,
            WaitState = EAbilityTaskWaitState.WaitingOnUser
        };
    }

    public override void Activate() { _wasPressed = true; }
    public override void Tick(float deltaTime)
    {
        if (_triggered && OnlyTriggerOnce) return;
        if (IsFinished) return;
        if (InputCheck != null && !InputCheck() && _wasPressed)
        {
            _triggered = true;
            OnReleased?.Invoke();
            if (OnlyTriggerOnce) EndTask();
        }
    }
}

/// <summary>
/// 等待取消输入 — 对应 UE5 UAbilityTask_WaitCancel
/// </summary>
public class WaitCancelTask : AbilityTask
{
    public Func<bool> CancelCheck;
    public Action OnCancel;
    public bool OnlyTriggerOnce = true;
    private bool _triggered;

    public static WaitCancelTask Create(Func<bool> cancelCheck, Action onCancel)
    {
        return new WaitCancelTask { CancelCheck = cancelCheck, OnCancel = onCancel, WaitState = EAbilityTaskWaitState.WaitingOnUser };
    }

    public override void Activate() { }
    public override void Tick(float deltaTime)
    {
        if (_triggered && OnlyTriggerOnce) return;
        if (IsFinished) return;
        if (CancelCheck != null && CancelCheck())
        {
            _triggered = true;
            OnCancel?.Invoke();
            EndTask();
        }
    }
}

/// <summary>
/// 等待确认输入 — 对应 UE5 UAbilityTask_WaitConfirm
/// </summary>
public class WaitConfirmTask : AbilityTask
{
    public Func<bool> ConfirmCheck;
    public Action OnConfirm;
    public bool OnlyTriggerOnce = true;
    private bool _triggered;

    public static WaitConfirmTask Create(Func<bool> confirmCheck, Action onConfirm)
    {
        return new WaitConfirmTask { ConfirmCheck = confirmCheck, OnConfirm = onConfirm, WaitState = EAbilityTaskWaitState.WaitingOnUser };
    }

    public override void Activate() { }
    public override void Tick(float deltaTime)
    {
        if (_triggered && OnlyTriggerOnce) return;
        if (IsFinished) return;
        if (ConfirmCheck != null && ConfirmCheck())
        {
            _triggered = true;
            OnConfirm?.Invoke();
            EndTask();
        }
    }
}

/// <summary>
/// 同时等待确认或取消 — 对应 UE5 UAbilityTask_WaitConfirmCancel
/// </summary>
public class WaitConfirmCancelTask : AbilityTask
{
    public Func<bool> ConfirmCheck;
    public Func<bool> CancelCheck;
    public Action OnConfirmed;
    public Action OnCancelled;
    private bool _done;

    public static WaitConfirmCancelTask Create(Func<bool> confirm, Func<bool> cancel,
        Action onConfirm, Action onCancel)
    {
        return new WaitConfirmCancelTask
        {
            ConfirmCheck = confirm, CancelCheck = cancel,
            OnConfirmed = onConfirm, OnCancelled = onCancel,
            WaitState = EAbilityTaskWaitState.WaitingOnUser
        };
    }

    public override void Activate() { }
    public override void Tick(float deltaTime)
    {
        if (_done || IsFinished) return;
        if (CancelCheck != null && CancelCheck())
        {
            _done = true; OnCancelled?.Invoke(); EndTask();
        }
        else if (ConfirmCheck != null && ConfirmCheck())
        {
            _done = true; OnConfirmed?.Invoke(); EndTask();
        }
    }
}

/// <summary>
/// 等待速度变化 — 对应 UE5 UAbilityTask_WaitVelocityChange
/// 例如：等角色落地速度归零
/// </summary>
public class WaitVelocityChangeTask : AbilityTask
{
    public Func<Vector3> GetVelocity;
    public Func<Vector3, bool> Condition;
    public Action<Vector3> OnVelocityChanged;
    public float MinMagnitudeThreshold = 0.1f;
    public bool OnlyTriggerOnce = true;
    private bool _triggered;
    private Vector3 _lastVelocity;

    public static WaitVelocityChangeTask Create(Func<Vector3> getVelocity, Func<Vector3, bool> condition,
        Action<Vector3> onChanged, bool once = true)
    {
        return new WaitVelocityChangeTask
        {
            GetVelocity = getVelocity, Condition = condition,
            OnVelocityChanged = onChanged, OnlyTriggerOnce = once,
            WaitState = EAbilityTaskWaitState.WaitingOnAvatar
        };
    }

    public override void Activate()
    {
        _lastVelocity = GetVelocity?.Invoke() ?? Vector3.zero;
    }

    public override void Tick(float deltaTime)
    {
        if (_triggered && OnlyTriggerOnce) return;
        if (IsFinished) return;
        var vel = GetVelocity?.Invoke() ?? Vector3.zero;
        if (Condition != null && Condition(vel))
        {
            _triggered = true;
            OnVelocityChanged?.Invoke(vel);
            if (OnlyTriggerOnce) EndTask();
        }
        _lastVelocity = vel;
    }
}
