// 文件名: EnemySkillComponent.cs
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Animancer;

/// <summary>
/// 一个纯粹的“技能播放器”，负责播放技能动画和处理时间轴事件。
/// 它的更新逻辑由外部系统（如行为树的Action节点）通过调用 ManualUpdate() 来驱动。
/// </summary>
public class EnemySkillComponent : MonoBehaviour
{
    // --- 外部依赖 ---
    private AnimancerComponent _animancer;

    // --- 内部状态 ---
    private SkillTimelineAsset _currentSkill;
    public bool IsPlaying { get; private set; } = false;
    private int _currentFrame = 0;

    // --- 事件管理 ---
    private readonly Dictionary<int, List<ITimelineEventRuntime>> _frameStartEvents = new Dictionary<int, List<ITimelineEventRuntime>>();
    private readonly Dictionary<int, List<ITimelineEventRuntime>> _frameEndEvents = new Dictionary<int, List<ITimelineEventRuntime>>();

    public event Action OnSkillEnd;

    // --- Animancer 层级管理 ---
    private AnimancerLayer _attackLayer;
    private int _attackLayerIndex = 1; // 攻击动画在第1层播放

    private void Awake()
    {
        _animancer = GetComponent<AnimancerComponent>();

        // 初始化攻击层
        if (_animancer.Layers.Count <= _attackLayerIndex)
            _animancer.Layers.Count = _attackLayerIndex + 1;
        
        _attackLayer = _animancer.Layers[_attackLayerIndex];
        _attackLayer.SetWeight(0f); // 默认权重为0，不影响其他动画
    }

    /// <summary>
    /// 播放一个指定的技能。
    /// </summary>
    /// <param name="skill">要播放的技能资源文件。</param>
    public void PlaySkill(SkillTimelineAsset skill)
    {
        if (skill == null)
        {
            Debug.LogWarning("[EnemySkillComponent] PlaySkill called with a null skill.", this);
            return;
        }
        
        // 如果正在播放上一个技能，先执行它所有尚未触发的 OnEnd 事件来清理状态
        if (IsPlaying)
        {
            foreach (var evt in _frameEndEvents.Values.SelectMany(v => v))
            {
                try { evt.OnEnd(gameObject); }
                catch (Exception e) { Debug.LogError($"Error cleaning up previous OnEnd event: {e}", this); }
            }
        }

        // --- 重置状态 ---
        _currentSkill = skill;
        IsPlaying = true;
        _currentFrame = -1; // 设置为-1，确保动画第一帧（第0帧）的事件能够被触发
        _frameStartEvents.Clear();
        _frameEndEvents.Clear();

        // --- 从技能资源中注册所有时间轴事件 ---
        if (skill.tracks != null)
        {
            foreach (var track in skill.tracks)
            {
                if (track?.events == null) continue;
                foreach (var evt in track.events)
                {
                    if (evt is ITimelineEventRuntime runtimeEvent)
                    {
                        // 注册 OnStart 事件
                        if (!_frameStartEvents.ContainsKey(evt.StartFrame))
                            _frameStartEvents[evt.StartFrame] = new List<ITimelineEventRuntime>();
                        _frameStartEvents[evt.StartFrame].Add(runtimeEvent);

                        // 注册 OnEnd 事件
                        if (!_frameEndEvents.ContainsKey(evt.EndFrame))
                            _frameEndEvents[evt.EndFrame] = new List<ITimelineEventRuntime>();
                        _frameEndEvents[evt.EndFrame].Add(runtimeEvent);
                    }
                }
            }
        }

        // --- 播放动画 ---
        if (_attackLayer != null && skill.animationClip != null)
        {
            var state = _attackLayer.Play(skill.animationClip, 0.1f, FadeMode.FromStart);
            _attackLayer.SetWeight(1f);
            
            // 为动画状态注册一个 OnEnd 回调
            // 当动画播放完毕时，Animancer 会自动调用 StopAndCleanup
            state.Events(this).OnEnd = StopAndCleanup;
        }
        else
        {
            // 如果没有动画片段，立即认为技能已结束
            StopAndCleanup();
        }
    }
    
    /// <summary>
    /// (由外部驱动) 手动更新当前帧的事件。
    /// 这是为了将 Update 逻辑的控制权交给行为树。
    /// </summary>
    public void ManualUpdate()
    {
        if (!IsPlaying || _currentSkill == null || _attackLayer.CurrentState == null) return;
        
        var state = _attackLayer.CurrentState;
        if(state.Clip == null) return;

        // 计算总帧数和当前帧
        float totalFrames = state.Clip.length * state.Clip.frameRate;
        int newFrame = Mathf.FloorToInt(state.NormalizedTime * totalFrames);

        // 如果帧数没有变化，则不执行任何操作，避免重复触发
        if (newFrame == _currentFrame) return;
        
        _currentFrame = newFrame;

        // 检查并触发 OnStart 事件
        if (_frameStartEvents.TryGetValue(_currentFrame, out var startEvents))
        {
            foreach (var evt in startEvents.ToArray()) // 使用 ToArray() 保护循环
            {
                try { evt.OnStart(gameObject); }
                catch (Exception e) { Debug.LogError($"Error executing OnStart event: {e}", this); }
            }
        }
        
        // 检查并触发 OnEnd 事件
        if (_frameEndEvents.TryGetValue(_currentFrame, out var endEvents))
        {
            foreach (var evt in endEvents.ToArray())
            {
                try { evt.OnEnd(gameObject); }
                catch (Exception e) { Debug.LogError($"Error executing OnEnd event: {e}", this); }
            }
        }
    }

    /// <summary>
    /// 停止当前技能并清理所有状态。
    /// </summary>
    public void StopAndCleanup()
    {
        if (!IsPlaying) return;
        
        // 重置内部状态
        IsPlaying = false;
        _currentSkill = null;
        _currentFrame = 0;
        _frameStartEvents.Clear();
        _frameEndEvents.Clear();

        // 平滑地隐藏攻击层
        _attackLayer.StartFade(0f, 0.25f);
        
        // 触发 OnSkillEnd 事件，通知行为树或其他系统技能已结束
        OnSkillEnd?.Invoke();
    }
}