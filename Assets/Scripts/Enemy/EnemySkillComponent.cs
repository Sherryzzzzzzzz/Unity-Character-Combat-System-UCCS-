// 文件名: EnemySkillComponent.cs
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Animancer;

/// <summary>
/// 一个纯粹的“技能播放器”，负责播放技能动画和处理时间轴事件。
/// 它的更新逻辑由外部系统（如行为树的Action节点）通过调用 ManualUpdate() 来驱动。
/// </summary>
public class EnemySkillComponent : MonoBehaviour, IClashable, UCCS.ISkillPlayer
{
    // --- 外部依赖 ---
    private AnimancerComponent _animancer;

    // --- 内部状态 ---
    private SkillTimelineAsset _currentSkill;
    public bool IsPlaying { get; private set; } = false;
    /// <summary>当前技能是否包含攻击事件（用于完美闪避检测）</summary>
    public bool HasActiveAttackEvents { get; private set; } = false;
    /// <summary>最后一次攻击结束的时间（用于完美闪避宽限期检测）</summary>
    public float LastAttackEndTime { get; private set; } = -999f;
    /// <summary>近期是否发动过攻击（完美闪避宽限期）</summary>
    public bool HasRecentAttack(float windowSeconds = 0.8f) =>
        Time.time - LastAttackEndTime < windowSeconds;
    private int _currentFrame = 0;
    
    // 【新增】为调试器缓存当前技能资源
    private SkillTimelineAsset _debuggingSkillAsset;

    // --- 事件管理 ---
    private readonly Dictionary<int, List<ITimelineEventRuntime>> _frameStartEvents = new Dictionary<int, List<ITimelineEventRuntime>>();
    private readonly Dictionary<int, List<ITimelineEventRuntime>> _frameEndEvents = new Dictionary<int, List<ITimelineEventRuntime>>();
    private readonly HashSet<ITimelineEventRuntime> _activeEvents = new HashSet<ITimelineEventRuntime>();

    public event Action OnSkillEnd;

    // --- Animancer 层级管理 ---
    private AnimancerLayer _attackLayer;
    private int _attackLayerIndex = 1; // 攻击动画在第1层播放
    
    private TagComponent _tagComponent; // 用于施加拼刀硬直 Tag
    
    [Header("Clash Configuration")]
    [Tooltip("代表“拼刀硬直”的状态 Tag (GameplayTagSO)")]
    public GameplayTagSO clashStunTag; // *** 在 Inspector 中拖入 "State.Clash.Stun" ***
    
    private bool _isClashed = false; // 拼刀状态锁
    
    private List<ClashDetector> _clashDetectors;
    


    private void Awake()
    {
        _animancer = GetComponent<AnimancerComponent>();
        _tagComponent = GetComponent<TagComponent>();

        // 初始化攻击层
        if (_animancer.Layers.Count <= _attackLayerIndex)
            _animancer.Layers.Count = _attackLayerIndex + 1;
        
        _attackLayer = _animancer.Layers[_attackLayerIndex];
        _attackLayer.SetWeight(0f); // 默认权重为0，不影响其他动画
        
        _clashDetectors = new List<ClashDetector>(GetComponentsInChildren<ClashDetector>(true));
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
        
        if (IsPlaying)
        {
            foreach (var evt in _activeEvents)
            {
                try { evt.OnEnd(gameObject); }
                catch (Exception e) { Debug.LogError($"Error cleaning up previous OnEnd event: {e}", this); }
            }
            _activeEvents.Clear();
        }

        // --- 重置状态 ---
        _currentSkill = skill;
        _debuggingSkillAsset = skill; // 【新增】缓存技能资源给调试器使用
        IsPlaying = true;
        _currentFrame = -1;
        _frameStartEvents.Clear();
        _frameEndEvents.Clear();
        
        foreach (var detector in _clashDetectors)
        {
            detector.Activate();
        }

        // --- 从技能资源中注册所有时间轴事件 ---
        HasActiveAttackEvents = false;
        if (skill.tracks != null)
        {
            foreach (var track in skill.tracks)
            {
                if (track?.events == null) continue;
                foreach (var evt in track.events)
                {
                    if (evt is AttackEvent)
                        HasActiveAttackEvents = true;

                    if (evt is ITimelineEventRuntime runtimeEvent)
                    {
                        if (!_frameStartEvents.ContainsKey(evt.StartFrame))
                            _frameStartEvents[evt.StartFrame] = new List<ITimelineEventRuntime>();
                        _frameStartEvents[evt.StartFrame].Add(runtimeEvent);

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
            // ★ 修复“打断后重播卡在被打断位置”：显式归零播放时间
            state.TimeD = 0;
            _attackLayer.SetWeight(1f);
            
            // 【修改】为动画状态注册一个 OnEnd 回调
            state.Events(this).OnEnd = () =>
            {
                if (_attackLayer == null || _attackLayer.CurrentState != state)
                    return;

                SkillDebugManager.ReportSkillStop(this.gameObject);
                StopAndCleanup();
            };
        }
        else
        {
            StopAndCleanup();
        }
    }
    
    /// <summary>
    /// (由外部驱动) 手动更新当前帧的事件。
    /// </summary>
    public void ManualUpdate()
    {
        if (_isClashed) return;
        if (!IsPlaying || _currentSkill == null || _attackLayer.CurrentState == null) return;
        
        var state = _attackLayer.CurrentState;
        if(state.Clip == null) return;

        float totalFrames = state.Clip.length * state.Clip.frameRate;
        int newFrame = Mathf.FloorToInt(state.NormalizedTime * totalFrames);

        // 【修改】如果帧数有变化，则广播并触发事件
        if (newFrame > _currentFrame) 
        {
             _currentFrame = newFrame;
            
            // 【新增】广播调试信息
            int maxFrame = totalFrames > 0 ? (int)totalFrames : 0;
            SkillDebugManager.ReportSkillFrameUpdate(this.gameObject, _debuggingSkillAsset, _currentFrame, maxFrame);

            // 检查并触发 OnStart 事件
            if (_frameStartEvents.TryGetValue(_currentFrame, out var startEvents))
            {
                for (int i = startEvents.Count - 1; i >= 0; i--)
                {
                    var evt = startEvents[i];
                    try
                    {
                        evt.OnStart(gameObject);
                        _activeEvents.Add(evt);
                    }
                    catch (Exception e) { Debug.LogError($"Error executing OnStart event: {e}", this); }
                }
            }

            // 检查并触发 OnEnd 事件
            if (_frameEndEvents.TryGetValue(_currentFrame, out var endEvents))
            {
                for (int i = endEvents.Count - 1; i >= 0; i--)
                {
                    var evt = endEvents[i];
                    try { evt.OnEnd(gameObject); }
                    catch (Exception e) { Debug.LogError($"Error executing OnEnd event: {e}", this); }
                    _activeEvents.Remove(evt);
                }
            }
        }
    }

    /// <summary>
    /// 停止当前技能并清理所有状态。
    /// </summary>
    public void StopAndCleanup()
    {
        // 【新增】在清理开始时，再次广播停止消息，确保状态同步
        if(IsPlaying)
        {
            SkillDebugManager.ReportSkillStop(this.gameObject);
        }

        if (!IsPlaying) return;

        // ★ 记录攻击结束时间（用于完美闪避宽限期检测）
        if (HasActiveAttackEvents)
            LastAttackEndTime = Time.time;

        foreach (var evt in _activeEvents)
        {
            try { evt.OnEnd(gameObject); }
            catch (Exception e) { Debug.LogWarning($"EnemySkillComponent: event OnEnd cleanup threw: {e}", this); }
        }
        _activeEvents.Clear();

        // 重置内部状态
        IsPlaying = false;
        HasActiveAttackEvents = false;
        _currentSkill = null;
        _currentFrame = 0;
        _debuggingSkillAsset = null; // 【新增】清理调试器缓存
        _frameStartEvents.Clear();
        _frameEndEvents.Clear();

        foreach (var detector in _clashDetectors)
        {
            detector.Deactivate();
        }

        // 平滑地隐藏攻击层
        _attackLayer.StartFade(0f, 0.25f);

        // 触发 OnSkillEnd 事件，通知行为树或其他系统技能已结束
        OnSkillEnd?.Invoke();
    }
    
    /// <summary>
    /// ★ P9: 受击时立即让攻击层让位（停止并清零权重），
    /// 避免攻击动画 0.25s 淡出期间与受击层权重混合（“边挥刀边挨打”）。
    /// </summary>
    public void ForceSuppressAttackLayer()
    {
        if (_attackLayer == null) return;
        // ★ 只对层做权重清零（安全）。
        //   绝不能对 CurrentState 调 Stop()：若该状态正处交叉淡化组（Play 的 FadeIn），
        //   Stop 会把它从组里移除 → 组 FadeIn=null 且仍有 FadeOut → Animancer FadeGroup NRE（每帧），
        //   导致整个动画系统崩溃、无法攻击。
        _attackLayer.SetWeight(0f);
    }

    // 【新增】在对象销毁时，广播停止消息
    private void OnDestroy()
    {
        // 确保在编辑器模式下，如果对象被销毁，调试器能收到通知
        #if UNITY_EDITOR
        SkillDebugManager.ReportSkillStop(this.gameObject);
        #endif
    }
    
    #region IClashable Implementation (保持不变)

    public GameObject GetGameObject() => this.gameObject;
    
    public int GetClashLevel()
    {
        if (IsPlaying && _currentSkill != null)
        {
            var attackEvent = _currentSkill.tracks
                .SelectMany(t => t.events)
                .OfType<AttackEvent>()
                .FirstOrDefault();
            
            return attackEvent != null ? (int)attackEvent.attackData?.forceType : 0;
        }
        return 0;
    }
    
    public void FreezeAnimation()
    {
        _isClashed = true;
        foreach (var detector in _clashDetectors)
        {
            detector.Deactivate();
        }
        if (_attackLayer.CurrentState != null)
        {
            _attackLayer.CurrentState.Speed = 0;
            Debug.Log($"'{gameObject.name}' Animation Frozen.");
        }
    }
    
    public void ResumeAndExecuteClash(ClashResult result)
    {
        StartCoroutine(ClashAftermathSequence(result));
    }

    private IEnumerator ClashAftermathSequence(ClashResult result)
    {
        if (_attackLayer.CurrentState != null)
        {
            _attackLayer.CurrentState.Speed = 1;
        }
        if (clashStunTag != null && _tagComponent != null)
        {
            _tagComponent.AddTag(clashStunTag);
        }
        var cc = GetComponent<CharacterController>();
        if (cc != null)
        {
            float timer = 0f;
            float knockbackDuration = 0.3f;
            while (timer < knockbackDuration)
            {
                float speed = result.KnockbackForce * (1f - (timer / knockbackDuration));
                cc.Move(result.KnockbackDirection * speed * Time.deltaTime);
                timer += Time.deltaTime;
                yield return null;
            }
        }
        yield return new WaitForSeconds(result.StunDuration);
        StopAndCleanup();
        if (clashStunTag != null && _tagComponent != null)
        {
            _tagComponent.RemoveTag(clashStunTag);
        }
        _isClashed = false;
    }

    #endregion
}