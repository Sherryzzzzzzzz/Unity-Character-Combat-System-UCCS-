using System.Collections.Generic;
using UnityEngine;

public class AbilitySystemComponent : MonoBehaviour
{
    [SerializeField] public AttributeSet Attributes;

    private Dictionary<string, GameplayAbility> abilities =
        new Dictionary<string, GameplayAbility>();

    private GameplayAbility currentAbility;

    private TagComponent tagComponent;

    [SerializeField] private GameplayTagSO stunnedTag;

    #region Effect 生命周期

    private readonly List<ActiveGameplayEffect> _activeEffects = new List<ActiveGameplayEffect>();
    // Primary lookup by handle to support multiple instances per GameplayEffect SO
    private readonly Dictionary<int, ActiveGameplayEffect> _activeEffectsByHandle = new Dictionary<int, ActiveGameplayEffect>();
    // Secondary grouping by SO -> list of handles for stacking queries
    private readonly Dictionary<GameplayEffect, List<int>> _effectHandleGroups = new Dictionary<GameplayEffect, List<int>>();
    private readonly Dictionary<GameplayEffect, ActiveGameplayEffect> _effectLookup =
        new Dictionary<GameplayEffect, ActiveGameplayEffect>();

    #endregion

    #region 数据驱动能力

    [Header("数据驱动能力")]
    [SerializeField] private List<GameplayAbilitySO> abilityDataList = new List<GameplayAbilitySO>();

    #endregion

    private void Awake()
    {
        Attributes = GetComponent<AttributeSet>();
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
        // 从数据资产自动注册能力
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

    private void Update()
    {
        TickActiveEffects(Time.deltaTime);
    }

    #region 能力管理

    public void RegisterAbility(string key, GameplayAbility ability)
    {
        ability.Initialize(this);
        abilities[key] = ability;
    }

    public void ActivateAbility(string key)
    {
        if (!abilities.TryGetValue(key, out var ability))
            return;

        ability.TryActivate();
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

    #region Effect 施加与管理

    /// <summary>
    /// 创建 GameplayEffectSpec，捕获当前 ASC 的属性快照
    /// </summary>
    public GameplayEffectSpec MakeEffectSpec(GameplayEffect effect)
    {
        return new GameplayEffectSpec(effect, this);
    }

    /// <summary>
    /// 便捷方法：向后兼容的效果施加入口
    /// 内部创建 Spec 并委托给 ApplyEffectSpec
    /// </summary>
    public int ApplyGameplayEffect(GameplayEffect effect, AbilitySystemComponent attackerASC)
    {
        if (effect == null) return -1;
        var spec = new GameplayEffectSpec(effect, attackerASC);
        return ApplyEffectSpec(spec);
    }

    // Convenience overload to apply effect where caller can specify both instigator and explicit target ASC
    public int ApplyGameplayEffect(GameplayEffect effect, AbilitySystemComponent instigatorASC, AbilitySystemComponent targetASC)
    {
        if (effect == null || targetASC == null) return -1;
        var spec = new GameplayEffectSpec(effect, instigatorASC);
        return targetASC.ApplyEffectSpec(spec);
    }

    /// <summary>
    /// 通过 GameplayEffectSpec 施加效果
    /// 返回 Handle：Duration/Infinite 返回正值，Instant 返回 0，被拒绝返回 -1
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
        }

        try
        {
            switch (effect.durationPolicy)
            {
                case DurationPolicy.Instant:
                    ExecuteInstantEffect(spec);
                    // Cue 分发：Instant → ExecuteCue
                    NotifyCueExecute(effect, spec);
                    return 0;

                case DurationPolicy.Duration:
                case DurationPolicy.Infinite:
                    int handle = ApplyDurationEffect(spec);
                    if (handle <= 0)
                        return -1;
                    // Cue 分发：Duration/Infinite → AddCue
                    NotifyCueAdd(effect, spec);
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
    /// 即时效果：使用 Spec 快照属性进行伤害计算
    /// </summary>
    private void ExecuteInstantEffect(GameplayEffectSpec spec)
    {
        var effect = spec.EffectData;
        var targetAttr = this.Attributes;
        if (targetAttr == null) return;

        // 伤害计算 — 使用快照属性值
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

        // Instant 效果的属性修改器直接修改 baseValue（使用 GetMagnitude）
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
    /// Duration/Infinite 效果：创建 ActiveGameplayEffect，返回 Handle
    /// </summary>
    private int ApplyDurationEffect(GameplayEffectSpec spec)
    {
        var effect = spec.EffectData;

        // Check stacking using secondary handle groups to allow multiple instances per SO
        if (_effectHandleGroups.TryGetValue(effect, out var handles) && handles.Count > 0)
        {
            // For simplicity consult the first existing handle's active effect for stacking policy
            var existingHandle = handles[0];
            if (_activeEffectsByHandle.TryGetValue(existingHandle, out var existing))
            {
                switch (effect.stackingPolicy)
                {
                    case StackingPolicy.None:
                        return -1; // 忽略重复施加
                    case StackingPolicy.RefreshDuration:
                        existing.Refresh();
                        return existing.Handle;
                    case StackingPolicy.AddStacks:
                        existing.AddStack();
                        return existing.Handle;
                }
            }
        }

        // Transactional apply: record applied modifiers and tags, and rollback on failure
        var appliedModifiers = new List<(AttributeValue, AttributeModifier)>();
        var appliedTags = new List<GameplayTagSO>();

        ActiveGameplayEffect activeEffect = null;

        try
        {
            activeEffect = new ActiveGameplayEffect(effect, spec.InstigatorASC, spec);

            // 注册属性修改器（使用 GetMagnitude 解析值）
            if (Attributes != null)
            {
                for (int i = 0; i < effect.modifiers.Count; i++)
                {
                    var mod = effect.modifiers[i];
                    var attrValue = Attributes.GetAttributeValue(mod.attribute);
                    if (attrValue == null)
                    {
                        // Treat missing target attribute as an application failure to keep transactional semantics
                        throw new System.Exception($"ApplyDurationEffect: target AttributeValue for {mod.attribute} is missing");
                    }

                    float magnitude = spec.GetMagnitude(i, this);
                    var modifier = new AttributeModifier(mod.modifierType, magnitude);
                    attrValue.AddModifier(modifier);
                    appliedModifiers.Add((attrValue, modifier));
                    activeEffect.RegisteredModifiers.Add(modifier);
                }
            } else if (effect.modifiers.Count > 0)
            {
                // No AttributeSet on target but modifiers exist -> fail
                throw new System.Exception("ApplyDurationEffect: Attributes is null but effect has modifiers");
            }

            // 授予标签
            if (tagComponent != null)
            {
                foreach (var tag in effect.grantedTags)
                {
                    tagComponent.AddTag(tag);
                    appliedTags.Add(tag);
                }
            }

            // 设置周期 Tick 回调（使用 Spec 快照值），保护回调执行
            if (effect.period > 0f)
            {
                activeEffect.OnPeriodicTick = () =>
                {
                    try { ExecutePeriodicTick(activeEffect); }
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

            return activeEffect.Handle;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"ApplyDurationEffect: exception while applying duration effect {effect.name}: {e}");
            // Rollback modifiers
            foreach (var (attrVal, modifier) in appliedModifiers)
            {
                try { attrVal.RemoveModifier(modifier); }
                catch (System.Exception ex) { Debug.LogWarning($"Rollback RemoveModifier exception: {ex}"); }
            }
            // Rollback tags
            foreach (var tag in appliedTags)
            {
                try { tagComponent?.RemoveTag(tag); }
                catch (System.Exception ex) { Debug.LogWarning($"Rollback RemoveTag exception: {ex}"); }
            }

            return -1;
        }
    }

    /// <summary>
    /// 周期 Tick 执行：使用 Spec 快照属性计算伤害
    /// </summary>
    private void ExecutePeriodicTick(ActiveGameplayEffect activeEffect)
    {
        var effect = activeEffect.EffectData;
        if (Attributes == null) return;

        if (effect.damage > 0f || effect.poiseDamage > 0f)
        {
            // 使用 Spec 快照属性值（若 Spec 存在），否则 fallback 到实时值
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
                RemoveActiveEffectInternal(activeEffect);
            }
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
                            // AttributeModifier is a struct (value type) — remove without null check
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

        // 移除授予标签 (使用 TagComponent 安全移除)
        if (tagComponent != null)
        {
            foreach (var tag in activeEffect.EffectData.grantedTags)
            {
                try { tagComponent.RemoveTag(tag); }
                catch (System.Exception e) { Debug.LogWarning($"RemoveActiveEffectInternal: RemoveTag exception: {e}"); }
            }
        }

        // Cue 分发：移除时调用 RemoveCue
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
