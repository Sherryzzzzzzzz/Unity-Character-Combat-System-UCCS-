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

    // ★ P7: 当前处于有效连招窗口的 ComboEvent。StartFrame 触发注册，窗口内每帧轮询输入。
    private readonly List<ComboEvent> _activeComboWindows = new List<ComboEvent>();

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

    [Header("追击跳 (P13)")]
    [Tooltip("追击跳水平冲锋速度（m/s）。数值越大追得越快")]
    public float chaseJumpSpeed = 7f;
    [Tooltip("追击跳水平冲锋最长时间（秒）")]
    public float chaseJumpDuration = 0.4f;

    private Coroutine _chaseCoroutine;
    
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

        // ★ P7: 连招窗口轮询 —— 窗口 [StartFrame, EndFrame] 内任意帧按下都有效
        PollComboWindows();

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
                        RegisterComboWindow(combo); // ★ P7: 注册连招窗口，不再只在起始帧判一次
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
    
    /// <summary>
    /// ★ P7: 注册连招窗口。ComboEvent 在 StartFrame 帧被触发时注册，
    /// 窗口 [StartFrame, EndFrame] 内每一帧由 PollComboWindows 轮询输入。
    /// 旧逻辑只在 StartFrame 那一帧判定一次（约 16ms 窗口），几乎不可能按准。
    /// </summary>
    private void RegisterComboWindow(ComboEvent combo)
    {
        if (combo == null || combo.nextSkill == null) return;
        if (!_activeComboWindows.Contains(combo))
            _activeComboWindows.Add(combo);
    }

    /// <summary>每帧轮询活跃连招窗口：窗口内任意帧输入命中即触发下一招（后注册的优先，符合设计顺序）</summary>
    private void PollComboWindows()
    {
        if (_activeComboWindows.Count == 0) return;

        for (int i = _activeComboWindows.Count - 1; i >= 0; i--)
        {
            var combo = _activeComboWindows[i];

            // 窗口结束：移除（EndFrame 为动画帧号）
            if (currentFrame > combo.EndFrame)
            {
                _activeComboWindows.RemoveAt(i);
                continue;
            }

            bool tagMatched = (combo.comboMode == ComboEvent.ComboMode.Normal_Cacheable)
                ? tagComponent.ConsumeTag(combo.RequiredTag)
                : tagComponent.HasTag(combo.RequiredTag);

            if (tagMatched && combo.nextSkill != null)
            {
                if (combo.comboMode == ComboEvent.ComboMode.Strict_Immediate)
                    tagComponent.ConsumeTag(combo.RequiredTag);

                _activeComboWindows.RemoveAt(i);

                var model = GetComponent<PlayerModel>();
                if (model != null)
                    model.isComboChain = true;

                // ★ P13: 追击跳 —— 连招目标为空中技能且玩家在地面时，起跳进入空中攻击。
                //   已增强：垂直起跳 + 水平向锁敌目标冲锋，保证贴近被击飞的敌人（而非原地直上直下）。
                if (combo.nextSkill.isAirSkill && PlayerController.Instance != null && PlayerController.Instance.isGround)
                {
                    if (model != null)
                        StartChaseJump(model);
                }

                PlaySkill(combo.nextSkill);
                return; // 已触发连招，停止本轮轮询（防止同帧多个窗口重复触发）
            }
        }
    }

    #endregion

    // --- 技能播放与管理 (方法签名保持不变, 内部清理列表) ---
    #region Skill Playback Management

    public void PlaySkill(SkillTimelineAsset skill)
    {
        if (skill == null) return;
        if (isSwitching) return;

        var model = GetComponent<PlayerModel>();

        // ★ P8: 连招跟随（isComboChain）不打断动画层 —— Animancer 原生交叉淡化换动画，
        //    避免 StopAndCleanup 的 0.25s 攻击层淡出造成“停旧播新”的断帧感。
        //    但仍需清理上一段的活跃事件（判定盒等）与拼刀检测器。
        bool isComboFollowUp = isPlaying && model != null && model.isComboChain;

        if (isPlaying && !isComboFollowUp)
        {
            StopAndCleanup(true, false);
        }
        else if (isComboFollowUp)
        {
            EndActiveEvents();
        }

        isSwitching = true;

        // ★ 标记翻滚/闪避状态（免疫受击）
        if (model != null &&
            (skill == model.dodgeF || skill == model.dodgeB ||
             skill == model.dodgeR || skill == model.dodgeL))
        {
            model.isDodging = true;
        }

        currentSkill = skill;
        _debuggingSkillAsset = skill;
        isPlaying = true;
        currentFrame = -1;
        
        frameStartEvents.Clear();
        frameEndEvents.Clear();
        activeLoopEvents.Clear();
        _activeCancelEvents.Clear();
        _activeEvents.Clear();
        _activeComboWindows.Clear();

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

        // ★ 激活拼刀检测器（武器碰撞检测）
        foreach (var detector in _clashDetectors)
            detector.Activate();

        if (_AttackLayer != null && skill.animationClip != null)
        {
            _AttackAnimation = skill.animationClip;
            float fadeDuration = (model != null && model.isComboChain) ? comboFadeInDuration : attackFadeInDuration;
            var animState = _AttackLayer.Play(_AttackAnimation, fadeDuration, FadeMode.FromStart);
            // ★ 修复“打断后重播卡在被打断位置”：FromStart 会复用同 clip 的零权重状态（不重置时间），
            //   显式把播放时间归零，保证每次都从头播放。
            animState.TimeD = 0;
            _AttackLayer.SetWeight(1f);

            animState.Events(this).OnEnd = skill.animationClip.isLooping ? (Action)null : () =>
            {
                if (_AttackLayer != null && _AttackLayer.CurrentState == animState)
                    HandleAnimationEnd();
            };

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

        // ★ 追击跳冲锋协程在技能停止时一并终止
        if (_chaseCoroutine != null)
        {
            StopCoroutine(_chaseCoroutine);
            _chaseCoroutine = null;
        }

        // ★ 重置翻滚/闪避标记
        var playerModel = GetComponent<PlayerModel>();
        if (playerModel != null) playerModel.isDodging = false;

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
        EndActiveEvents();

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
        
                var playerController = PlayerController.Instance;
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
    
    /// <summary>触发所有已开始但未结束事件的 OnEnd（判定盒关闭等），并停用拼刀检测器</summary>
    private void EndActiveEvents()
    {
        foreach (var evt in _activeEvents)
        {
            try { evt.OnEnd(gameObject); }
            catch (Exception e) { Debug.LogWarning($"PlayerSkillComponent: event OnEnd cleanup threw: {e}"); }
        }
        _activeEvents.Clear();

        foreach (var detector in _clashDetectors) detector.Deactivate();
    }

    /// <summary>
    /// ★ P9: 受击时立即让攻击层让位（停止并清零权重），
    /// 避免攻击动画 0.25s 淡出期间与受击层权重混合（“边挥刀边挨打”）。
    /// </summary>
    public void ForceSuppressAttackLayer()
    {
        if (_AttackLayer == null) return;
        // ★ 只对层做权重清零（安全）。
        //   绝不能对 CurrentState 调 Stop()：若该状态正处交叉淡化组（Play 的 FadeIn），
        //   Stop 会把它从组里移除 → 组变成 FadeIn=null 且仍有 FadeOut → Animancer FadeGroup
        //   在图形更新里抛 NullReferenceException（每帧），导致整个动画系统崩溃、无法攻击。
        _AttackLayer.SetWeight(0f);
    }

    /// <summary>
    /// ★ P13 增强：追击跳 = 垂直起跳 + 水平向锁敌目标冲锋。
    /// 敌人被浮空招击飞时会水平后退约 1~1.5m，若只原地直跳，空中第一段会够不到；
    /// 冲锋保证跳起时贴近敌人（接近后自动减速，避免冲过头）。
    /// </summary>
    public void StartChaseJump(PlayerModel model)
    {
        if (model == null) return;

        // 垂直：起跳（略高于普通跳）
        model.gravityVector.y = Mathf.Sqrt(model.gravity * -2.0f * model.jumpHeight * 1.2f);

        // 水平：朝锁敌目标（优先）或面朝方向冲锋
        Vector3 dir = transform.forward;
        Transform target = null;
        if (model.ts != null && model.ts.HasTarget && model.ts.CurrentTarget != null)
            target = model.ts.CurrentTarget;

        if (_chaseCoroutine != null) StopCoroutine(_chaseCoroutine);
        _chaseCoroutine = StartCoroutine(ChaseRoutine(target, dir, chaseJumpSpeed, chaseJumpDuration));
    }

    private IEnumerator ChaseRoutine(Transform target, Vector3 fallbackDir, float speed, float maxDuration)
    {
        var cc = GetComponent<CharacterController>();
        if (cc == null) yield break;

        Vector3 dir = fallbackDir;
        float elapsed = 0f;
        while (elapsed < maxDuration)
        {
            // 有锁敌目标：动态朝向目标，接近后停止
            if (target != null)
            {
                Vector3 toTarget = target.position - transform.position;
                toTarget.y = 0f;
                if (toTarget.sqrMagnitude < 1.2f * 1.2f) break; // 贴近敌人后停冲
                if (toTarget.sqrMagnitude > 0.0001f)
                    dir = toTarget.normalized;
            }

            cc.Move(dir * speed * Time.deltaTime);
            elapsed += Time.deltaTime;
            yield return null;
        }
        _chaseCoroutine = null;
    }

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