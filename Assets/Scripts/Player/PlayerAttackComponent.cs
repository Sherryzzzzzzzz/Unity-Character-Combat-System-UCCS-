using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using Animancer;


public class PlayerAttackComponent : MonoBehaviour,IClashable
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

        // 初始化攻击层
        if (_Animancer.Layers.Count <= attackLayerIndex)
            _Animancer.Layers.Count = attackLayerIndex + 1;

        _AttackLayer = _Animancer.Layers[attackLayerIndex];
        
        _AttackLayer.SetWeight(0f);
        _clashDetectors = new List<ClashDetector>(GetComponentsInChildren<ClashDetector>(true));
    }

    private void Update()
    {
        if (_isClashed)
        {
            return;
        }
        
        // 缓存输入过期检测
        if (cachedInputAction != null)
        {
            cachedInputTimer -= Time.deltaTime;
            if (cachedInputTimer <= 0f)
            {
                //Debug.Log($"[PlayerAttackComponent] Cached input expired: {cachedInputAction.action?.name}");
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
        
        foreach (var branch in activeBranchEvents.ToArray()) // 使用 ToArray 保护循环
        {
            if (branch.ConditionsMet(gameObject))
            {
                // 条件满足！执行动作
                switch (branch.action)
                {
                    case BranchActionType.JumpToFrame:
                        // 跳转到指定帧
                        float targetTime = branch.targetFrame / state.Clip.frameRate;
                        state.Time = targetTime;
                        // 跳转后立即重新计算当前帧，以确保新帧的事件能被触发
                        currentFrame = Mathf.FloorToInt(state.Time * state.Clip.frameRate);
                        Debug.Log($"Branching to frame {branch.targetFrame}");
                        // (可选) 触发一个“分支成功”的音效或特效
                        break;
                    case BranchActionType.EndSkill:
                        StopAndCleanup();
                        return; // 立即退出 Update，因为技能已结束
                }
                // (可选) 如果一个分支成功了，可以立即跳出循环
                break; 
            }
        }
        // 如果因为 EndSkill 而退出了，就不再执行后续代码
        if (!isPlaying) return;

        int previousFrame = currentFrame;
        currentFrame = Mathf.FloorToInt(state.Time * state.Clip.frameRate);

        // 如果帧数没有前进，则不触发事件（防止循环时重复触发同一帧）
        if (currentFrame <= previousFrame && state.Time > 0) return;

        // ---------- Start ----------
        if (frameStartEvents.TryGetValue(currentFrame, out var starts))
        {
            var startsSnapshot = starts.ToArray();
            foreach (var evt in startsSnapshot)
            {
                // OnStart 的调用保持不变
                evt.OnStart(gameObject);
                
                if (evt is LoopEvent loop) activeLoopEvents.Add(loop);
                if (evt is BranchEvent branch) activeBranchEvents.Add(branch);

            }

            // ====================================================================
            // --- 核心升级：同时检查瞬时 Tag 和缓存 Tag ---
            // ====================================================================
            foreach (var evt in startsSnapshot)
            {
                if (evt is ComboEvent combo)
                {
                    bool tagMatched = false;

                    // --- 核心修正：根据模式调用不同的 TagComponent 方法 ---
                    if (combo.comboMode == ComboEvent.ComboMode.Normal_Cacheable)
                    {
                        // Normal 模式使用消耗性的 ConsumeTag，它能同时处理实时和缓存输入
                        tagMatched = tagComponent.ConsumeTag(combo.RequiredTag);
                    }
                    else // Strict_Immediate
                    {
                        // Strict 模式使用非消耗性的 HasTag，它只检查当前帧的瞬时输入
                        tagMatched = tagComponent.HasTag(combo.RequiredTag);
                    }

                    // 后续逻辑完全不变
                    if (tagMatched)
                    {
                        if (combo.nextSkill != null)
                        {
                            // 如果是 Strict 模式匹配成功，它的 Tag 还在，我们也需要消耗掉它
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
        // ---------- End ----------
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
            // 如果当前帧超过了循环的结束帧
            if (currentFrame >= loop.EndFrame)
            {
                // 将动画时间设置回循环的起始帧
                float loopStartTime = loop.StartFrame / state.Clip.frameRate;
                state.Time = loopStartTime;
                // 更新当前帧，防止下一帧的逻辑出错
                currentFrame = loop.StartFrame; 
                Debug.Log($"Looping back to frame {loop.StartFrame}");
                // 通常一帧只处理一个循环，所以可以 break
                break; 
            }
        }

        // --- 动画结束 (逻辑微调) ---
        // 使用 state.NormalizedTime >= 1.0f 更可靠，因为它不受浮点数精度影响
        if (state.NormalizedTime >= 1.0f)
        {
            StopAndCleanup();
        }
    }

    // ========== 播放技能 ==========
    public void PlaySkill(SkillTimelineAsset skill)
    {
        if (skill == null) return;
        if (isSwitching || (isPlaying && currentSkill == skill)) return;

        isSwitching = true;

        bool isCombo = isPlaying;

        var model = GetComponent<PlayerModel>();
        if (model != null)
        {
            model.isComboChain = isCombo;
            model.isAttacking = true; // isAttacking 由 PlayerModel 的 SuperState 控制会更好，但暂时先这样
        }
        
        foreach (var detector in _clashDetectors)
        {
            detector.Activate();
        }

        // --- 清理和注册事件 (逻辑不变) ---
        currentSkill = skill;
        isPlaying = true;
        currentFrame = 0;
        frameStartEvents.Clear();
        frameEndEvents.Clear();
        activeLoopEvents.Clear();
        activeBranchEvents.Clear();
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

        // --- 核心修改：使用交叉淡入来播放动画 ---
        if (_AttackLayer != null && skill.animationClip != null)
        {
            _AttackAnimation = skill.animationClip;

            // 根据是起手还是连招，选择不同的淡入时间
            float fadeDuration = isCombo ? comboFadeInDuration : attackFadeInDuration;
            
            // Play 方法本身就会执行交叉淡入，它会平滑地从当前正在播放的动画过渡到新动画
            var state = _AttackLayer.Play(_AttackAnimation, fadeDuration, FadeMode.FromStart);

            // 确保攻击层的权重是1
            _AttackLayer.SetWeight(1f);
            
            maxFrame = Mathf.RoundToInt(state.Clip.length * state.Clip.frameRate);
        }
        else
        {
            maxFrame = 0;
        }

        isSwitching = false;
    }

    // ========== 停止并清理 ==========
    public void StopAndCleanup(bool clearCache = true)
    {
        if (!isPlaying) return;

        var endEventsSnapshot = frameEndEvents.Values.SelectMany(v => v).ToList();
        foreach (var evt in endEventsSnapshot)
        {
            try { evt.OnEnd(gameObject); }
            catch (Exception ex) { Debug.LogError($"[PlayerAttackComponent] OnEnd exception: {ex}"); }
        }

        currentSkill = null;
        isPlaying = false;
        currentFrame = 0;
        maxFrame = 0;
        activeLoopEvents.Clear();
        activeBranchEvents.Clear();
        
        foreach (var detector in _clashDetectors)
        {
            detector.Deactivate();
        }

        // 渐隐攻击层权重
        if (_AttackLayer != null)
            _AttackLayer.StartFade(0f, 0.25f);

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
                // isAttacking 会在下一帧的 Update 中自动变为 false
            }
        
            // 确保在攻击结束后，状态机可以回到正确的地面或空中状态
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

    // ========== 输入缓存 ==========
    public void CacheInputAction(InputActionReference input)
    {
        if (input == null || input.action == null) return;
        cachedInputAction = input;
        cachedInputTimer = CachedInputExpire;
        //Debug.Log($"[PlayerAttackComponent] Cached input: {input.action.name}");
    }

    public bool ConsumeCachedInputIfMatch(InputActionReference input)
    {
        if (cachedInputAction == null || input == null || input.action == null) return false;
        if (cachedInputAction.action == input.action)
        {
            cachedInputAction = null;
            cachedInputTimer = 0f;
            //Debug.Log($"[PlayerAttackComponent] Consumed cached input for: {input.action.name}");
            return true;
        }
        return false;
    }

    #region IClashable Implementation

    public GameObject GetGameObject()
    {
        return this.gameObject;
    }
    
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
        // 1. 开启状态锁
        _isClashed = true;
        
        // 2. 冻结动画
        if (_AttackLayer.CurrentState != null)
        {
            _AttackLayer.CurrentState.Speed = 0;
            Debug.Log($"'{gameObject.name}' Animation Frozen.");
        }
    }

    /// <summary>
    /// 接到导演的“开始”指令。
    /// </summary>
    public void ResumeAndExecuteClash(ClashResult result)
    {
        // 启动一个专门处理后续效果的协程
        StartCoroutine(ClashAftermathSequence(result));
    }

    private IEnumerator ClashAftermathSequence(ClashResult result)
    {
        // 1. 恢复动画播放（它会从被冻结的那一帧继续）
        if (_AttackLayer.CurrentState != null)
        {
            _AttackLayer.CurrentState.Speed = 1;
        }

        // 2. 施加硬直 Tag
        if (clashStunTag != null)
        {
            tagComponent.AddTag(clashStunTag);
        }

        // 3. 应用击退效果 (持续性)
        var cc = GetComponent<CharacterController>();
        if (cc != null)
        {
            float timer = 0f;
            float knockbackDuration = 0.3f; // 击退持续时间
            while (timer < knockbackDuration)
            {
                float speed = result.KnockbackForce * (1f - (timer / knockbackDuration));
                cc.Move(result.KnockbackDirection * speed * Time.deltaTime);
                timer += Time.deltaTime;
                yield return null;
            }
        }

        // 4. 等待硬直时间结束
        yield return new WaitForSeconds(result.StunDuration);

        // 5. 硬直结束，清理状态
        StopAndCleanup();
        if (clashStunTag != null)
        {
            tagComponent.RemoveTag(clashStunTag);
        }
        _isClashed = false; // 解锁
    }

    #endregion
    
    public int CurrentSkillMaxFrame() => maxFrame;
    public bool HasCachedInput() => cachedInputAction != null;
    public string GetCachedInputName() => cachedInputAction?.action?.name ?? "None";
}
