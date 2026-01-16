using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

public class InputActionWatcher : MonoBehaviour
{
    [Header("输入设置")]
    [Tooltip("要监视的输入动作 (Input Action)")]
    public InputActionReference actionToWatch;

    [Tooltip("判定为长按所需的最短时间（秒）")]
    public float longPressThreshold = 0.3f;

    [Header("事件回调")]
    [Tooltip("当检测到短按时触发")]
    public UnityEvent onShortPress;

    [Tooltip("当检测到长按开始时触发")]
    public UnityEvent onLongPressStart;

    [Tooltip("当长按结束（按键释放）时触发")]
    public UnityEvent onLongPressEnd;

    // --- 内部状态 ---
    private float pressTimer = 0f;
    private bool isWaitingForLongPress = false;
    private bool isLongPress = false;

    private void OnEnable()
    {
        if (actionToWatch == null || actionToWatch.action == null)
        {
            Debug.LogError("InputActionWatcher: 'Action To Watch' is not set!", this);
            return;
        }
        
        // 订阅输入事件
        actionToWatch.action.started += OnActionStarted;
        actionToWatch.action.canceled += OnActionCanceled;
        actionToWatch.action.Enable();
    }

    private void OnDisable()
    {
        if (actionToWatch == null || actionToWatch.action == null) return;

        // 取消订阅
        actionToWatch.action.started -= OnActionStarted;
        actionToWatch.action.canceled -= OnActionCanceled;
        actionToWatch.action.Disable();
    }

    private void Update()
    {
        // 只有在等待判定时，才更新计时器
        if (isWaitingForLongPress)
        {
            pressTimer += Time.deltaTime;
            if (pressTimer >= longPressThreshold)
            {
                // 时间到了，判定为长按
                isWaitingForLongPress = false;
                isLongPress = true;
                onLongPressStart.Invoke(); // 触发长按开始事件
            }
        }
    }

    private void OnActionStarted(InputAction.CallbackContext context)
    {
        // 按键按下，开始等待
        pressTimer = 0f;
        isWaitingForLongPress = true;
        isLongPress = false;
    }

    private void OnActionCanceled(InputAction.CallbackContext context)
    {
        // 按键释放
        if (isWaitingForLongPress)
        {
            // 如果在等待期间释放，是短按
            isWaitingForLongPress = false;
            onShortPress.Invoke(); // 触发短按事件
        }
        else if (isLongPress)
        {
            // 如果已经是长按状态，则触发长按结束事件
            isLongPress = false;
            onLongPressEnd.Invoke(); // 触发长按结束事件
        }
    }
}