using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// --- 接口不再需要，我们将所有逻辑都放到抽象基类中 ---

/// <summary>
/// 输入动作的抽象基类。这是一个可配置的 ScriptableObject，
/// 负责将原始输入转换为一个 GameplayTag。
/// </summary>
public abstract class CustomInputAction : ScriptableObject
{
    [Header("Output")]
    [Tooltip("当此输入成功时，要授予的 Gameplay Tag")]
    public GameplayTagSO tagToGrant;
    
    public abstract void Initialize(TagComponent tagComponent);
    
    public virtual void ProcessInput() { }
    
    public abstract void Deinitialize();
}

// --- PressInput 的实现 ---
[CreateAssetMenu(fileName = "New Press Input", menuName = "Custom Inputs/Press Input")]
public class PressInput : CustomInputAction
{
    [Header("Configuration")]
    public InputActionReference action;
    
    [NonSerialized] private Action<InputAction.CallbackContext> _handler;
    [NonSerialized] private TagComponent _tagComponent;

    public override void Initialize(TagComponent tagComponent)
    {
        if (action == null || action.action == null) return;
        _tagComponent = tagComponent;

        // 按下时，直接授予 Tag
        _handler = ctx => {
            if (_tagComponent != null && tagToGrant != null)
            {
                _tagComponent.AddTransientTag(tagToGrant);
            }
        };
        
        action.action.Enable();
        action.action.performed += _handler;
    }

    public override void Deinitialize()
    {
        if (action?.action != null) action.action.performed -= _handler;
    }
}


// --- HoldInput 的实现 ---
[CreateAssetMenu(fileName = "New Hold Input", menuName = "Custom Inputs/Hold Input")]
public class HoldInput : CustomInputAction
{
    [Header("Configuration")]
    public InputActionReference action;
    public float holdDuration = 0.5f;
    
    [NonSerialized] private TagComponent _tagComponent;
    [NonSerialized] private bool _isHolding = false;
    [NonSerialized] private float _holdStartTime = 0f;

    public override void Initialize(TagComponent tagComponent)
    {
        if (action == null || action.action == null) return;
        _tagComponent = tagComponent;
        
        action.action.Enable();
        action.action.started += OnActionStarted;
        action.action.canceled += OnActionCanceled;
    }

    private void OnActionStarted(InputAction.CallbackContext ctx)
    {
        _isHolding = true;
        _holdStartTime = Time.time;
    }

    private void OnActionCanceled(InputAction.CallbackContext ctx)
    {
        _isHolding = false;
    }
    
    // Hold 逻辑的核心在 ProcessInput 中
    public override void ProcessInput()
    {
        if (_isHolding && Time.time - _holdStartTime >= holdDuration)
        {
            if (_tagComponent != null && tagToGrant != null)
            {
                _tagComponent.AddTransientTag(tagToGrant);
                // 蓄力成功后，立即停止按住状态，防止在一帧内多次授予 Tag
                _isHolding = false; 
            }
        }
    }

    public override void Deinitialize()
    {
        if (action?.action != null)
        {
            action.action.started -= OnActionStarted;
            action.action.canceled -= OnActionCanceled;
        }
    }
}


// --- SequenceInput 的实现 ---
[CreateAssetMenu(fileName = "New Sequence Input", menuName = "Custom Inputs/Sequence Input")]
public class SequenceInput : CustomInputAction
{
    [Header("Configuration")]
    public List<InputActionReference> sequence;
    public float timeLimit = 0.5f;

    [NonSerialized] private TagComponent _tagComponent;
    [NonSerialized] private int _currentIndex;
    [NonSerialized] private float _lastInputTime;
    [NonSerialized] private Action<InputAction.CallbackContext> _handler;

    public override void Initialize(TagComponent tagComponent)
    {
        if (sequence == null || sequence.Count == 0) return;
        _tagComponent = tagComponent;
        _currentIndex = 0;
        
        _handler = ctx => {
            if (Time.time - _lastInputTime > timeLimit && _currentIndex > 0) _currentIndex = 0;

            if (_currentIndex < sequence.Count && ctx.action == sequence[_currentIndex].action)
            {
                _currentIndex++;
                _lastInputTime = Time.time;
                if (_currentIndex >= sequence.Count)
                {
                    if (_tagComponent != null && tagToGrant != null)
                    {
                        _tagComponent.AddTransientTag(tagToGrant);
                    }
                    _currentIndex = 0;
                }
            }
            else if (ctx.action == sequence[0].action)
            {
                _currentIndex = 1;
                _lastInputTime = Time.time;
            }
            else _currentIndex = 0;
        };

        foreach (var actionRef in sequence)
        {
            if (actionRef?.action != null)
            {
                actionRef.action.Enable();
                actionRef.action.performed += _handler;
            }
        }
    }

    public override void Deinitialize()
    {
        if (sequence == null) return;
        foreach (var actionRef in sequence)
        {
            if (actionRef?.action != null) actionRef.action.performed -= _handler;
        }
    }
}