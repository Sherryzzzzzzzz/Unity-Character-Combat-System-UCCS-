// 文件名: PlayerSkillComponent.cs (已修复“后摇取消”的最终完整版)
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using Animancer;

public class PlayerSkillComponent : MonoBehaviour, IClashable
{
    // --- 字段声明 ---
    #region Fields

    private AnimancerComponent _Animancer;

    [Header("Skill Settings")]
    public AnimationClip _AttackAnimation;
    private SkillTimelineAsset currentSkill;
    [HideInInspector] public SkillTimelineAsset CurrentSkill => currentSkill;
    public bool isPlaying { get; private set; } = false;

    private int currentFrame = 0;
    public int CurrentFrame => currentFrame;
    private int maxFrame = 0;
    
    private SkillTimelineAsset _debuggingSkillAsset;

    private readonly Dictionary<int, List<ITimelineEventRuntime>> frameStartEvents = new();
    private readonly Dictionary<int, List<ITimelineEventRuntime>> frameEndEvents = new();
    private readonly List<CancelEvent> _activeCancelEvents = new List<CancelEvent>();
    
    private readonly List<LoopEvent> activeLoopEvents = new List<LoopEvent>();

    // 追踪已触发 OnStart 但未触发 OnEnd 的事件，用于 StopAndCleanup 时完整清理
    private readonly HashSet<ITimelineEventRuntime> _activeEvents = new HashSet<ITimelineEventRuntime>();

    private InputActionReference cachedInputAction = null;
    private float cachedInputTimer = 0f;
    private const float CachedInputExpire = 0.25f;

    private bool isSwitching = false;

    public event Action OnSkillEnd;

    private AnimancerLayer _AttackLayer;
    private int attackLayerIndex = 1;
    
    private TagComponent tagComponent;
    private AbilitySystemComponent _asc;

    /// <summary>
    /// 通过 Spec API 激活 GAS 能力（新的推荐方式）
    /// 如果找不到 Spec Handle，fallback 到旧 string-key API
    /// </summary>
    public int ActivateAbilityViaSpec(string abilityName)
    {
        if (_asc == null) return -1;

        // 先尝试通过 Spec API 按 Tag/名称激活
        foreach (var spec in _asc.ActivatableAbilities)
        {
            if (spec.Ability != null && spec.Ability.GetType().Name.Contains(abilityName))
                return _asc.TryActivateAbilityByHandle(spec.Handle) ? spec.Handle : -1;
        }

        // Fallback: 旧版 string-key API
        return _asc.ActivateAbility(abilityName);
    }

    /// <summary>
    /// 通过 GameplayTag 触发所有匹配的 Ability
    /// </summary>
    public int ActivateAbilitiesByTag(GameplayTagSO tag)
    {
        return _asc != null ? _asc.TryActivateAbilitiesByTag(tag) : 0;
    }
    
    [Tooltip("开始一次新攻击（起手式）时的淡入时间")]
    public float attackFadeInDuration = 0.2f;
    [Tooltip("连接一次连招（Combo）时的淡入时间")]
    public float comboFadeInDuration = 0.1f;
    [Tooltip("攻击结束，过渡回 Idle/Move 时的淡出时间")]
    public float attackFadeOutDuration = 0.25f;
    
    [Header("Clash Configuration")]
    [Tooltip("代表“拼刀硬直”的状态 Tag")]
    public GameplayTagSO clashStunTag;
    
    private bool _isClashed = false;
    
    public List<ClashDetector> _clashDetectors { get; private set; }

    #endregion

    // --- Unity 生命周期方法 ---
    #region Unity Lifecycle

    private void Awake()
    {
        _Animancer = GetComponent<AnimancerComponent>();
        tagComponent = GetComponent<TagComponent>();
        _asc = GetComponent<AbilitySystemComponent>();

        if (_Animancer.Layers.Count <= attackLayerIndex)
            _Animancer.Layers.Count = attackLayerIndex + 1;

        _AttackLayer = _Animancer.Layers[attackLayerIndex];
        _AttackLayer.SetWeight(0f);
        _clashDetectors = new List<ClashDetector>(GetComponentsInChildren<ClashDetector>(true));
    }

    private void OnDestroy()
    {
        SkillDebugManager.ReportSkillStop(this.gameObject);
    }

    #endregion

    // --- 核心更新逻辑 ---
    #region Update Logic

    private void Update()
    {
        if (_isClashed) return;
        
        if (cachedInputAction != null)
        {
            cachedInputTimer -= Time.deltaTime;
            if (cachedInputTimer <= 0f)
            {
                cachedInputAction = null;
            }
        }

        if (!isPlaying || currentSkill == null || _AttackLayer == null) return;

        var state = _AttackLayer.CurrentState;
        if (state == null || state.Clip == null)
        {
            StopAndCleanup();
            return;
        }
        
        // --- 先更新 currentFrame，再检查循环 ---
        int previousFrame = currentFrame;
        currentFrame = Mathf.FloorToInt(state.Time * state.Clip.frameRate);

        // --- 循环逻辑：使用当前帧的真实值进行回跳判断 ---
        if (activeLoopEvents.Count > 0)
        {
            var activeLoop = activeLoopEvents[0];
            if (activeLoop.BreakConditionsMet(gameObject))
            {
                activeLoopEvents.Remove(activeLoop);
                Debug.Log("Loop break conditions met. Exiting loop.");
            }
            else if (currentFrame >= activeLoop.EndFrame)
            {
                float loopStartTime = (float)activeLoop.StartFrame / state.Clip.frameRate;
                state.Time = loopStartTime;
                currentFrame = activeLoop.StartFrame;
                return;
            }
        }
        
        // 只在帧前进时才触发事件
        if (currentFrame > previousFrame)
        {
            #if UNITY_EDITOR
            int maxFrame = state.Clip.length > 0 ? Mathf.RoundToInt(state.Clip.length * state.Clip.frameRate) : 0;
            SkillDebugManager.ReportSkillFrameUpdate(this.gameObject, _debuggingSkillAsset, currentFrame, maxFrame);
            #endif

            // 【关键修改】调用范围性触发方法
            TriggerEventsForFrameRange(previousFrame + 1, currentFrame);
        }
        else if (currentFrame < previousFrame)
        {
            // 处理时间回溯（例如循环）时，也可能需要清理事件，暂时简化
        }

        // 动画结束检查
        if (isPlaying && state.IsPlaying && state.NormalizedTime >= 1.0f)
        {
            HandleAnimationEnd();
        }
    }
    
    private void TriggerEventsForFrameRange(int fromFrame, int toFrame)
    {
        for (int frame = fromFrame; frame <= toFrame; frame++)
        {
            // 触发 Start 事件
            if (frameStartEvents.TryGetValue(frame, out var starts))
            {
                foreach (var evt in starts.ToArray())
                {
                    evt.OnStart(gameObject);
                    _activeEvents.Add(evt);
                    if (evt is LoopEvent loop)
                    {
                        activeLoopEvents.Add(loop);
                    }
                    if (evt is CancelEvent cancel) _activeCancelEvents.Add(cancel);
                    if (evt is ComboEvent combo)
                    {
                        HandleComboEvent(combo);
                    }
                }
            }

            // 触发 End 事件
            if (frameEndEvents.TryGetValue(frame, out var ends))
            {
                foreach (var evt in ends.ToArray())
                {
                    evt.OnEnd(gameObject);
                    _activeEvents.Remove(evt);
                    if (evt is LoopEvent loop) activeLoopEvents.Remove(loop);
                    if (evt is CancelEvent cancel) _activeCancelEvents.Remove(cancel);
                }
            }
        }
    }
    
    private void HandleComboEvent(ComboEvent combo)
    {
        bool tagMatched = (combo.comboMode == ComboEvent.ComboMode.Normal_Cacheable)
            ? tagComponent.ConsumeTag(combo.RequiredTag)
            : tagComponent.HasTag(combo.RequiredTag);

        if (tagMatched && combo.nextSkill != null)
        {
            if (combo.comboMode == ComboEvent.ComboMode.Strict_Immediate)
                tagComponent.ConsumeTag(combo.RequiredTag);

            var model = GetComponent<PlayerModel>();
            if (model != null) 
            {
                model.isComboChain = true;
            }
            PlaySkill(combo.nextSkill);
        }
    }

    #endregion

    // --- 技能播放与管理 (方法签名保持不变, 内部清理列表) ---
    #region Skill Playback Management

    public void PlaySkill(SkillTimelineAsset skill)
    {
        if (skill == null) return;
        if (isSwitching) return;

        if (isPlaying)
        {
            StopAndCleanup(true, false);
        }

        isSwitching = true;
        
        var model = GetComponent<PlayerModel>();
        
        currentSkill = skill;
        _debuggingSkillAsset = skill;
        isPlaying = true;
        currentFrame = -1;
        
        frameStartEvents.Clear();
        frameEndEvents.Clear();
        activeLoopEvents.Clear();
        _activeCancelEvents.Clear();
        _activeEvents.Clear();

        if (skill.tracks != null)
        {
            foreach (var track in skill.tracks)
            {
                if (track?.events == null) continue;
                foreach (var evt in track.events)
                {
                    if (evt is ITimelineEventRuntime runtime)
                    {
                        if (!frameStartEvents.ContainsKey(evt.StartFrame))
                            frameStartEvents[evt.StartFrame] = new List<ITimelineEventRuntime>();
                        frameStartEvents[evt.StartFrame].Add(runtime);

                        if (!frameEndEvents.ContainsKey(evt.EndFrame))
                            frameEndEvents[evt.EndFrame] = new List<ITimelineEventRuntime>();
                        frameEndEvents[evt.EndFrame].Add(runtime);
                    }
                }
            }
        }

        if (_AttackLayer != null && skill.animationClip != null)
        {
            _AttackAnimation = skill.animationClip;
            float fadeDuration = (model != null && model.isComboChain) ? comboFadeInDuration : attackFadeInDuration;
            var animState = _AttackLayer.Play(_AttackAnimation, fadeDuration, FadeMode.FromStart);
            _AttackLayer.SetWeight(1f);
            
            animState.Events(this).OnEnd = skill.animationClip.isLooping ? (Action)null : HandleAnimationEnd;
            
            maxFrame = Mathf.RoundToInt(animState.Clip.length * animState.Clip.frameRate);
        }
        else
        {
            maxFrame = 0;
            HandleAnimationEnd();
        }

        isSwitching = false;
    }
    
    private void HandleAnimationEnd()
    {
        if (!isPlaying) return;
        StopAndCleanup(true, true);
    }
    
    public void StopAndCleanup(bool clearCache = true, bool triggerDefaultStateChange = true)
    {
        if (!isPlaying) return;

        isPlaying = false;
        OnSkillEnd?.Invoke();
        
        #if UNITY_EDITOR
        SkillDebugManager.ReportSkillStop(this.gameObject);
        #endif
        
        currentSkill = null;
        _debuggingSkillAsset = null;
        currentFrame = 0;
        maxFrame = 0;

        activeLoopEvents.Clear();
        _activeCancelEvents.Clear();

        // 对所有已 OnStart 但未 OnEnd 的事件触发 OnEnd，确保 HitBoxEvent 等清理逻辑执行
        foreach (var evt in _activeEvents)
        {
            try { evt.OnEnd(gameObject); }
            catch (Exception e) { Debug.LogWarning($"PlayerSkillComponent: event OnEnd cleanup threw: {e}"); }
        }
        _activeEvents.Clear();
        
        foreach (var detector in _clashDetectors) detector.Deactivate();

        // 仅在进入 GuardState 时保留攻击层（OnSkillEnd 回调中可能已切入 guard）
        var modelForFade = GetComponent<PlayerModel>();
        bool keepAttackLayer = modelForFade != null && modelForFade._PlayerState == PlayerState.guard;
        if (_AttackLayer != null && !keepAttackLayer)
            _AttackLayer.StartFade(0f, attackFadeOutDuration);

        if (clearCache) { cachedInputAction = null; cachedInputTimer = 0f; }

        if (triggerDefaultStateChange)
        {
            var model = GetComponent<PlayerModel>();
            if (model != null && model._PlayerState != PlayerState.guard)
            {
                model.isComboChain = false;
        
                var playerController = FindObjectOfType<PlayerController>();
                if (playerController != null && playerController.isGround)
                {
                    model.ChangePlayerState(PlayerState.ground);
                }
                else
                {
                    model.ChangePlayerState(PlayerState.sky);
                }
            }
        }
    }

    #endregion
    
    #region Interfaces and Helpers
    
    public bool CanBeCanceledBy(CancelActionType actionType)
    {
        if (!isPlaying) return true;
        foreach (var cancelEvent in _activeCancelEvents)
        {
            if ((cancelEvent.CancelableBy & actionType) != 0)
            {
                return true;
            }
                
        }
        return false;
    }

    public void CacheInputAction(InputActionReference input)
    {
        if (input == null || input.action == null) return;
        cachedInputAction = input;
        cachedInputTimer = CachedInputExpire;
    }

    public bool ConsumeCachedInputIfMatch(InputActionReference input)
    {
        if (cachedInputAction == null || input == null || input.action == null) return false;
        if (cachedInputAction.action == input.action)
        {
            cachedInputAction = null;
            cachedInputTimer = 0f;
            return true;
        }
        return false;
    }

    public GameObject GetGameObject() => this.gameObject;
    
    public int GetClashLevel()
    {
        if (isPlaying && currentSkill != null)
        {
            var attackEvent = currentSkill.tracks
                .SelectMany(t => t.events)
                .OfType<AttackEvent>()
                .FirstOrDefault();
            return (int)attackEvent.attackData?.forceType;
        }
        return 0;
    }

    public void FreezeAnimation()
    {
        _isClashed = true;
        if (_AttackLayer.CurrentState != null)
        {
            _AttackLayer.CurrentState.Speed = 0;
        }
    }

    public void ResumeAndExecuteClash(ClashResult result)
    {
        StartCoroutine(ClashAftermathSequence(result));
    }

    private IEnumerator ClashAftermathSequence(ClashResult result)
    {
        if (_AttackLayer.CurrentState != null)
        {
            _AttackLayer.CurrentState.Speed = 1;
        }
        if (clashStunTag != null)
        {
            tagComponent.AddTag(clashStunTag);
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
        if (clashStunTag != null)
        {
            tagComponent.RemoveTag(clashStunTag);
        }
        _isClashed = false;
    }
    
    public int CurrentSkillMaxFrame() => maxFrame;
    public bool HasCachedInput() => cachedInputAction != null;
    public string GetCachedInputName() => cachedInputAction?.action?.name ?? "None";
    
    #endregion
}