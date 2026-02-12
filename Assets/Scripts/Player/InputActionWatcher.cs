using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using System.Linq;

[System.Serializable]
public class WatchedAction
{
    [Header("输入设置")]
    public InputActionReference actionReference;
    public float longPressThreshold = 0.3f;
    
    [Header("事件回调")]
    public UnityEvent onShortPress;
    public UnityEvent onLongPressStart;
    public UnityEvent onLongPressEnd;

    [System.NonSerialized] private float pressTimer = 0f;
    private enum PressState { None, HeldDown } // 简化状态：无 或 按下
    [System.NonSerialized] private PressState state = PressState.None;
    [System.NonSerialized] private bool longPressTriggered = false;

    public void UpdateAndProcess()
    {
        if (actionReference == null || actionReference.action == null) return;

        bool wasPressedThisFrame = actionReference.action.WasPressedThisFrame();
        bool wasReleasedThisFrame = actionReference.action.WasReleasedThisFrame();

        // --- 按下瞬间 ---
        if (wasPressedThisFrame)
        {
            state = PressState.HeldDown;
            pressTimer = 0f;
            longPressTriggered = false;
        }

        // --- 持续按住 ---
        if (state == PressState.HeldDown)
        {
            pressTimer += Time.deltaTime;
            if (!longPressTriggered && pressTimer >= longPressThreshold)
            {
                // 计时器超过阈值，并且长按事件还未触发过
                longPressTriggered = true;
                onLongPressStart.Invoke();
            }
        }

        // --- 释放瞬间 ---
        if (wasReleasedThisFrame)
        {
            if (state == PressState.HeldDown) // 确保是从按下状态释放的
            {
                if (longPressTriggered)
                {
                    // 如果长按事件已经触发过，那么这次释放就是长按结束
                    onLongPressEnd.Invoke();
                }
                else
                {
                    // 如果长按事件还未触发，那么这次释放就是短按
                    onShortPress.Invoke();
                }
            }
            // 无论如何，释放后都重置状态
            state = PressState.None;
            pressTimer = 0f;
            longPressTriggered = false;
        }
    }
}

public class InputActionWatcher : MonoBehaviour
{
    [Header("要监视的输入动作列表")]
    public List<WatchedAction> watchedActions = new List<WatchedAction>();

    private void OnEnable()
    {
        foreach (var watchedAction in watchedActions)
        {
            watchedAction?.actionReference?.action?.Enable();
        }
    }

    private void OnDisable()
    {
        foreach (var watchedAction in watchedActions)
        {
            watchedAction?.actionReference?.action?.Disable();
        }
    }

    private void Update()
    {
        foreach (var watchedAction in watchedActions)
        {
            watchedAction.UpdateAndProcess();
        }
    }
    
    public WatchedAction GetWatchedAction(InputActionReference actionRef)
    {
        if (actionRef == null || actionRef.action == null) return null;
        return watchedActions.FirstOrDefault(wa => wa.actionReference == actionRef);
    }
}