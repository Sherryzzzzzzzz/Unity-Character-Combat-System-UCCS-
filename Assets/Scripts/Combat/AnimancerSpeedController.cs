using System.Collections.Generic;
using UnityEngine;
using Animancer;

/// <summary>
/// 统一动画速度控制器 — 替代多个组件各自修改 state.Speed。
/// 使用优先级栈：高优先级覆盖低优先级，所有请求取消后恢复 1.0。
///
/// 挂在 AnimancerComponent 所在 GameObject 上。
/// 通过 GetComponent<AnimancerSpeedController>() 获取，不是全局单例。
/// </summary>
[RequireComponent(typeof(AnimancerComponent))]
public class AnimancerSpeedController : MonoBehaviour
{
    private AnimancerComponent _animancer;

    /// <summary>速度请求栈（按优先级排序，最高优先级的生效）</summary>
    private readonly List<SpeedRequest> _requests = new List<SpeedRequest>();
    private float _currentSpeed = 1f;

    private struct SpeedRequest
    {
        public int Priority;
        public float Speed;
        public object Owner;
        public float ExpireTime; // ★ 请求过期时间（防协程被 Stop 后漏释放导致永久卡速）
    }

    private void Awake()
    {
        _animancer = GetComponent<AnimancerComponent>();
    }

    /// <summary>
    /// ★ 每帧兜底：清理过期请求并重算速度。
    /// 即使之后没有任何 Request/Release 调用，漏释放的慢速请求也会自动恢复。
    /// </summary>
    private void Update()
    {
        bool expired = false;
        float now = Time.unscaledTime;
        for (int i = _requests.Count - 1; i >= 0; i--)
        {
            if (now > _requests[i].ExpireTime)
            {
                _requests.RemoveAt(i);
                expired = true;
            }
        }
        if (expired)
            ApplyTopSpeed();
    }

    /// <summary>
    /// 请求动画速度。同一 owner 重复调用会覆盖旧请求。
    /// priority 越大越优先。
    /// maxDuration：请求最长有效时间（秒），超时自动释放——防止协程被 StopCoroutine 打断后
    /// 漏调 ReleaseSpeed 导致动画永久卡在慢速（表现为“贴地飞行/滑行”）。
    /// </summary>
    public void RequestSpeed(object owner, float speed, int priority = 0, float maxDuration = 1.5f)
    {
        if (_animancer == null) return;

        // 移除同一 owner 的旧请求
        RemoveByOwner(owner);

        // 插入并按优先级排序
        _requests.Add(new SpeedRequest { Owner = owner, Speed = speed, Priority = priority, ExpireTime = Time.unscaledTime + maxDuration });
        _requests.Sort((a, b) => b.Priority.CompareTo(a.Priority));

        ApplyTopSpeed();
    }

    /// <summary>取消某个 owner 的速度请求，恢复正常速度</summary>
    public void ReleaseSpeed(object owner)
    {
        RemoveByOwner(owner);
        ApplyTopSpeed();
    }

    /// <summary>owner 是否还有活跃请求</summary>
    public bool HasActiveRequest(object owner)
    {
        foreach (var r in _requests)
            if (r.Owner == owner) return true;
        return false;
    }

    private void RemoveByOwner(object owner)
    {
        for (int i = _requests.Count - 1; i >= 0; i--)
        {
            if (_requests[i].Owner == owner)
                _requests.RemoveAt(i);
        }
    }

    private void ApplyTopSpeed()
    {
        // ★ 清理过期请求（防漏释放的兜底安全网）
        float now = Time.unscaledTime;
        for (int i = _requests.Count - 1; i >= 0; i--)
        {
            if (now > _requests[i].ExpireTime)
                _requests.RemoveAt(i);
        }

        float targetSpeed = 1f;
        if (_requests.Count > 0)
            targetSpeed = _requests[0].Speed;

        if (Mathf.Abs(_currentSpeed - targetSpeed) > 0.001f)
        {
            _currentSpeed = targetSpeed;
            SetAllLayerSpeeds(targetSpeed);
        }
    }

    /// <summary>直接设置所有 Animancer layer 的速度（不经过优先级系统）</summary>
    public void SetAllLayerSpeeds(float speed)
    {
        if (_animancer == null) return;
        for (int i = 0; i < _animancer.Layers.Count; i++)
        {
            var state = _animancer.Layers[i].CurrentState;
            // ★ 修复：必须无条件设置速度。旧代码的 `state.Speed > 0.001f` 守卫会导致恢复失败——
            //   卡肉冻结把状态速度设为 0 后，恢复 1.0 时因 0 > 0.001 为假而跳过，
            //   状态永远卡在 0 速度（移动动画停一帧、攻击动画播不出来）。
            //   对已停止的状态设置 Speed 是无害的（不会启动它，仅设时间缩放）。
            if (state != null)
                state.Speed = speed;
        }
    }

    /// <summary>获取当前生效的速度</summary>
    public float CurrentSpeed => _currentSpeed;
}
