using System.Collections.Generic;
using UnityEngine;

public class AbilitySystemComponent : MonoBehaviour
{
    [SerializeField] public AttributeSet Attributes;

    // ===== 旧版 string-key 能力管理（保留向后兼容） =====
    private Dictionary<string, GameplayAbility> abilities =
        new Dictionary<string, GameplayAbility>();

    private GameplayAbility currentAbility;

    // Active ability instance tracking
    private int _nextAbilityHandle = 1;
    private readonly Dictionary<int, GameplayAbility> _activeAbilitiesByHandle = new Dictionary<int, GameplayAbility>();
    private readonly Dictionary<GameplayAbility, int> _activeAbilityHandles = new Dictionary<GameplayAbility, int>();

    // ===== 新版 Spec-based 能力管理（对应 UE5） =====
    /// <summary>所有已授予的能力Spec列表</summary>
    [SerializeField] private List<GameplayAbilitySpec> _activatableAbilities = new List<GameplayAbilitySpec>();
    /// <summary>公开的能力Spec列表（只读访问）</summary>
    public IReadOnlyList<GameplayAbilitySpec> ActivatableAbilities => _activatableAbilities;
    private int _nextSpecHandle = 1000;
    /// <summary>通过 EventTag 触发的能力映射 (Tag → Spec Handles)</summary>
    private readonly Dictionary<GameplayTagSO, List<int>> _triggeredAbilityMap = new Dictionary<GameplayTagSO, List<int>>();

    // ===== 委托系统（对应 UE5 的 Delegates） =====
    public System.Action<GameplayAbility> OnAbilityActivated;
    public System.Action<GameplayAbility> OnAbilityEnded;
    public System.Action<GameplayAbility> OnAbilityCommitted;
    public System.Action<GameplayAbility, GameplayTagSO> OnAbilityFailed;
    public System.Action<AbilitySystemComponent, GameplayEffectSpec, int> OnGameplayEffectAppliedToSelf;
    public System.Action<AbilitySystemComponent, GameplayEffectSpec, int> OnGameplayEffectAppliedToTarget;
    public System.Action<AbilitySystemComponent, GameplayEffectSpec, int> OnActiveGameplayEffectAddedToSelf;
    public System.Action<GameplayTagSO, int> OnTagCountChanged;

    private TagComponent tagComponent;

    [SerializeField] private GameplayTagSO stunnedTag;

    #region Effect 生命周期

    private readonly List<ActiveGameplayEffect> _activeEffects = new List<ActiveGameplayEffect>();
    private readonly Dictionary<int, ActiveGameplayEffect> _activeEffectsByHandle = new Dictionary<int, ActiveGameplayEffect>();
    private readonly Dictionary<GameplayEffect, List<int>> _effectHandleGroups = new Dictionary<GameplayEffect, List<int>>();
    private readonly Dictionary<GameplayEffect, ActiveGameplayEffect> _effectLookup = new Dictionary<GameplayEffect, ActiveGameplayEffect>();

    // cancelOnAbilityEnd 支持：Ability Handle → 关联 Effect Handles
    private readonly Dictionary<int, List<int>> _abilityEffectLinks = new Dictionary<int, List<int>>();

    #endregion

    #region 数据驱动能力

    [Header("数据驱动能力")]
    [SerializeField] private List<GameplayAbilitySO> abilityDataList = new List<GameplayAbilitySO>();

    #endregion

    private void Awake()
    {
        if (Attributes == null)
            Attributes = GetComponent<AttributeSet>();
        if (Attributes == null)
            Attributes = GetComponentInParent<AttributeSet>();
        if (Attributes == null)
            Attributes = GetComponentInChildren<AttributeSet>();
        tagComponent = GetComponent<TagComponent>();

        if (Attributes == null)
            Debug.LogWarning($"{gameObject.name}: AbilitySystemComponent 缺少 AttributeSet 组件", this);

        if (stunnedTag == null)
            Debug.LogWarning($"{gameObject.name}: AbilitySystemComponent 的 stunnedTag 未配置", this);

        if (tagComponent != null)
            tagComponent.OnTagAdded += HandleTagAdded;
    }

    private void Start()
    {
        if (abilityDataList != null)
        {
            foreach (var abilitySO in abilityDataList)
            {
                if (abilitySO == null) continue;
                var ability = abilitySO.CreateRuntimeAbility();
                RegisterAbility(abilitySO.abilityName, ability);
            }
        }
    }

    private void OnEnable()
    {
        if (GASHost.Instance != null)
            GASHost.Instance.RegisterASC(this);
    }

    private void OnDisable()
    {
        if (GASHost.Instance != null)
            GASHost.Instance.UnregisterASC(this);
    }

    private bool _tickedByHost = false;

    private void Update()
    {
        if (!_tickedByHost)
            InternalTick(Time.deltaTime);
        _tickedByHost = false;
    }

    /// <summary>
    /// GASHost 驱动的 Tick
    /// </summary>
    public void TickFromHost(float deltaTime)
    {
        _tickedByHost = true;
        InternalTick(deltaTime);
    }

    private void InternalTick(float deltaTime)
    {
        TickActiveEffects(deltaTime);
        TickActiveTasks(deltaTime);
    }

    #region 能力管理

    public void RegisterAbility(string key, GameplayAbility ability)
    {
        ability.Initialize(this);
        abilities[key] = ability;
    }

    public int ActivateAbility(string key)
    {
        if (!abilities.TryGetValue(key, out var ability))
            return -1;

        if (ability.TryActivate())
        {
            int handle = _nextAbilityHandle++;
            _activeAbilitiesByHandle[handle] = ability;
            _activeAbilityHandles[ability] = handle;
            return handle;
        }
        return -1;
    }

    public bool EndAbilityByHandle(int handle)
    {
        if (_activeAbilitiesByHandle.TryGetValue(handle, out var ability))
        {
            ability.End();
            _activeAbilityHandles.Remove(ability);
            _activeAbilitiesByHandle.Remove(handle);

            // cancelOnAbilityEnd：移除关联效果
            RemoveEffectsForAbility(handle);

            return true;
        }
        return false;
    }

    public int GetHandleForAbility(GameplayAbility ability)
    {
        if (_activeAbilityHandles.TryGetValue(ability, out var h)) return h;
        return -1;
    }

    public void SetCurrentAbility(GameplayAbility ability)
    {
        currentAbility = ability;
    }

    public void ClearCurrentAbility(GameplayAbility ability)
    {
        if (currentAbility == ability)
            currentAbility = null;
    }

    public void InterruptCurrentAbility()
    {
        if (currentAbility == null)
            return;

        if (currentAbility.CanBeInterrupted)
        {
            currentAbility.End();
            currentAbility = null;
        }
    }

    private void HandleTagAdded(GameplayTagSO tag)
    {
        if (stunnedTag != null && tag == stunnedTag)
        {
            InterruptCurrentAbility();
        }
    }

    #endregion

    #region Spec-based 能力管理（对应 UE5 ASC API）

    /// <summary>
    /// 授予一个能力 → 创建 GameplayAbilitySpec 并添加到 ActivatableAbilities
    /// 对应 UE5: UAbilitySystemComponent::GiveAbility
    /// </summary>
    public int GiveAbility(GameplayAbility ability, int level = 1, int inputID = -1, object sourceObject = null)
    {
        if (ability == null) return -1;
        ability.Initialize(this);

        // 检查是否已存在（非Instanced模式复用）
        foreach (var existing in _activatableAbilities)
        {
            if (existing.Ability != null && existing.Ability.GetType() == ability.GetType() &&
                ability.AbilityInstancingPolicy == InstancingPolicy.InstancedPerActor)
            {
                existing.Level = level;
                return existing.Handle;
            }
        }

        int handle = _nextSpecHandle++;
        var spec = new GameplayAbilitySpec(ability, level, inputID, sourceObject)
        {
            Handle = handle
        };
        _activatableAbilities.Add(spec);

        // 自动激活
        if (spec.bActivateOnce)
            TryActivateAbilityBySpec(spec);

        return handle;
    }

    /// <summary>
    /// 通过 Handle 激活 Ability
    /// 对应 UE5: ASC::TryActivateAbility
    /// </summary>
    public bool TryActivateAbilityByHandle(int handle)
    {
        var spec = FindSpecByHandle(handle);
        if (spec == null) return false;
        return TryActivateAbilityBySpec(spec);
    }

    /// <summary>
    /// 通过 GameplayTag 触发所有匹配的 Ability
    /// 对应 UE5: ASC::TryActivateAbilitiesByTag
    /// </summary>
    public int TryActivateAbilitiesByTag(GameplayTagSO tag)
    {
        int count = 0;
        foreach (var spec in _activatableAbilities)
        {
            if (spec.Ability != null && spec.Ability.AbilityTags != null)
            {
                foreach (var abilityTag in spec.Ability.AbilityTags)
                    if (abilityTag == tag || (tag.parentTag != null && abilityTag == tag.parentTag))
                    {
                        if (TryActivateAbilityBySpec(spec)) count++;
                        break;
                    }
            }
        }
        return count;
    }

    private bool TryActivateAbilityBySpec(GameplayAbilitySpec spec)
    {
        if (spec == null || spec.Ability == null) return false;

        var ability = spec.Ability;
        var actorInfo = GameplayAbilityActorInfo.FromSingleActor(gameObject);
        var activationInfo = spec.ActivationInfo;

        // 检查阻塞标签
        if (tagComponent != null)
        {
            foreach (var spec2 in _activatableAbilities)
            {
                if (spec2.Ability != null && spec2.ActiveCount > 0)
                {
                    foreach (var blockTag in spec2.Ability.BlockAbilitiesWithTag)
                        if (tagComponent.HasTagOrChild(blockTag))
                        {
                            OnAbilityFailed?.Invoke(ability, blockTag);
                            return false;
                        }
                }
            }
        }

        // 检查激活条件
        if (!ability.CanActivateAbility(actorInfo))
        {
            OnAbilityFailed?.Invoke(ability, null);
            return false;
        }

        // Instancing
        GameplayAbility instance;
        if (ability.AbilityInstancingPolicy == InstancingPolicy.InstancedPerActor)
        {
            instance = spec.ReplicatedInstances.Count > 0 ? spec.ReplicatedInstances[0] : ability;
            if (spec.ReplicatedInstances.Count == 0)
                spec.ReplicatedInstances.Add(instance);
        }
        else // InstancedPerExecution (default)
        {
            // 创建新实例（简化：复用同一个对象但重置关键状态）
            instance = ability;
        }

        // PreActivate（新增钩子）
        instance.PreActivate(spec.Handle, actorInfo);

        // 取消被此能力标记为CancelByTag的其他能力
        foreach (var cancelTag in ability.CancelAbilitiesWithTag)
        {
            CancelAbilitiesWithTag(cancelTag);
        }

        // 授予 ActivationOwnedTags
        if (tagComponent != null)
        {
            foreach (var tag in ability.ActivationOwnedTags)
                tagComponent.AddTag(tag);
        }

        // Activate
        instance.SetCurrentActorInfo(spec.Handle, actorInfo);
        spec.ActiveCount++;
        int abilityHandle = _nextAbilityHandle++;
        _activeAbilitiesByHandle[abilityHandle] = instance;
        _activeAbilityHandles[instance] = abilityHandle;

        try
        {
            instance.ActivateAbility(spec.Handle, actorInfo);
            OnAbilityActivated?.Invoke(instance);
            return true;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"ASC: ActivateAbility threw: {e}");
            spec.ActiveCount--;
            OnAbilityFailed?.Invoke(instance, null);
            return false;
        }
    }

    /// <summary>
    /// 取消拥有指定Tag的所有活跃Ability
    /// 对应 UE5: ASC::CancelAbilitiesWithTag
    /// </summary>
    public void CancelAbilitiesWithTag(GameplayTagSO tag)
    {
        if (tag == null) return;
        foreach (var spec in _activatableAbilities)
        {
            if (spec.ActiveCount > 0 && spec.Ability != null)
            {
                foreach (var abilityTag in spec.Ability.AbilityTags)
                    if (abilityTag == tag || abilityTag.HasChild(tag))
                    {
                        var actorInfo = GameplayAbilityActorInfo.FromSingleActor(gameObject);
                        spec.Ability.EndAbility(spec.Handle, actorInfo, spec.ActivationInfo, true);
                        break;
                    }
            }
        }
    }

    /// <summary>
    /// 通过 Handle 取消特定 Ability
    /// </summary>
    public void CancelAbilityByHandle(int handle)
    {
        var spec = FindSpecByHandle(handle);
        if (spec != null && spec.ActiveCount > 0)
        {
            var actorInfo = GameplayAbilityActorInfo.FromSingleActor(gameObject);
            spec.Ability.EndAbility(spec.Handle, actorInfo, spec.ActivationInfo, true);
        }
    }

    private GameplayAbilitySpec FindSpecByHandle(int handle)
    {
        foreach (var spec in _activatableAbilities)
            if (spec.Handle == handle) return spec;
        return null;
    }

    /// <summary>
    /// 设置输入按下/释放（用于 InputID 绑定的 Ability）
    /// </summary>
    public void SetInputPressed(int handle)
    {
        var spec = FindSpecByHandle(handle);
        if (spec != null) spec.InputPressed = true;
    }

    public void SetInputReleased(int handle)
    {
        var spec = FindSpecByHandle(handle);
        if (spec != null) spec.InputPressed = false;
    }

    #endregion

    #region EffectContext + MakeOutgoingSpec（对应 UE5）

    /// <summary>
    /// 创建 EffectContext
    /// 对应 UE5: ASC::MakeEffectContext
    /// </summary>
    public GameplayEffectContext MakeEffectContext()
    {
        return new GameplayEffectContext(this, gameObject);
    }

    /// <summary>
    /// 创建出站 GameplayEffectSpec
    /// 对应 UE5: ASC::MakeOutgoingSpec
    /// </summary>
    public GameplayEffectSpec MakeOutgoingSpec(GameplayEffect effect, float level, GameplayEffectContext context = null)
    {
        if (effect == null) return null;
        var spec = EffectSpecFactory.CreateSpec(effect, this);
        if (context != null)
        {
            spec.Context = context;
            spec.Level = level;
            // 将 Context 中的 SetByCaller magnitudes 复制到 Spec
            if (context.SetByCallerMagnitudes != null)
            {
                foreach (var kvp in context.SetByCallerMagnitudes)
                    spec.SetByCallerMagnitude(kvp.Key, kvp.Value);
            }
        }
        return spec;
    }

    /// <summary>
    /// 向 Target 施加 EffectSpec（携带完整 Context）
    /// 对应 UE5: ASC::ApplyGameplayEffectSpecToTarget
    /// </summary>
    public int ApplyGameplayEffectSpecToTarget(GameplayEffectSpec spec, AbilitySystemComponent target)
    {
        if (spec == null || target == null) return -1;
        int handle = target.ApplyEffectSpec(spec);
        if (handle > 0)
        {
            OnGameplayEffectAppliedToTarget?.Invoke(target, spec, handle);
        }
        return handle;
    }

    /// <summary>
    /// 向 Self 施加 EffectSpec
    /// 对应 UE5: ASC::ApplyGameplayEffectSpecToSelf
    /// </summary>
    public int ApplyGameplayEffectSpecToSelf(GameplayEffectSpec spec)
    {
        if (spec == null) return -1;
        int handle = ApplyEffectSpec(spec);
        if (handle > 0)
        {
            OnGameplayEffectAppliedToSelf?.Invoke(this, spec, handle);
            OnActiveGameplayEffectAddedToSelf?.Invoke(this, spec, handle);
        }
        return handle;
    }

    #endregion

    #region GameplayEvent 事件系统（对应 UE5）

    /// <summary>
    /// 触发 GameplayEvent → 激活所有监听该 Tag 的 Ability
    /// 对应 UE5: ASC::HandleGameplayEvent
    /// 返回成功触发的 Ability 数量
    /// </summary>
    public int HandleGameplayEvent(GameplayTagSO eventTag, GameplayEventData payload = null)
    {
        if (eventTag == null) return 0;
        int triggered = 0;

        // 1) 通过 _triggeredAbilityMap 查找显式注册的 Ability（精确Tag匹配）
        if (_triggeredAbilityMap.TryGetValue(eventTag, out var specHandles))
        {
            foreach (var handle in specHandles)
            {
                var spec = FindSpecByHandle(handle);
                if (spec != null && spec.Ability != null && spec.Ability.ShouldAbilityRespondToEvent(eventTag, payload))
                {
                    if (TryActivateAbilityBySpec(spec))
                        triggered++;
                }
            }
        }

        // 2) 遍历所有 Ability，检查哪些监听此事件Tag（通过 TagHierarchy）
        foreach (var spec in _activatableAbilities)
        {
            if (spec.Ability == null) continue;
            if (spec.Ability.ShouldAbilityRespondToEvent(eventTag, payload))
            {
                // 避免重复激活
                if (_triggeredAbilityMap.TryGetValue(eventTag, out var registered) && registered.Contains(spec.Handle))
                    continue;
                if (TryActivateAbilityBySpec(spec))
                    triggered++;
            }
        }

        return triggered;
    }

    /// <summary>
    /// 获取当前活跃效果数量
    /// </summary>
    public int GetNumActiveGameplayEffects() => _activeEffects.Count;

    /// <summary>
    /// 注册一个 Ability 作为特定 GameplayEvent 的触发目标
    /// </summary>
    public void RegisterAbilityForEvent(int specHandle, GameplayTagSO eventTag)
    {
        if (!_triggeredAbilityMap.TryGetValue(eventTag, out var handles))
        {
            handles = new List<int>();
            _triggeredAbilityMap[eventTag] = handles;
        }
        if (!handles.Contains(specHandle))
            handles.Add(specHandle);
    }

    #endregion

    #region Loose Tags（对应 UE5 AddLooseGameplayTag）

    /// <summary>
    /// 添加不通过GE的"松散标签"（如代码直接标记状态）
    /// 对应 UE5: ASC::AddLooseGameplayTag
    /// </summary>
    public void AddLooseGameplayTag(GameplayTagSO tag, int count = 1)
    {
        if (tagComponent != null && tag != null)
        {
            for (int i = 0; i < count; i++)
                tagComponent.AddTag(tag);
            OnTagCountChanged?.Invoke(tag, tagComponent.GetTagCount(tag));
        }
    }

    public void RemoveLooseGameplayTag(GameplayTagSO tag, int count = 1)
    {
        if (tagComponent != null && tag != null)
        {
            for (int i = 0; i < count; i++)
                tagComponent.RemoveTag(tag);
            OnTagCountChanged?.Invoke(tag, tagComponent.GetTagCount(tag));
        }
    }

    /// <summary>
    /// 获取指定Tag的计数（包括Loose Tag和GE授予的Tag）
    /// 对应 UE5: ASC::GetGameplayTagCount
    /// </summary>
    public int GetTagCount(GameplayTagSO tag)
    {
        return tagComponent?.GetTagCount(tag) ?? 0;
    }

    /// <summary>
    /// 注册Tag变化事件
    /// 对应 UE5: ASC::RegisterGameplayTagEvent
    /// </summary>
    public void RegisterGameplayTagEvent(GameplayTagSO tag, System.Action<GameplayTagSO, int> callback)
    {
        OnTagCountChanged += (changedTag, count) =>
        {
            if (changedTag == tag || tag.HasChild(changedTag))
                callback?.Invoke(changedTag, count);
        };
    }

    #endregion

    #region AbilityTask 管理

    private readonly Dictionary<int, List<AbilityTask>> _abilityTasks = new Dictionary<int, List<AbilityTask>>();
    private readonly List<AbilityTask> _allActiveTasks = new List<AbilityTask>();

    /// <summary>
    /// 注册 Task 到指定 Ability Handle
    /// </summary>
    public void RegisterTask(int abilityHandle, AbilityTask task)
    {
        if (!_abilityTasks.TryGetValue(abilityHandle, out var tasks))
        {
            tasks = new List<AbilityTask>();
            _abilityTasks[abilityHandle] = tasks;
        }
        tasks.Add(task);
        _allActiveTasks.Add(task);
    }

    /// <summary>
    /// 取消指定 Ability 的所有关联 Task
    /// </summary>
    public void CancelTasksForAbility(int abilityHandle)
    {
        if (_abilityTasks.TryGetValue(abilityHandle, out var tasks))
        {
            foreach (var task in tasks)
            {
                if (task.IsActive && !task.IsFinished)
                {
                    try { task.Cancel(); }
                    catch (System.Exception e) { Debug.LogWarning($"CancelTasksForAbility: {e}"); }
                }
            }
            _abilityTasks.Remove(abilityHandle);
        }
    }

    private void TickActiveTasks(float deltaTime)
    {
        for (int i = _allActiveTasks.Count - 1; i >= 0; i--)
        {
            var task = _allActiveTasks[i];
            if (task.IsFinished)
            {
                _allActiveTasks.RemoveAt(i);
                continue;
            }
            if (task.IsActive)
            {
                try { task.Tick(deltaTime); }
                catch (System.Exception e) { Debug.LogWarning($"TickActiveTasks: {e}"); }
            }
        }
    }

    #endregion

    #region Effect 施加与管理

    /// <summary>
    /// 创建 GameplayEffectSpec，使用 EffectSpecFactory 自动匹配子类
    /// </summary>
    public GameplayEffectSpec MakeEffectSpec(GameplayEffect effect)
    {
        return EffectSpecFactory.CreateSpec(effect, this);
    }

    /// <summary>
    /// 便捷方法：向后兼容的效果施加入口
    /// </summary>
    public int ApplyGameplayEffect(GameplayEffect effect, AbilitySystemComponent attackerASC)
    {
        if (effect == null) return -1;
        var spec = EffectSpecFactory.CreateSpec(effect, attackerASC);
        return ApplyEffectSpec(spec);
    }

    public int ApplyGameplayEffect(GameplayEffect effect, AbilitySystemComponent instigatorASC, AbilitySystemComponent targetASC)
    {
        if (effect == null || targetASC == null) return -1;
        var spec = EffectSpecFactory.CreateSpec(effect, instigatorASC);
        return targetASC.ApplyEffectSpec(spec);
    }

    /// <summary>
    /// 对多个目标施加效果
    /// </summary>
    public void ApplyEffectToTargets(GameplayEffect effect, TargetData targetData)
    {
        if (effect == null || targetData == null || !targetData.HasTargets) return;
        foreach (var target in targetData.TargetActors)
        {
            if (target != null)
            {
                var spec = EffectSpecFactory.CreateSpec(effect, this);
                target.ApplyEffectSpec(spec);
            }
        }
    }

    /// <summary>
    /// 通过 GameplayEffectSpec 施加效果
    /// </summary>
    public int ApplyEffectSpec(GameplayEffectSpec spec)
    {
        if (spec == null || spec.EffectData == null) return -1;

        var effect = spec.EffectData;

        // Application Tags 检查
        if (tagComponent != null)
        {
            foreach (var requiredTag in effect.applicationRequiredTags)
            {
                if (!tagComponent.HasTag(requiredTag))
                    return -1;
            }
            foreach (var blockedTag in effect.applicationBlockedTags)
            {
                if (tagComponent.HasTag(blockedTag))
                    return -1;
            }

            // ★ Immunity 检查
            foreach (var immunityQuery in effect.applicationImmunityQueries)
            {
                if (immunityQuery.Matches(tagComponent))
                {
                    Debug.Log($"ApplyEffectSpec: {effect.name} blocked by immunity query on {gameObject.name}");
                    return -1;
                }
            }

            // ★ RemoveGameplayEffectsWithTags: 施加前先移除拥有这些Tag的活跃GE
            foreach (var removeTag in effect.removeGameplayEffectsWithTags)
            {
                RemoveActiveEffectsWithTag(removeTag);
            }
        }

        // ★ Grant Abilities: Duration/Infinite GE 激活时授予能力
        int appliedHandle;
        try
        {
            switch (effect.durationPolicy)
            {
                case DurationPolicy.Instant:
                    ExecuteInstantEffect(spec);
                    NotifyCueExecute(effect, spec);
                    return 0;

                case DurationPolicy.Duration:
                case DurationPolicy.Infinite:
                    int handle = ApplyDurationEffect(spec);
                    if (handle <= 0)
                        return -1;
                    NotifyCueAdd(effect, spec);

                    // ★ Grant Abilities
                    if (effect.grantedAbilities != null && effect.grantedAbilities.Count > 0)
                    {
                        foreach (var granted in effect.grantedAbilities)
                        {
                            if (granted.Ability != null)
                            {
                                var runtimeAbility = granted.Ability.CreateRuntimeAbility();
                                int grantedHandle = GiveAbility(runtimeAbility, granted.Level, granted.InputID, granted.SourceObject);
                                // 记录来自哪个 ActiveGE Handle (用于 GE 移除时收回)
                                _grantedAbilityMap[grantedHandle] = handle;
                            }
                        }
                    }

                    // 调用生命周期回调
                    try { spec.OnInitialApply(this); }
                    catch (System.Exception e) { Debug.LogWarning($"OnInitialApply threw: {e}"); }

                    return handle;

                default:
                    return -1;
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"ApplyEffectSpec: exception while applying effect {effect.name}: {e}");
            return -1;
        }
    }

    /// <summary>
    /// 移除拥有指定Tag的所有活跃GE
    /// </summary>
    public void RemoveActiveEffectsWithTag(GameplayTagSO tag)
    {
        if (tag == null) return;
        for (int i = _activeEffects.Count - 1; i >= 0; i--)
        {
            var active = _activeEffects[i];
            foreach (var grantedTag in active.EffectData.grantedTags)
            {
                if (grantedTag == tag || tag.HasChild(grantedTag))
                {
                    RemoveActiveEffectInternal(active);
                    break;
                }
            }
        }
    }

    /// <summary>
    /// 记录 Ability Handle → ActiveGE Handle 的映射（用于 GE 移除时收回 Ability）
    /// </summary>
    private readonly Dictionary<int, int> _grantedAbilityMap = new Dictionary<int, int>();

    /// <summary>
    /// 施加效果并关联到 Ability Handle（用于 cancelOnAbilityEnd）
    /// </summary>
    public int ApplyEffectSpecLinkedToAbility(GameplayEffectSpec spec, int abilityHandle)
    {
        int effectHandle = ApplyEffectSpec(spec);
        if (effectHandle > 0 && spec.EffectData.cancelOnAbilityEnd)
        {
            LinkEffectToAbility(abilityHandle, effectHandle);
        }
        return effectHandle;
    }

    private void LinkEffectToAbility(int abilityHandle, int effectHandle)
    {
        if (!_abilityEffectLinks.TryGetValue(abilityHandle, out var handles))
        {
            handles = new List<int>();
            _abilityEffectLinks[abilityHandle] = handles;
        }
        handles.Add(effectHandle);
    }

    private void RemoveEffectsForAbility(int abilityHandle)
    {
        if (_abilityEffectLinks.TryGetValue(abilityHandle, out var effectHandles))
        {
            foreach (var effectHandle in effectHandles)
            {
                RemoveActiveEffectByHandle(effectHandle);
            }
            _abilityEffectLinks.Remove(abilityHandle);
        }

        // 同时取消关联 Task
        CancelTasksForAbility(abilityHandle);
    }

    /// <summary>
    /// 即时效果：使用 ExecutionCalculation 或 Spec 快照属性进行计算
    /// </summary>
    private void ExecuteInstantEffect(GameplayEffectSpec spec)
    {
        var effect = spec.EffectData;
        var targetAttr = this.Attributes;
        if (targetAttr == null) return;

        // 优先使用 ExecutionCalculation
        if (effect.executionCalculation != null)
        {
            try
            {
                var output = new EffectExecutionOutput
                {
                    Modifications = new List<AttributeModification>()
                };
                effect.executionCalculation.Execute(spec.InstigatorASC, this, spec, ref output);
                ApplyExecutionOutput(output);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"ExecutionCalculation threw exception: {e}");
            }
        }
        else
        {
            // Fallback: 原有硬编码伤害计算
            if (effect.damage > 0f || effect.poiseDamage > 0f)
            {
                float attackPower = 0f;
                spec.CapturedAttackerAttributes.TryGetValue(GameplayAttribute.AttackPower, out attackPower);
                float defense = targetAttr.Defense;

                float finalDamage = (effect.damage * effect.damageMultiplier) + attackPower - defense;
                finalDamage = Mathf.Max(finalDamage, 1f);

                targetAttr.ModifyHealth(-finalDamage);
                targetAttr.ModifyPoise(-effect.poiseDamage);

                Debug.Log($"{gameObject.name} took {finalDamage} damage");
            }
        }

        // Instant 效果的属性修改器直接修改 baseValue
        for (int i = 0; i < effect.modifiers.Count; i++)
        {
            var mod = effect.modifiers[i];
            var attrValue = targetAttr.GetAttributeValue(mod.attribute);
            if (attrValue != null)
            {
                float magnitude = spec.GetMagnitude(i, this);
                attrValue.BaseValue += magnitude;
            }
        }
    }

    /// <summary>
    /// 应用 ExecutionCalculation 的输出
    /// </summary>
    private void ApplyExecutionOutput(EffectExecutionOutput output)
    {
        if (output.Modifications == null || Attributes == null) return;

        foreach (var modification in output.Modifications)
        {
            var attrValue = Attributes.GetAttributeValue(modification.Attribute);
            if (attrValue == null) continue;

            switch (modification.Type)
            {
                case AttributeModificationType.ModifyBaseValue:
                    attrValue.BaseValue += modification.Magnitude;
                    break;
                case AttributeModificationType.Additive:
                    attrValue.AddModifier(new AttributeModifier(ModifierType.Additive, modification.Magnitude));
                    break;
                case AttributeModificationType.Multiplicative:
                    attrValue.AddModifier(new AttributeModifier(ModifierType.Multiplicative, modification.Magnitude));
                    break;
                case AttributeModificationType.Override:
                    attrValue.AddModifier(new AttributeModifier(ModifierType.Override, modification.Magnitude));
                    break;
            }
        }
    }

    /// <summary>
    /// Duration/Infinite 效果：创建 ActiveGameplayEffect，返回 Handle
    /// </summary>
    private int ApplyDurationEffect(GameplayEffectSpec spec)
    {
        var effect = spec.EffectData;

        // Check stacking
        if (_effectHandleGroups.TryGetValue(effect, out var handles) && handles.Count > 0)
        {
            var existingHandle = handles[0];
            if (_activeEffectsByHandle.TryGetValue(existingHandle, out var existing))
            {
                switch (effect.stackingPolicy)
                {
                    case StackingPolicy.None:
                        return -1;
                    case StackingPolicy.RefreshDuration:
                        if (effect.refreshPolicy == DurationRefreshPolicy.ExtendOnRefresh)
                            existing.Extend(effect.duration);
                        else
                            existing.Refresh();
                        try { spec.OnRefresh(); }
                        catch (System.Exception e) { Debug.LogWarning($"OnRefresh threw: {e}"); }
                        return existing.Handle;
                    case StackingPolicy.AddStacks:
                        if (existing.CurrentStacks >= effect.maxStacks)
                        {
                            // 堆叠溢出
                            if (effect.overflowPolicy == OverflowPolicy.TriggerOverflowEffect && effect.overflowEffect != null)
                            {
                                ApplyGameplayEffect(effect.overflowEffect, spec.InstigatorASC);
                            }
                            try { spec.OnOverflow(this); }
                            catch (System.Exception e) { Debug.LogWarning($"OnOverflow threw: {e}"); }
                            return existing.Handle;
                        }
                        existing.AddStack();
                        // 通知关联的 AttributeValue 重算（StackCount 感知）
                        NotifyStackCountChanged(existing);
                        return existing.Handle;
                }
            }
        }

        // Transactional apply
        var appliedModifiers = new List<(AttributeValue, AttributeModifier)>();
        var appliedTags = new List<GameplayTagSO>();

        ActiveGameplayEffect activeEffect = null;

        try
        {
            activeEffect = new ActiveGameplayEffect(effect, spec.InstigatorASC, spec);

            if (Attributes != null)
            {
                for (int i = 0; i < effect.modifiers.Count; i++)
                {
                    var mod = effect.modifiers[i];
                    var attrValue = Attributes.GetAttributeValue(mod.attribute);
                    if (attrValue == null)
                    {
                        throw new System.Exception($"ApplyDurationEffect: target AttributeValue for {mod.attribute} is missing");
                    }

                    float magnitude = spec.GetMagnitude(i, this);
                    var modifier = new AttributeModifier(mod.modifierType, magnitude, activeEffect);
                    attrValue.AddModifier(modifier);
                    appliedModifiers.Add((attrValue, modifier));
                    activeEffect.RegisteredModifiers.Add(modifier);
                }
            }
            else if (effect.modifiers.Count > 0)
            {
                throw new System.Exception("ApplyDurationEffect: Attributes is null but effect has modifiers");
            }

            // 周期 Tick
            if (effect.period > 0f)
            {
                activeEffect.OnPeriodicTick = () =>
                {
                    try
                    {
                        ExecutePeriodicTick(activeEffect);
                        spec.OnPeriodicExecute(this);
                    }
                    catch (System.Exception e) { Debug.LogWarning($"PeriodicTick exception: {e}"); }
                };
            }

            // Commit to runtime lists
            _activeEffects.Add(activeEffect);
            _activeEffectsByHandle[activeEffect.Handle] = activeEffect;
            if (!_effectHandleGroups.TryGetValue(effect, out var list))
            {
                list = new List<int>();
                _effectHandleGroups[effect] = list;
            }
            list.Add(activeEffect.Handle);
            _effectLookup[effect] = activeEffect;

            // 授予标签
            if (tagComponent != null)
            {
                foreach (var tag in effect.grantedTags)
                {
                    tagComponent.AddTag(tag);
                    appliedTags.Add(tag);
                }
            }

            return activeEffect.Handle;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"ApplyDurationEffect: exception while applying duration effect {effect.name}: {e}");
            foreach (var (attrVal, modifier) in appliedModifiers)
            {
                try { attrVal.RemoveModifier(modifier); }
                catch (System.Exception ex) { Debug.LogWarning($"Rollback RemoveModifier exception: {ex}"); }
            }
            foreach (var tag in appliedTags)
            {
                try { tagComponent?.RemoveTag(tag); }
                catch (System.Exception ex) { Debug.LogWarning($"Rollback RemoveTag exception: {ex}"); }
            }

            return -1;
        }
    }

    /// <summary>
    /// 当 StackCount 变化时通知关联的 AttributeValue 重算
    /// </summary>
    private void NotifyStackCountChanged(ActiveGameplayEffect activeEffect)
    {
        if (Attributes == null) return;
        foreach (var modifier in activeEffect.RegisteredModifiers)
        {
            var attrValue = FindAttributeValueForModifier(activeEffect, modifier);
            attrValue?.SetDirty();
        }
    }

    private AttributeValue FindAttributeValueForModifier(ActiveGameplayEffect activeEffect, AttributeModifier modifier)
    {
        if (activeEffect.EffectData == null || Attributes == null) return null;
        // Find by matching index in RegisteredModifiers to effect.modifiers
        int idx = activeEffect.RegisteredModifiers.IndexOf(modifier);
        if (idx >= 0 && idx < activeEffect.EffectData.modifiers.Count)
        {
            return Attributes.GetAttributeValue(activeEffect.EffectData.modifiers[idx].attribute);
        }
        return null;
    }

    /// <summary>
    /// 周期 Tick 执行
    /// </summary>
    private void ExecutePeriodicTick(ActiveGameplayEffect activeEffect)
    {
        var effect = activeEffect.EffectData;
        if (Attributes == null) return;

        if (effect.damage > 0f || effect.poiseDamage > 0f)
        {
            float attackPower = 0f;
            if (activeEffect.Spec != null)
            {
                activeEffect.Spec.CapturedAttackerAttributes.TryGetValue(GameplayAttribute.AttackPower, out attackPower);
            }
            else if (activeEffect.InstigatorASC != null && activeEffect.InstigatorASC.Attributes != null)
            {
                attackPower = activeEffect.InstigatorASC.Attributes.AttackPower;
            }

            float defense = Attributes.Defense;

            float finalDamage = (effect.damage * effect.damageMultiplier) + attackPower - defense;
            finalDamage = Mathf.Max(finalDamage, 1f);

            Attributes.ModifyHealth(-finalDamage);
            Attributes.ModifyPoise(-effect.poiseDamage);

            Debug.Log($"{gameObject.name} took {finalDamage} periodic damage");
        }
    }

    /// <summary>
    /// 每帧 tick 所有活跃效果
    /// </summary>
    private void TickActiveEffects(float deltaTime)
    {
        for (int i = _activeEffects.Count - 1; i >= 0; i--)
        {
            var activeEffect = _activeEffects[i];
            activeEffect.Tick(deltaTime);

            if (activeEffect.IsExpired)
            {
                HandleEffectExpiration(activeEffect);
            }
        }
    }

    /// <summary>
    /// 处理效果到期（支持 ExpirationPolicy）
    /// </summary>
    private void HandleEffectExpiration(ActiveGameplayEffect activeEffect)
    {
        var effect = activeEffect.EffectData;

        if (effect.expirationPolicy == ExpirationPolicy.RemoveOneStack && activeEffect.CurrentStacks > 1)
        {
            // 只移除一层，刷新持续时间
            activeEffect.RemoveStack();
            activeEffect.Refresh();
            NotifyStackCountChanged(activeEffect);
        }
        else
        {
            // 移除全部
            try { activeEffect.Spec?.OnComplete(this); }
            catch (System.Exception e) { Debug.LogWarning($"OnComplete threw: {e}"); }
            RemoveActiveEffectInternal(activeEffect);
        }
    }

    /// <summary>
    /// 手动移除一个活跃效果
    /// </summary>
    public void RemoveActiveEffect(ActiveGameplayEffect activeEffect)
    {
        if (activeEffect == null) return;
        RemoveActiveEffectInternal(activeEffect);
    }

    /// <summary>
    /// 按 Handle 移除活跃效果
    /// </summary>
    public bool RemoveActiveEffectByHandle(int handle)
    {
        for (int i = 0; i < _activeEffects.Count; i++)
        {
            if (_activeEffects[i].Handle == handle)
            {
                RemoveActiveEffectInternal(_activeEffects[i]);
                return true;
            }
        }
        return false;
    }

    private void RemoveActiveEffectInternal(ActiveGameplayEffect activeEffect)
    {
        if (activeEffect == null) return;

        // 注销属性修改器
        if (Attributes != null)
        {
            int modIndex = 0;
            foreach (var mod in activeEffect.EffectData.modifiers)
            {
                if (modIndex < activeEffect.RegisteredModifiers.Count)
                {
                    try
                    {
                        var attrValue = Attributes.GetAttributeValue(mod.attribute);
                        if (attrValue != null)
                        {
                            var registeredModifier = activeEffect.RegisteredModifiers[modIndex];
                            attrValue.RemoveModifier(registeredModifier);
                        }
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogWarning($"RemoveActiveEffectInternal: exception while removing modifier: {e}");
                    }
                }
                modIndex++;
            }
        }

        // 移除授予标签
        if (tagComponent != null)
        {
            foreach (var tag in activeEffect.EffectData.grantedTags)
            {
                try { tagComponent.RemoveTag(tag); }
                catch (System.Exception e) { Debug.LogWarning($"RemoveActiveEffectInternal: RemoveTag exception: {e}"); }
            }
        }

        // Cue 分发
        try { NotifyCueRemove(activeEffect.EffectData); }
        catch (System.Exception e) { Debug.LogWarning($"NotifyCueRemove exception: {e}"); }

        // 清理 runtime lookup containers
        _activeEffects.Remove(activeEffect);
        _activeEffectsByHandle.Remove(activeEffect.Handle);
        if (_effectHandleGroups.TryGetValue(activeEffect.EffectData, out var handles))
        {
            handles.Remove(activeEffect.Handle);
            if (handles.Count == 0) _effectHandleGroups.Remove(activeEffect.EffectData);
        }
        if (_effectLookup.TryGetValue(activeEffect.EffectData, out var existing) && existing == activeEffect)
        {
            _effectLookup.Remove(activeEffect.EffectData);
        }
    }

    #endregion

    #region GameplayCue 分发

    private void NotifyCueExecute(GameplayEffect effect, GameplayEffectSpec spec)
    {
        if (effect.cueTag == null) return;
        var cueManager = GameplayCueManager.Instance;
        if (cueManager != null)
            cueManager.ExecuteCue(effect.cueTag, gameObject, spec);
    }

    private void NotifyCueAdd(GameplayEffect effect, GameplayEffectSpec spec)
    {
        if (effect.cueTag == null) return;
        var cueManager = GameplayCueManager.Instance;
        if (cueManager != null)
            cueManager.AddCue(effect.cueTag, gameObject, spec);
    }

    private void NotifyCueRemove(GameplayEffect effect)
    {
        if (effect.cueTag == null) return;
        var cueManager = GameplayCueManager.Instance;
        if (cueManager != null)
            cueManager.RemoveCue(effect.cueTag, gameObject);
    }

    #endregion
}
