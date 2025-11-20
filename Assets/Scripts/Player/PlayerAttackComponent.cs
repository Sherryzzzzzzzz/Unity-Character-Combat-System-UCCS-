using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using Animancer;


public class PlayerAttackComponent : MonoBehaviour
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

    private void Awake()
    {
        _Animancer = GetComponent<AnimancerComponent>();
        tagComponent = GetComponent<TagComponent>();

        // 初始化攻击层
        if (_Animancer.Layers.Count <= attackLayerIndex)
            _Animancer.Layers.Count = attackLayerIndex + 1;

        _AttackLayer = _Animancer.Layers[attackLayerIndex];
        
        _AttackLayer.SetWeight(0f);
    }

    private void Update()
    {
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

        // 当前帧更新
        currentFrame = Mathf.FloorToInt((float)state.Time * state.Clip.frameRate);

        // ---------- Start ----------
        if (frameStartEvents.TryGetValue(currentFrame, out var starts))
        {
            var startsSnapshot = starts.ToArray();
            foreach (var evt in startsSnapshot)
            {
                // OnStart 的调用保持不变
                evt.OnStart(gameObject);
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
            }
        }

        // ---------- 动画结束 ----------
        if (currentFrame >= maxFrame)
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

        // --- 清理和注册事件 (逻辑不变) ---
        currentSkill = skill;
        isPlaying = true;
        currentFrame = 0;
        frameStartEvents.Clear();
        frameEndEvents.Clear();
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

    public int CurrentSkillMaxFrame() => maxFrame;
    public bool HasCachedInput() => cachedInputAction != null;
    public string GetCachedInputName() => cachedInputAction?.action?.name ?? "None";
}
