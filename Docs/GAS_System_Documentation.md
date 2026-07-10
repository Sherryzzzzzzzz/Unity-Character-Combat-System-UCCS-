# Unity GAS（Gameplay Ability System）实现文档

> 本项目是对 Unreal Engine GAS（Gameplay Ability System）在 Unity 中的完整复刻实现，包含核心属性系统、能力系统、效果系统、标签系统、任务系统，以及可视化技能编辑器。

---

## 目录

1. [架构概览](#架构概览)
2. [核心系统详解](#核心系统详解)
   - [属性系统 (Attribute System)](#属性系统-attribute-system)
   - [能力系统 (Ability System)](#能力系统-ability-system)
   - [效果系统 (Effect System)](#效果系统-effect-system)
   - [标签系统 (Tag System)](#标签系统-tag-system)
   - [任务系统 (Task System)](#任务系统-task-system)
   - [GameplayCue 系统](#gameplaycue-系统)
3. [技能编辑器](#技能编辑器)
   - [时间轴架构](#时间轴架构)
   - [事件系统](#事件系统)
   - [编辑器扩展](#编辑器扩展)
4. [设计模式与最佳实践](#设计模式与最佳实践)
5. [扩展指南](#扩展指南)

---

## 架构概览

### 整体架构图

```
┌─────────────────────────────────────────────────────────────────┐
│                         GASHost (全局管理器)                      │
│                    - 时间缩放控制                                  │
│                    - 集中 Tick 所有 ASC                           │
└─────────────────────────────────────────────────────────────────┘
                                │
                                ▼
┌─────────────────────────────────────────────────────────────────┐
│                  AbilitySystemComponent (核心组件)               │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐          │
│  │  AttributeSet │  │ TagComponent │  │  Abilities   │          │
│  │  (属性管理)   │  │  (标签管理)  │  │  (能力管理)  │          │
│  └──────────────┘  └──────────────┘  └──────────────┘          │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐          │
│  │ActiveEffects │  │ AbilityTasks │  │ GameplayCue  │          │
│  │  (效果管理)  │  │  (任务管理)  │  │  (视觉反馈)  │          │
│  └──────────────┘  └──────────────┘  └──────────────┘          │
└─────────────────────────────────────────────────────────────────┘
                                │
            ┌───────────────────┼───────────────────┐
            ▼                   ▼                   ▼
    ┌──────────────┐    ┌──────────────┐    ┌──────────────┐
    │GameplayEffect│    │GameplayAbility│    │ GameplayCue │
    │   (效果定义) │    │   (能力定义) │    │  (视觉/音效) │
    └──────────────┘    └──────────────┘    └──────────────┘
```

### 核心设计理念

1. **数据驱动**：通过 ScriptableObject 定义能力、效果、标签，实现策划友好配置
2. **组件化设计**：每个子系统独立可测试，通过 ASC 聚合协调
3. **事件驱动**：属性变化、标签变化、效果施加等均通过事件通知
4. **运行时隔离**：运行时实例（Spec、ActiveEffect）与定义资产分离

---

## 核心系统详解

### 属性系统 (Attribute System)

属性系统是 GAS 的基础，负责管理角色的所有数值属性（生命值、攻击力、防御力等）。

#### 核心类

##### `GameplayAttribute` 枚举
定义所有可被 GameplayEffect 修改的属性类型：

```csharp
public enum GameplayAttribute
{
    AttackPower,    // 攻击力
    Defense,        // 防御力
    HealthMax,      // 最大生命值
    PoiseMax,       // 最大韧性
    Health,         // 当前生命值
    Poise           // 当前韧性
}
```

##### `AttributeValue` - 属性值容器

**设计思路**：
- 支持 **基础值** 和 **修改器** 分离
- 使用 **Dirty 标记** 实现惰性计算，避免每帧重算
- 支持三种修改器类型：**加法(Additive)**、**乘法(Multiplicative)**、**覆盖(Override)**
- 内置 **聚合模式(AggregatorMode)** 支持不同的修改器组合策略

```csharp
public class AttributeValue
{
    // 基础值
    [SerializeField] private float _baseValue;

    // 修改器列表
    [NonSerialized] private readonly List<AttributeModifier> _modifiers;

    // 缓存的当前值 + Dirty标记
    [NonSerialized] private float _cachedCurrentValue;
    [NonSerialized] private bool _dirty = true;

    // 聚合模式
    public AggregatorMode Mode = AggregatorMode.Default;

    // 变更回调
    public Action<float, float> OnValueChanged;
    public Func<float, float> OnPreAttributeChange; // 钳制回调
}
```

##### 计算公式

**Default 模式**：
```
CurrentValue = (BaseValue + ΣAdditive) × (1 + ΣMultiplicative)
Override 类型：取最后一个 Override 值
```

**MostPositive 模式**：只取最大正向加法修改器
**MostNegative 模式**：只取最大负向加法修改器

##### `AttributeModifier` - 属性修改器

```csharp
public struct AttributeModifier
{
    public ModifierType type;      // Additive / Multiplicative / Override
    public float value;            // 修改值
    public ActiveGameplayEffect Source;  // 来源效果（用于堆叠感知）
}
```

**Magnitude 计算模式**：

```csharp
public enum MagnitudeCalculation
{
    Static,         // 使用 SO 上的固定值
    AttributeBased, // 从施加者/目标捕获指定属性值
    Custom          // 通过 IMagnitudeCalculation 接口自定义
}
```

##### `AttributeSet` - 属性集合

挂载在角色 GameObject 上，管理所有属性的初始化和运行时访问：

```csharp
public class AttributeSet : MonoBehaviour
{
    // Inspector 配置的初始值
    [SerializeField] private List<AttributeInitEntry> _attributeInitList;

    // 运行时属性字典
    private readonly Dictionary<GameplayAttribute, AttributeValue> _attributes;

    // 特殊属性逻辑
    public float Health;  // 当前生命值
    public float Poise;   // 当前韧性

    // 事件
    public event Action OnDeath;
    public event Action OnPoiseBreak;
    public event Action<GameplayAttribute, float, float> OnAttributeChanged;
}
```

**特殊处理**：
- **Health**：独立管理，支持死亡检测
- **Poise**：韧性系统，支持自动恢复、崩溃检测

---

### 能力系统 (Ability System)

能力系统是 GAS 的核心，负责管理角色可执行的动作（攻击、技能、闪避等）。

#### 核心类

##### `GameplayAbility` - 能力基类

**设计思路**：
- 支持标签驱动的激活条件
- 支持 Cost（资源消耗）和 Cooldown（冷却）
- 支持充能 CD 机制
- 支持中断控制

```csharp
public abstract class GameplayAbility
{
    // 拥有者引用
    protected AbilitySystemComponent OwnerASC;
    protected GameObject Owner;
    protected TagComponent TagComp;

    // 冷却配置
    public float Cooldown = 0f;
    protected GameplayEffect _cooldownEffect;
    protected GameplayTagSO _cooldownTag;

    // 充能 CD
    public int MaxCharges = 1;
    public float ChargeRecoveryTime = 0f;
    private int _remainingCharges;

    // 标签配置
    public List<GameplayTagSO> ActivationRequiredTags;  // 激活所需标签
    public List<GameplayTagSO> ActivationBlockedTags;   // 激活阻止标签
    public List<GameplayTagSO> GrantedTags;             // 激活时授予的标签

    // 资源消耗
    protected GameplayEffect _costEffect;

    // 核心方法
    public bool TryActivate();       // 尝试激活
    protected abstract void Activate(); // 实际激活逻辑
    public virtual void End();       // 结束能力
}
```

##### 激活流程

```
TryActivate()
    │
    ├─► 1. 冷却检查 (IsOnCooldown)
    │       ├─ 充能模式：检查剩余充能数
    │       └─ 标签驱动：检查冷却标签
    │
    ├─► 2. 标签检查
    │       ├─ ActivationRequiredTags: 必须全部拥有
    │       └─ ActivationBlockedTags: 必须全部不拥有
    │
    ├─► 3. Cost 检查 (CheckCost)
    │       └─ 检查资源是否足够
    │
    ├─► 4. 提交 (CommitAbility)
    │       ├─ 扣除 Cost Effect
    │       ├─ 启动冷却
    │       │   ├─ 充能模式：消耗一个充能
    │       │   └─ 标签驱动：施加 CooldownEffect
    │       └─ 授予 GrantedTags
    │
    └─► 5. 执行 (Activate)
            └─ 子类实现具体逻辑
```

##### `GameplayAbilitySO` - 数据资产

通过 ScriptableObject 定义能力配置，支持数据驱动：

```csharp
[CreateAssetMenu(menuName = "GAS-like/GameplayAbility")]
public class GameplayAbilitySO : ScriptableObject
{
    public string abilityName;
    public float cooldown;
    public GameplayEffect cooldownEffect;
    public GameplayTagSO cooldownTag;
    public int maxCharges;
    public float chargeRecoveryTime;
    public List<GameplayTagSO> activationRequiredTags;
    public List<GameplayTagSO> activationBlockedTags;
    public List<GameplayTagSO> grantedTags;
    public GameplayEffect costEffect;
    public List<GameplayEffect> effectsToApply;  // 激活时自动施加的效果

    public virtual GameplayAbility CreateRuntimeAbility();
}
```

##### `DefaultGameplayAbility` - 默认实现

```csharp
public class DefaultGameplayAbility : GameplayAbility
{
    private List<GameplayEffect> _effectsToApply;

    protected override void Activate()
    {
        // 施加关联效果
        foreach (var effect in _effectsToApply)
            OwnerASC.ApplyGameplayEffect(effect, OwnerASC);
    }
}
```

---

### 效果系统 (Effect System)

效果系统负责所有属性修改、Buff/Debuff、伤害治疗的计算和施加。

#### 核心类

##### `GameplayEffect` - 效果定义

```csharp
[CreateAssetMenu(menuName = "GAS-like/GameplayEffect")]
public class GameplayEffect : ScriptableObject
{
    // 效果类型
    public EffectType effectType;  // Custom / Damage / Heal / Buff / Cooldown / Cost

    // 基础数值
    public float damage;
    public float poiseDamage;
    public float damageMultiplier;

    // 持续时间策略
    public DurationPolicy durationPolicy;  // Instant / Duration / Infinite
    public float duration;
    public float period;  // 周期 Tick 间隔

    // 堆叠规则
    public StackingPolicy stackingPolicy;  // None / RefreshDuration / AddStacks
    public int maxStacks;

    // 高级堆叠
    public OverflowPolicy overflowPolicy;  // RejectNew / TriggerOverflowEffect
    public ExpirationPolicy expirationPolicy;  // RemoveAllStacks / RemoveOneStack
    public DurationRefreshPolicy refreshPolicy;  // ResetOnRefresh / ExtendOnRefresh
    public GameplayEffect overflowEffect;

    // 标签
    public List<GameplayTagSO> grantedTags;
    public List<GameplayTagSO> applicationRequiredTags;
    public List<GameplayTagSO> applicationBlockedTags;

    // 属性修改器
    public List<EffectAttributeModifier> modifiers;

    // 执行计算
    public GameplayEffectExecutionCalculation executionCalculation;

    // GameplayCue
    public GameplayTagSO cueTag;
}
```

##### `GameplayEffectSpec` - 运行时效果规格

**设计思路**：
- 持有效果数据引用
- 自动捕获施加者属性快照
- 支持动态 Magnitude 覆盖
- 提供生命周期虚方法供子类扩展

```csharp
public class GameplayEffectSpec
{
    public GameplayEffect EffectData { get; }
    public AbilitySystemComponent InstigatorASC { get; }

    // 施加者属性快照
    public Dictionary<GameplayAttribute, float> CapturedAttackerAttributes { get; }

    // 动态 Magnitude 覆盖
    private readonly Dictionary<int, float> _magnitudeOverrides;

    // 生命周期虚方法
    public virtual void OnInitialApply(AbilitySystemComponent targetASC);
    public virtual void OnPeriodicExecute(AbilitySystemComponent targetASC);
    public virtual void OnComplete(AbilitySystemComponent targetASC);
    public virtual void OnRefresh();
    public virtual void OnOverflow(AbilitySystemComponent targetASC);
}
```

##### `ActiveGameplayEffect` - 活跃效果实例

```csharp
public class ActiveGameplayEffect
{
    public int Handle { get; }  // 唯一标识
    public GameplayEffect EffectData { get; }
    public GameplayEffectSpec Spec { get; }

    public float TimeRemaining { get; private set; }
    public int CurrentStacks { get; private set; }
    public bool IsExpired => ...;

    // 已注册的修改器引用（用于清理）
    public List<AttributeModifier> RegisteredModifiers { get; }

    // 周期 Tick 回调
    public Action OnPeriodicTick;
}
```

##### `EffectSpecFactory` - 效果规格工厂

根据 `GameplayEffect.effectType` 自动创建对应的 Spec 子类：

```csharp
public static class EffectSpecFactory
{
    public static GameplayEffectSpec CreateSpec(GameplayEffect effectData, AbilitySystemComponent instigator)
    {
        switch (effectData.effectType)
        {
            case EffectType.Damage: return new DamageEffectSpec(effectData, instigator);
            case EffectType.Heal: return new HealEffectSpec(effectData, instigator);
            case EffectType.Buff: return new BuffEffectSpec(effectData, instigator);
            case EffectType.Cost: return new CostEffectSpec(effectData, instigator);
            default: return new GameplayEffectSpec(effectData, instigator);
        }
    }
}
```

##### `GameplayEffectExecutionCalculation` - 执行计算

用于自定义伤害/治疗计算，替代硬编码公式：

```csharp
public abstract class GameplayEffectExecutionCalculation : ScriptableObject
{
    public abstract void Execute(
        AbilitySystemComponent instigatorASC,
        AbilitySystemComponent targetASC,
        GameplayEffectSpec spec,
        ref EffectExecutionOutput output);
}
```

#### 效果施加流程

```
ApplyEffectSpec(spec)
    │
    ├─► Application Tags 检查
    │       ├─ applicationRequiredTags: 目标必须拥有
    │       └─ applicationBlockedTags: 目标不能拥有
    │
    ├─► DurationPolicy 分发
    │       │
    │       ├─► Instant:
    │       │       ├─ 执行 ExecutionCalculation（如有）
    │       │       ├─ 直接修改 BaseValue
    │       │       └─ 触发 GameplayCue.OnExecute
    │       │
    │       └─► Duration / Infinite:
    │               ├─ 堆叠检查
    │               │   ├─ None: 拒绝重复施加
    │               │   ├─ RefreshDuration: 刷新时间
    │               │   └─ AddStacks: 增加层数
    │               │
    │               ├─ 创建 ActiveGameplayEffect
    │               ├─ 注册 AttributeModifier
    │               ├─ 授予 GameplayTags
    │               └─ 触发 GameplayCue.OnAdd
    │
    └─► 返回 Effect Handle（用于移除）
```

---

### 标签系统 (Tag System)

标签系统是 GAS 的状态管理核心，用于控制能力激活、效果施加、连招触发等。

#### 核心类

##### `GameplayTagSO` - 标签定义

```csharp
[CreateAssetMenu(menuName = "GAS-like/GameplayTag")]
public class GameplayTagSO : ScriptableObject
{
    public GameplayTagSO parentTag;  // 父标签（层级关系）
}
```

##### `TagComponent` - 标签组件

**设计思路**：
- **引用计数**：支持多来源添加/移除
- **瞬态标签**：单帧有效，用于输入事件
- **缓存标签**：带时间窗口的标签
- **层级匹配**：支持父标签匹配所有子标签

```csharp
public class TagComponent : MonoBehaviour
{
    // 引用计数标签
    private readonly Dictionary<GameplayTagSO, int> _tagRefCounts;

    // 瞬态标签（每帧清理）
    private HashSet<GameplayTagSO> transientTags;

    // 缓存标签（带时间窗口）
    private readonly List<CachedTag> cachedTags;
    private const float CACHE_DURATION = 0.25f;

    // 标签添加事件
    public Action<GameplayTagSO> OnTagAdded;
}
```

##### 核心方法

```csharp
// 永久标签（引用计数）
void AddTag(GameplayTagSO tag);     // 添加（增加计数）
void RemoveTag(GameplayTagSO tag);  // 移除（减少计数）

// 瞬态标签（单帧有效）
void AddTransientTag(GameplayTagSO tag);

// 消费标签
bool ConsumeTag(GameplayTagSO tag); // 消费并移除

// 查询
bool HasTag(GameplayTagSO tag);           // 精确匹配
bool HasTagOrChild(GameplayTagSO tag);    // 层级匹配
```

##### 标签层级匹配

```
State
├─ State.Attacking
│   ├─ State.Attacking.Light1
│   └─ State.Attacking.Light2
└─ State.Guarding

// HasTagOrChild(State.Attacking) 会匹配：
// - State.Attacking
// - State.Attacking.Light1
// - State.Attacking.Light2
```

---

### 任务系统 (Task System)

任务系统用于管理能力的异步子任务（播放动画、等待延迟、搜索目标等）。

#### 核心类

##### `AbilityTask` - 任务基类

```csharp
public abstract class AbilityTask
{
    public GameplayAbility OwnerAbility { get; private set; }
    public AbilitySystemComponent OwnerASC { get; private set; }

    public bool IsActive { get; protected set; }
    public bool IsFinished { get; protected set; }

    public event Action OnTaskCompleted;
    public event Action OnTaskCancelled;

    public virtual void Activate();
    public virtual void Tick(float deltaTime);
    public virtual void Cancel();
    protected virtual void OnDestroy();
    protected void Complete();
}
```

##### 内置任务类型

| 任务类型 | 文件 | 用途 |
|---------|------|-----|
| `WaitDelayTask` | WaitDelayTask.cs | 等待指定时间后完成 |
| `PlayMontageAndWaitTask` | PlayMontageAndWaitTask.cs | 播放动画并等待完成 |
| `SearchTargetTask` | SearchTargetTask.cs | 搜索目标并填充 TargetData |
| `EndAbilityTask` | EndAbilityTask.cs | 结束能力 |

##### 任务生命周期

```
Ability 激活
    │
    ├─► 创建 Task
    │       └─ ASC.RegisterTask(abilityHandle, task)
    │
    ├─► Task.Activate()
    │       └─ IsActive = true
    │
    ├─► ASC.TickActiveTasks()
    │       └─ task.Tick(deltaTime)
    │
    ├─► Task.Complete() 或 Task.Cancel()
    │       ├─ OnDestroy()
    │       └─ 触发 OnTaskCompleted / OnTaskCancelled
    │
    └─► Ability End
            └─ ASC.CancelTasksForAbility(abilityHandle)
```

---

### GameplayCue 系统

GameplayCue 系统用于解耦视觉效果/音效与游戏逻辑。

#### 核心接口

```csharp
public interface IGameplayCue
{
    void OnExecute(GameObject target, GameplayEffectSpec spec);  // Instant 效果
    void OnAdd(GameObject target, GameplayEffectSpec spec);      // Duration 效果施加
    void OnRemove(GameObject target);                            // Duration 效果移除
}
```

#### 内置 Cue 类型

| Cue 类型 | 文件 | 用途 |
|---------|------|-----|
| `SoundCue` | SoundCue.cs | 音效播放 |
| `ParticleCue` | ParticleCue.cs | 粒子效果 |
| `FloatingTextCue` | FloatingTextCue.cs | 伤害数字显示 |

#### GameplayCueManager

单例管理器，负责 Cue 的注册和分发：

```csharp
public class GameplayCueManager : MonoBehaviour
{
    private readonly Dictionary<GameplayTagSO, IGameplayCue> _cueRegistry;

    public void RegisterCue(GameplayTagSO tag, IGameplayCue cue);
    public void ExecuteCue(GameplayTagSO tag, GameObject target, GameplayEffectSpec spec);
    public void AddCue(GameplayTagSO tag, GameObject target, GameplayEffectSpec spec);
    public void RemoveCue(GameplayTagSO tag, GameObject target);
}
```

---

## 技能编辑器

技能编辑器是一个基于时间轴的可视化工具，用于设计技能的帧精确事件序列。

### 时间轴架构

#### 核心数据结构

##### `SkillTimelineAsset` - 技能时间轴资产

```csharp
[CreateAssetMenu(menuName = "Skill/Timeline Asset")]
public class SkillTimelineAsset : ScriptableObject
{
    public AnimationClip animationClip;
    public List<TimelineTrackData> tracks;
}
```

##### `TimelineTrackData` - 时间轴轨道

```csharp
public class TimelineTrackData
{
    public string name;
    public TimelineEventType type;
    public List<TimelineEventBase> events;
}
```

##### `TimelineEventBase` - 时间轴事件基类

```csharp
public abstract class TimelineEventBase
{
    public int startFrame;
    public int endFrame;

    public abstract TimelineEventType Type { get; }
    public abstract string GetSummary();
    public abstract TimelineEventBase Clone();
}
```

### 事件系统

#### 事件类型

| 事件类型 | 类名 | 用途 |
|---------|------|-----|
| `Attack` | AttackEvent | 攻击检测、伤害施加 |
| `HitBox` | HitBoxEvent | HitBox 启用/禁用 |
| `Combo` | ComboEvent | 连招窗口定义 |
| `Effect` | EffectEvent | 播放特效 |
| `Sound` | SoundEvent | 播放音效 |
| `Buff` | BuffEvent | Buff 施加 |
| `Loop` | LoopEvent | 循环控制 |
| `Cancel` | CancelEvent | 取消窗口定义 |
| `GASEffect` | GameplayEffectEvent | GAS 效果施加 |
| `TargetSearch` | TargetSearchEvent | 目标搜索 |
| `Cue` | CueEvent | GameplayCue 触发 |
| `CooldownTrigger` | CooldownEvent | 冷却触发 |

#### 运行时接口

```csharp
public interface ITimelineEventRuntime
{
    void OnStart(GameObject owner);
    void OnEnd(GameObject owner);
}
```

#### 关键事件详解

##### AttackEvent - 攻击事件

```csharp
public class AttackEvent : TimelineEventBase, ITimelineEventRuntime
{
    public string hitBoxName;
    public AttackData attackData;

    // 目标系统模式
    public bool useTargetSystem = false;

    // 攻击形状
    public AttackShape shape;  // WeaponCollider / Sphere / Capsule / Cone

    void OnStart(GameObject owner)
    {
        // 1. 查找 HitBox
        // 2. 启用碰撞体
        // 3. 初始化 MeleeWeapon
        // 4. 执行攻击检测
    }

    void OnEnd(GameObject owner)
    {
        // 禁用碰撞体，清理资源
    }
}
```

##### ComboEvent - 连招事件

```csharp
public class ComboEvent : TimelineEventBase, ITimelineEventRuntime
{
    public GameplayTagSO RequiredTag;    // 触发所需的标签
    public SkillTimelineAsset nextSkill; // 下一个技能

    public enum ComboMode { Normal_Cacheable, Strict_Immediate }
    public ComboMode comboMode;

    // Normal_Cacheable: 允许提前输入，使用标签缓存
    // Strict_Immediate: 必须在窗口期内精确输入
}
```

##### LoopEvent - 循环事件

```csharp
public class LoopEvent : TimelineEventBase, ITimelineEventRuntime
{
    public List<BranchCondition> breakConditions;

    // 跳出条件类型
    public enum ConditionType
    {
        IsGrounded,
        IsFalling,
        InputWasPressed,
        InputIsPressed,
        InputWasReleased,
        HasInputTag
    }
}
```

### 编辑器扩展

#### SkillEditorWindow

主编辑器窗口，提供：
- 时间轴可视化
- 轨道管理
- 事件编辑
- 动画预览
- 帧精确编辑

#### EventFactory 模式

每个事件类型对应一个 Factory 类，负责：
- 创建事件实例
- 生成 Inspector UI

```csharp
public interface ITimelineEventFactory
{
    TimelineEventType Type { get; }
    TimelineEventBase Create();
    VisualElement CreateInspector(TimelineEventBase evt);
}
```

#### 自动注册

通过 `[InitializeOnLoad]` 自动注册所有 Factory：

```csharp
[InitializeOnLoad]
public static class TimelineEventFactoryBootstrap
{
    static TimelineEventFactoryBootstrap()
    {
        EventFactoryRegistry.Register(new AttackEventFactory());
        EventFactoryRegistry.Register(new ComboEventFactory());
        // ... 其他 Factory
    }
}
```

---

## 设计模式与最佳实践

### 使用的设计模式

| 模式 | 应用场景 |
|-----|---------|
| **组件模式** | ASC 聚合 AttributeSet、TagComponent 等子系统 |
| **工厂模式** | EffectSpecFactory、EventFactoryRegistry |
| **策略模式** | AggregatorMode、DurationPolicy、StackingPolicy |
| **观察者模式** | OnValueChanged、OnAttributeChanged、OnTagAdded |
| **命令模式** | TimelineEvent 的 OnStart/OnEnd |
| **模板方法** | GameplayAbility.Activate、GameplayEffectSpec 生命周期 |

### 最佳实践

1. **效果设计**
   - Instant 效果用于直接伤害/治疗
   - Duration 效果用于 Buff/Debuff
   - 使用 ExecutionCalculation 替代硬编码公式

2. **能力设计**
   - 使用 GameplayAbilitySO 数据驱动
   - 复杂逻辑通过 AbilityTask 分解
   - 使用标签控制激活条件

3. **标签设计**
   - 建立清晰的标签层级结构
   - 状态标签使用永久标签
   - 输入事件使用瞬态标签

4. **性能优化**
   - 属性使用 Dirty 标记惰性计算
   - 技能事件按帧索引预构建
   - GASHost 集中 Tick 避免多次 Update

---

## 扩展指南

### 添加新属性

1. 在 `GameplayAttribute` 枚举中添加新属性
2. 在 `AttributeSet._attributeInitList` 中配置默认值
3. 在 `GameplayEffectSpec` 构造函数中添加快照捕获

### 添加新效果类型

1. 创建继承 `GameplayEffectSpec` 的子类
2. 实现生命周期虚方法
3. 在 `EffectSpecFactory.CreateSpec` 中添加分支
4. 在 `EffectType` 枚举中添加新类型

### 添加新事件类型

1. 创建继承 `TimelineEventBase` 的类
2. 实现 `ITimelineEventRuntime` 接口
3. 创建对应的 Factory 类
4. 在 `TimelineEventFactoryBootstrap` 中注册
5. 在 `TimelineEventType` 枚举中添加新类型

### 添加新 GameplayCue

1. 创建实现 `IGameplayCue` 的 MonoBehaviour
2. 在 `GameplayCueManager` 中注册到对应标签
3. 在 `GameplayEffect` 中配置 cueTag

---

## 文件结构

```
Assets/Scripts/GASSystem/
├── AbilitySystemComponent.cs   # 核心组件
├── GameplayAbility.cs          # 能力基类
├── AttributeSet.cs             # 属性集合
├── AttributeValue.cs           # 属性值容器
├── AttributeModifier.cs        # 属性修改器
├── ActiveGameplayEffect.cs     # 活跃效果
├── GameplayEffectSpec.cs       # 效果规格
├── TagComponent.cs             # 标签组件
├── GameplayCueManager.cs       # Cue 管理器
├── IGameplayCue.cs             # Cue 接口
├── FormulaEvaluator.cs         # 公式计算
├── Core/
│   ├── GASHost.cs              # 全局管理器
│   └── AbilityExecutionContext.cs
├── Effect/
│   ├── EffectSpecFactory.cs
│   ├── GameplayEffectExecutionCalculation.cs
│   ├── DamageEffectSpec.cs
│   ├── HealEffectSpec.cs
│   ├── BuffEffectSpec.cs
│   ├── CooldownEffectSpec.cs
│   └── CostEffectSpec.cs
├── Task/
│   ├── AbilityTask.cs
│   ├── WaitDelayTask.cs
│   ├── PlayMontageAndWaitTask.cs
│   ├── SearchTargetTask.cs
│   └── EndAbilityTask.cs
├── Target/
│   ├── TargetData.cs
│   └── HitBoxTargetDataAdapter.cs
└── Cue/
    ├── SoundCue.cs
    ├── ParticleCue.cs
    └── FloatingTextCue.cs

Assets/Scripts/ScriptsObject/
├── GameplayEffect.cs           # 效果定义
├── GameplayAbilitySO.cs        # 能力数据资产
├── AttackData.cs               # 攻击数据
├── BuffSO.cs                   # Buff 定义（已废弃）
└── SkillTimelineAsset.cs       # 技能时间轴

Assets/Scripts/EventFactory/
├── TimelineEventBase.cs        # 事件基类
└── Events/
    ├── AttackEvent.cs
    ├── ComboEvent.cs
    ├── LoopEvent.cs
    ├── GameplayEffectEvent.cs
    └── ...

Assets/Editor/Timeline/
├── SkillEditorWindow.cs        # 编辑器主窗口
├── EventInspectorPanel.cs
├── TimelineRulerRenderer.cs
├── TimelineTrackRenderer.cs
└── ...
```

---

## 参考资料

- [Unreal Engine GAS Documentation](https://docs.unrealengine.com/5.0/en-US/gameplay-ability-system-for-unreal-engine/)
- [Tranek's GAS Documentation](https://github.com/tranek/GASDocumentation)

---

*文档生成日期：2026-04-06*
*项目：Unity Character Combat System (UCCS)* v  