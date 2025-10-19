using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using Animancer;

using Animancer;
using UnityEngine;
using UnityEngine.InputSystem;
using System;
using System.Collections.Generic;
using System.Linq;

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

    private void Awake()
    {
        _Animancer = GetComponent<AnimancerComponent>();

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
                try { evt.OnStart(gameObject); }
                catch (Exception ex) { Debug.LogError($"[PlayerAttackComponent] Start event exception: {ex}"); }
            }

            // 连段检测
            foreach (var evt in startsSnapshot)
            {
                if (evt is ComboEvent combo && combo.triggerType == ComboTriggerType.Normal)
                {
                    if (ConsumeCachedInputIfMatch(combo.inputAction))
                    {
                        if (combo.nextSkill != null)
                        {
                            var model = GetComponent<PlayerModel>();
                            if (model != null && isPlaying)
                            {
                                model.isComboChain = true;
                                model.isAttacking = true;
                                //Debug.Log("[PlayerAttackComponent] 连段开始（Start 匹配）");
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
        if (skill == null)
        {
            //Debug.LogError("[PlayerAttackComponent] PlaySkill called with null skill.");
            return;
        }

        if (isPlaying && currentSkill == skill) return;
        if (isSwitching) return;

        isSwitching = true;

        var model = GetComponent<PlayerModel>();
        if (isPlaying && model != null)
        {
            model.isComboChain = true;
            model.isAttacking = true;
            //Debug.Log("[PlayerAttackComponent] PlaySkill: 连段衔接标记 isComboChain=true");
        }

        // 结束上一个事件（快照）
        if (isPlaying)
        {
            var endEventsSnapshot = frameEndEvents.Values.SelectMany(v => v).ToList();
            foreach (var evt in endEventsSnapshot)
            {
                try { evt.OnEnd(gameObject); }
                catch (Exception ex) { Debug.LogError($"[PlayerAttackComponent] OnEnd exception during switch: {ex}"); }
            }
        }

        currentSkill = skill;
        isPlaying = true;
        currentFrame = 0;
        frameStartEvents.Clear();
        frameEndEvents.Clear();

        // 注册事件
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

        // 播放到攻击层
        if (_AttackLayer != null && skill.animationClip != null)
        {
            _AttackAnimation = skill.animationClip;
            var state = _AttackLayer.Play(_AttackAnimation, 0.06f, FadeMode.FixedSpeed);
            state.Speed = 1f;
            _AttackLayer.SetWeight(1f); // 激活攻击层

            maxFrame = Mathf.RoundToInt(state.Clip.length * state.Clip.frameRate);
            //Debug.Log($"[PlayerAttackComponent] PlaySkill -> {skill.name} ({maxFrame} frames) on AttackLayer");
        }
        else
        {
            //Debug.LogWarning("[PlayerAttackComponent] PlaySkill: animationClip is null.");
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
                model.isAttacking = false;
            }
        }

        //Debug.Log("[PlayerAttackComponent] StopAndCleanup executed (AttackLayer).");
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
