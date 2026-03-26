using UnityEngine;

/// <summary>
/// 播放动画并等待 Task — 触发 Animator 状态，监听完成或中断
/// </summary>
public class PlayMontageAndWaitTask : AbilityTask
{
    private string _stateName;
    private int _triggerHash;
    private bool _useTrigger;
    private Animator _animator;
    private bool _waitingForEnd;
    private int _animatorLayer;

    public PlayMontageAndWaitTask(string stateName, int animatorLayer = 0)
    {
        _stateName = stateName;
        _useTrigger = false;
        _animatorLayer = animatorLayer;
    }

    public PlayMontageAndWaitTask(int triggerHash, int animatorLayer = 0)
    {
        _triggerHash = triggerHash;
        _useTrigger = true;
        _animatorLayer = animatorLayer;
    }

    public override void Activate()
    {
        base.Activate();

        if (OwnerASC == null)
        {
            Complete();
            return;
        }

        _animator = OwnerASC.GetComponent<Animator>();
        if (_animator == null)
        {
            Debug.LogWarning("PlayMontageAndWaitTask: No Animator found on owner");
            Complete();
            return;
        }

        if (_useTrigger)
            _animator.SetTrigger(_triggerHash);
        else
            _animator.Play(_stateName, _animatorLayer);

        _waitingForEnd = true;
    }

    public override void Tick(float deltaTime)
    {
        if (!IsActive || IsFinished || _animator == null) return;

        if (_waitingForEnd)
        {
            var stateInfo = _animator.GetCurrentAnimatorStateInfo(_animatorLayer);

            // 等待过渡完成后再检查
            if (_animator.IsInTransition(_animatorLayer)) return;

            if (_useTrigger || stateInfo.IsName(_stateName))
            {
                if (stateInfo.normalizedTime >= 1f)
                {
                    Complete();
                }
            }
        }
    }

    public override void Cancel()
    {
        _waitingForEnd = false;
        base.Cancel();
    }
}
