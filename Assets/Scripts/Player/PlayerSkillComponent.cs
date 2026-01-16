// 文件名: PlayerAttackComponent.cs
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using Animancer;


public class PlayerSkillComponent : MonoBehaviour,IClashable
{
    private AnimancerComponent _Animancer;

    [Header("Skill Settings")]
    public AnimationClip _AttackAnimation;
    private SkillTimelineAsset currentSkill;
    [HideInInspector] public SkillTimelineAsset CurrentSkill => currentSkill;
    public bool isPlaying { get; private set; } = false;

    private int currentFrame = 0;
    public int CurrentFrame => currentFrame;
    private int maxFrame = 0;
    
    // 【新增】为调试器缓存当前技能资源
    private SkillTimelineAsset _debuggingSkillAsset;

    private readonly Dictionary<int, List<ITimelineEventRuntime>> frameStartEvents = new();
    private readonly Dictionary<int, List<ITimelineEventRuntime>> frameEndEvents = new();
    
    private readonly List<LoopEvent> activeLoopEvents = new List<LoopEvent>();
    private readonly List<BranchEvent> activeBranchEvents = new List<BranchEvent>();

    // 缓存输入系统
    private InputActionReference cachedInputAction = null;
    private float cachedInputTimer = 0f;
    private const float CachedInputExpire = 0.25f;

    private bool isSwitching = false;

    public event Action OnSkillEnd;

    // === 新增：专用攻击层 ===
    private AnimancerLayer _AttackLayer;
    private int attackLayerIndex = 1; // 攻击层放在 Layer 1，底层 Layer 0 是移动/idle
    
    private TagComponent tagComponent;
    
    [Tooltip("开始一次新攻击（起手式）时的淡入时间")]
    public float attackFadeInDuration = 0.2f;
    [Tooltip("连接一次连招（Combo）时的淡入时间")]
    public float comboFadeInDuration = 0.1f;
    [Tooltip("攻击结束，过渡回 Idle/Move 时的淡出时间")]
    public float attackFadeOutDuration = 0.25f;
    
    // --- 新增：拼刀相关 ---
    [Header("Clash Configuration")]
    [Tooltip("代表“拼刀硬直”的状态 Tag")]
    public GameplayTagSO clashStunTag; // *** 在 Inspector 中拖入 "State.Clash.Stun" ***
    
    private bool _isClashed = false; // 拼刀状态锁
    
    public List<ClashDetector> _clashDetectors { get; private set; }

    private void Awake()
    {
        _Animancer = GetComponent<AnimancerComponent>();
        tagComponent = GetComponent<TagComponent>();

        if (_Animancer.Layers.Count <= attackLayerIndex)
            _Animancer.Layers.Count = attackLayerIndex + 1;

        _AttackLayer = _Animancer.Layers[attackLayerIndex];
        
        _AttackLayer.SetWeight(0f);
        _clashDetectors = new List<ClashDetector>(GetComponentsInChildren<ClashDetector>(true));
    }

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
        
        foreach (var branch in activeBranchEvents.ToArray())
        {
            if (branch.ConditionsMet(gameObject))
            {
                switch (branch.action)
                {
                    case BranchActionType.JumpToFrame:
                        float targetTime = branch.targetFrame / state.Clip.frameRate;
                        state.Time = targetTime;
                        currentFrame = Mathf.FloorToInt(state.Time * state.Clip.frameRate);
                        Debug.Log($"Branching to frame {branch.targetFrame}");
                        break;
                    case BranchActionType.EndSkill:
                        HandleAnimationEnd(); // 使用统一的结束函数
                        return;
                }
                break; 
            }
        }
        if (!isPlaying) return;

        int previousFrame = currentFrame;
        currentFrame = Mathf.FloorToInt(state.Time * state.Clip.frameRate);

        // 如果帧数没有前进，则不触发事件
        if (currentFrame <= previousFrame && state.Time > 0) return;
        
        // --- 【新增】广播调试信息 ---
        if (currentFrame != previousFrame)
        {
            int maxFrame = state.Clip.length > 0 ? Mathf.RoundToInt(state.Clip.length * state.Clip.frameRate) : 0;
            SkillDebugManager.ReportSkillFrameUpdate(this.gameObject, _debuggingSkillAsset, currentFrame, maxFrame);
        }

        if (frameStartEvents.TryGetValue(currentFrame, out var starts))
        {
            // ... (原有的 Combo 等逻辑保持不变)
            var startsSnapshot = starts.ToArray();
            foreach (var evt in startsSnapshot)
            {
                evt.OnStart(gameObject);
                if (evt is LoopEvent loop) activeLoopEvents.Add(loop);
                if (evt is BranchEvent branch) activeBranchEvents.Add(branch);
            }
            foreach (var evt in startsSnapshot)
            {
                if (evt is ComboEvent combo)
                {
                    bool tagMatched = false;
                    if (combo.comboMode == ComboEvent.ComboMode.Normal_Cacheable)
                    {
                        tagMatched = tagComponent.ConsumeTag(combo.RequiredTag);
                    }
                    else
                    {
                        tagMatched = tagComponent.HasTag(combo.RequiredTag);
                    }

                    if (tagMatched)
                    {
                        if (combo.nextSkill != null)
                        {
                            if (combo.comboMode == ComboEvent.ComboMode.Strict_Immediate)
                            {
                                tagComponent.ConsumeTag(combo.RequiredTag);
                            }
                            var model = GetComponent<PlayerModel>();
                            if (model != null && isPlaying)
                            {
                                model.isComboChain = true;
                                model.isAttacking = true;
                            }
                            PlaySkill(combo.nextSkill);
                            return;
                        }
                    }
                }
            }
        }
        if (frameEndEvents.TryGetValue(currentFrame, out var ends))
        {
            var endsSnapshot = ends.ToArray();
            foreach (var evt in endsSnapshot)
            {
                try { evt.OnEnd(gameObject); }
                catch (Exception ex) { Debug.LogError($"[PlayerAttackComponent] End event exception: {ex}"); }
                
                if (evt is LoopEvent loop) activeLoopEvents.Remove(loop);
                if (evt is BranchEvent branch) activeBranchEvents.Remove(branch);
            }
        }

        foreach (var loop in activeLoopEvents)
        {
            if (currentFrame >= loop.EndFrame)
            {
                float loopStartTime = loop.StartFrame / state.Clip.frameRate;
                state.Time = loopStartTime;
                currentFrame = loop.StartFrame; 
                Debug.Log($"Looping back to frame {loop.StartFrame}");
                break; 
            }
        }

        if (state.NormalizedTime >= 1.0f)
        {
            HandleAnimationEnd(); // 使用统一的结束函数
        }
    }

    // ========== 播放技能 ==========
    public void PlaySkill(SkillTimelineAsset skill)
    {
        if (skill == null) return;
        if (isPlaying && currentSkill == skill) return;

        isSwitching = true;
        bool isCombo = isPlaying;

        var model = GetComponent<PlayerModel>();
        if (model != null)
        {
            model.isComboChain = isCombo;
            model.isAttacking = true;
        }
        
        foreach (var detector in _clashDetectors)
        {
            detector.Activate();
        }

        currentSkill = skill;
        _debuggingSkillAsset = skill; // 【新增】缓存技能资源
        isPlaying = true;
        currentFrame = -1; // 设置为-1以确保第0帧事件触发
        frameStartEvents.Clear();
        frameEndEvents.Clear();
        activeLoopEvents.Clear();
        activeBranchEvents.Clear();
        if (skill.tracks != null)
        {
            // ... (注册事件逻辑保持不变)
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
            float fadeDuration = isCombo ? comboFadeInDuration : attackFadeInDuration;
            var state = _AttackLayer.Play(_AttackAnimation, fadeDuration, FadeMode.FromStart);
            _AttackLayer.SetWeight(1f);

            // 【新增】为动画状态注册 OnEnd 回调
            state.Events(this).OnEnd = HandleAnimationEnd;
            
            maxFrame = Mathf.RoundToInt(state.Clip.length * state.Clip.frameRate);
        }
        else
        {
            maxFrame = 0;
            HandleAnimationEnd();
        }

        isSwitching = false;
    }
    
    // 【新增】创建一个统一的动画结束处理函数
    private void HandleAnimationEnd()
    {
        if (!isPlaying) return;
        
        SkillDebugManager.ReportSkillStop(this.gameObject);
        StopAndCleanup();
    }

    // ========== 停止并清理 ==========
    public void StopAndCleanup(bool clearCache = true)
    {
        // 【新增】在外部调用时也广播停止
        if (isPlaying)
        {
            SkillDebugManager.ReportSkillStop(this.gameObject);
        }

        if (!isPlaying) return;

        var endEventsSnapshot = frameEndEvents.Values.SelectMany(v => v).ToList();
        foreach (var evt in endEventsSnapshot)
        {
            try { evt.OnEnd(gameObject); }
            catch (Exception ex) { Debug.LogError($"[PlayerAttackComponent] OnEnd exception: {ex}"); }
        }

        currentSkill = null;
        _debuggingSkillAsset = null; // 【新增】清理调试缓存
        isPlaying = false;
        currentFrame = 0;
        maxFrame = 0;
        activeLoopEvents.Clear();
        activeBranchEvents.Clear();
        
        foreach (var detector in _clashDetectors)
        {
            detector.Deactivate();
        }

        if (_AttackLayer != null)
            _AttackLayer.StartFade(0f, attackFadeOutDuration); // 使用配置的淡出时间

        if (clearCache)
        {
            cachedInputAction = null;
            cachedInputTimer = 0f;
        }

        var model = GetComponent<PlayerModel>();
        if (model != null)
        {
            if (clearCache)
            {
                model.isComboChain = false;
            }
        
            if (PlayerController.Instance.isGround)
            {
                model.ChangePlayerState(PlayerState.ground);
            }
            else
            {
                model.ChangePlayerState(PlayerState.sky);
            }
        }

        OnSkillEnd?.Invoke();
    }
    
    // 【新增】在对象销毁时，广播停止消息
    private void OnDestroy()
    {
        #if UNITY_EDITOR
        SkillDebugManager.ReportSkillStop(this.gameObject);
        #endif
    }

    // ========== 输入缓存 (保持不变) ==========
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

    #region IClashable Implementation (保持不变)

    public GameObject GetGameObject() => this.gameObject;
    
    public int GetClashLevel()
    {
        if (isPlaying && currentSkill != null)
        {
            var attackEvent = currentSkill.tracks
                .SelectMany(t => t.events)
                .OfType<AttackEvent>()
                .FirstOrDefault();
            return (int)attackEvent?.forceType;
        }
        return 0;
    }

    public void FreezeAnimation()
    {
        _isClashed = true;
        if (_AttackLayer.CurrentState != null)
        {
            _AttackLayer.CurrentState.Speed = 0;
            Debug.Log($"'{gameObject.name}' Animation Frozen.");
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

    #endregion
    
    public int CurrentSkillMaxFrame() => maxFrame;
    public bool HasCachedInput() => cachedInputAction != null;
    public string GetCachedInputName() => cachedInputAction?.action?.name ?? "None";
}