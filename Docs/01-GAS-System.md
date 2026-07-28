# 01 — GAS (Gameplay Ability System) 架构详解

## 目录

1. [系统总览](#系统总览)
2. [核心类详解](#核心类详解)
3. [属性系统 (Attribute)](#属性系统-attribute)
4. [能力系统 (Ability)](#能力系统-ability)
5. [效果系统 (Effect)](#效果系统-effect)
6. [标签系统 (Tag)](#标签系统-tag)
7. [GameplayCue 反馈系统](#gameplaycue-反馈系统)
8. [AbilityTask 异步任务系统](#abilitytask-异步任务系统)
9. [Event 事件系统](#event-事件系统)
10. [GASHost 全局调度](#gashost-全局调度)
11. [执行流程示例](#执行流程示例)

---

## 系统总览

```
┌──────────────────────────────────────────────────────────┐
│                      GASHost (全局)                       │
│   - 集中 Tick 所有 ASC                                    │
│   - 全局 TimeScale 控制 (慢动作/暂停)                      │
└────────────────────────┬─────────────────────────────────┘
                         │ 注册/注销 + 驱动 Tick
                         ▼
┌──────────────────────────────────────────────────────────┐
│                AbilitySystemComponent (ASC)               │
│  ┌──────────┐  ┌──────────┐  ┌──────────┐  ┌──────────┐ │
│  │ Ability  │  │  Effect  │  │   Tag    │  │   Task   │ │
│  │ Manager  │  │ Manager  │  │ Manager  │  │ Manager  │ │
│  └──────────┘  └──────────┘  └──────────┘  └──────────┘ │
│       │             │             │             │         │
│  ┌────▼─────┐ ┌────▼──────┐ ┌───▼───────┐ ┌──▼───────┐  │
│  │Spec List │ │Active GEs │ │TagComp    │ │Task Pool │  │
│  │Give/Cancel│ │Apply/Tick │ │RefCount   │ │Register/ │  │
│  │Activate  │ │Remove     │ │Hierarchy  │ │Tick/Cancel│  │
│  └──────────┘ └───────────┘ └───────────┘ └──────────┘  │
│                                                          │
│  ┌──────────────────────────────────────────────────┐    │
│  │              AttributeSet                         │    │
│  │  Health │ Poise │ Stamina │ AttackPower │ ...    │    │
│  │         └───────┴─────────┴─────────────┘        │    │
│  │          每个 Attribute 由 AttributeValue 管理     │    │
│  │          BaseValue + Modifiers → CurrentValue     │    │
│  └──────────────────────────────────────────────────┘    │
└──────────────────────────────────────────────────────────┘
```

---

## 核心类详解

### 1. AbilitySystemComponent (ASC)

**文件**: `Assets/Scripts/GASSystem/AbilitySystemComponent.cs` (1284行)

ASC 是挂载在每个角色 GameObject 上的核心组件，是所有 GAS 子系统的中枢：

```csharp
public class AbilitySystemComponent : MonoBehaviour
{
    public AttributeSet Attributes;                          // 属性集引用
    private List<GameplayAbilitySpec> _activatableAbilities; // Spec-based 能力列表
    private List<ActiveGameplayEffect> _activeEffects;       // 活跃效果列表
    private Dictionary<int, List<AbilityTask>> _abilityTasks; // 能力任务管理
}
```

**核心职责**:
- **能力管理**: GiveAbility / TryActivateAbilityByHandle / TryActivateAbilitiesByTag / CancelAbility
- **效果管理**: ApplyEffectSpec / RemoveActiveEffect / TickActiveEffects
- **标签管理**: AddLooseGameplayTag / GetTagCount / RegisterGameplayTagEvent
- **Context创建**: MakeEffectContext / MakeOutgoingSpec
- **事件分发**: HandleGameplayEvent → 触发匹配的 Ability
- **Task 管理**: RegisterTask / CancelTasksForAbility / TickActiveTasks

**双重能力管理**:
- **旧版 string-key API**: `ActivateAbility("key")` — 向后兼容
- **新版 Spec API**: `GiveAbility(ability)` → `TryActivateAbilityByHandle(handle)` — 对齐 UE5

### 2. GameplayAbility (能力基类)

**文件**: `Assets/Scripts/GASSystem/GameplayAbility.cs` (551行)

完整对齐 UE5 `UGameplayAbility` 的生命周期：

```
PreActivate → CommitCost → CommitCooldown → Activate → [Running...] → EndAbility
```

**核心属性**:
```csharp
public abstract class GameplayAbility
{
    // 元数据
    public InstancingPolicy AbilityInstancingPolicy;   // InstancedPerActor / InstancedPerExecution
    public NetExecutionPolicy AbilityNetExecutionPolicy; // LocalOnly / ServerOnly 等
    public List<GameplayTagSO> AbilityTags;            // AssetTag
    public List<GameplayTagSO> CancelAbilitiesWithTag; // 激活时取消其他能力
    public List<GameplayTagSO> BlockAbilitiesWithTag;  // 激活时阻塞其他能力
    public List<GameplayTagSO> ActivationOwnedTags;    // 激活时授予标签

    // CD 系统 (3种策略)
    public float Cooldown;                             // 简单时间戳 CD
    protected GameplayEffect _cooldownEffect;          // 标签驱动 CD (GE)
    public int MaxCharges;                             // 充能式 CD
}
```

**冷却系统 (3种模式)**:
1. **简单 CD**: `Time.time < lastCastTime + Cooldown`
2. **标签驱动 CD**: 施加一个 Duration 类型的 CooldownEffect，授予 cooldownTag
3. **充能式 CD**: MaxCharges > 1 时启用，支持 ChargeRecoveryTime

**生命周期方法对齐 UE5**:
| 方法 | 对应 UE5 | 说明 |
|------|----------|------|
| `CanActivateAbility(actorInfo)` | ✅ | 检查能否激活 (不含副作用) |
| `PreActivate(handle, actorInfo)` | ✅ | 预激活钩子 |
| `ActivateAbility(handle, actorInfo)` | ✅ | 实际激活入口 |
| `CommitAbility(handle, actorInfo)` | ✅ | 提交 Cost + Cooldown |
| `CommitCost(handle, actorInfo)` | ✅ | 只提交消耗 |
| `CommitCooldown(handle, actorInfo)` | ✅ | 只提交冷却 |
| `EndAbility(handle, actorInfo, info, cancelled)` | ✅ | 结束能力 |
| `ShouldAbilityRespondToEvent(tag, payload)` | ✅ | 事件响应判断 |
| `GetAbilityLevel()` | ✅ | 获取等级 |

### 3. GameplayAbilitySO (能力数据资产)

**文件**: `Assets/Scripts/ScriptsObject/GameplayAbilitySO.cs` (97行)

ScriptableObject 数据驱动，策划可在 Inspector 中配置：
- abilityName / cooldown / maxCharges / chargeRecoveryTime
- abilityTags / activationRequiredTags / activationBlockedTags
- grantedTags / cancelAbilitiesWithTag / blockAbilitiesWithTag / activationOwnedTags
- costEffect (资源消耗)
- cooldownEffect / cooldownTag (标签驱动CD)
- effectsToApply (激活时施加的效果列表)

通过 `CreateRuntimeAbility()` 工厂方法创建 `DefaultGameplayAbility` 运行时实例。

### 4. GameplayAbilitySpec (能力运行时描述符)

**文件**: `Assets/Scripts/GASSystem/Core/GameplayAbilitySpec.cs` (178行)

对应 UE5 `FGameplayAbilitySpec`，存储能力的运行时元数据：
```csharp
public class GameplayAbilitySpec
{
    public int Handle;                              // 全局唯一Handle
    public GameplayAbility Ability;                 // CDO引用
    public int Level;                               // 等级
    public int InputID;                             // 输入绑定
    public object SourceObject;                     // 来源（GE/ Actor）
    public int ActiveCount;                         // 活跃实例数
    public bool InputPressed;                       // 输入状态
    public List<GameplayAbility> ReplicatedInstances; // 网络实例
    public GameplayAbilityActivationInfo ActivationInfo; // 激活信息
}
```

**InstancingPolicy 实例化策略**:
- `InstancedPerActor`: 每个 Actor 只创建一份，重复激活复用
- `InstancedPerExecution`: 每次激活创建新实例
- `NonInstanced`: 不实例化，直接调用 CDO (预留)

---

## 属性系统 (Attribute)

### AttributeValue

**文件**: `Assets/Scripts/GASSystem/AttributeValue.cs` (289行)

属性值的核心容器，支持 Dirty 标记按需重算和 StackCount 感知：

```
BaseValue + Σ(Additive × StackCount) × (1 + Σ(Multiplicative × StackCount))
→ Override 覆盖(取最后一个)
→ OnPreAttributeChange 钳制
→ CurrentValue
```

**聚合模式 (AggregatorMode)**:
| 模式 | 行为 | 应用场景 |
|------|------|---------|
| `Default` | Base + ΣAdd × (1 + ΣMult)，Override 取最后 | 通用属性 |
| `MostPositive` | 只取最大正向 Additive modifier | 增益取最大 |
| `MostNegative` | 只取最大负向 Additive modifier | 减益取最大 |

**StackCount 感知**: modifier 的有效值 = `value × source.CurrentStacks`，堆叠层数变化时自动触发 `SetDirty()` 重算。

### GameplayAttribute 枚举

```csharp
public enum GameplayAttribute
{
    AttackPower,  // 攻击力
    Defense,      // 防御力
    HealthMax,    // 最大生命
    PoiseMax,     // 最大韧性
    Health,       // 当前生命
    Poise,        // 当前韧性
    StaminaMax,   // 最大体力
    Stamina       // 当前体力
}
```

### AttributeSet

**文件**: `Assets/Scripts/GASSystem/AttributeSet.cs` (348行)

挂载在角色上的属性集组件，管理所有 AttributeValue 实例：
- Inspector 可配置初始值列表
- Health/Poise/Stamina 带有自然恢复逻辑
- 支持动态注册属性 (`RegisterAttribute`)
- Health: 扣血 → 死亡检查 → OnDeath 事件
- Poise: 削韧 → 破韧检查 → OnPoiseBreak 事件
- Stamina: 消耗 → 延迟恢复

### AttributeModifier

**文件**: `Assets/Scripts/GASSystem/AttributeModifier.cs` (102行)

定义了完整的修改器系统：

```csharp
public enum ModifierType { Additive, Multiplicative, Override }
public enum MagnitudeCalculation { Static, AttributeBased, Custom, SetByCaller }
public enum CaptureSource { Attacker, Target }
```

**Magnitude 计算 (4种模式)**:
1. **Static**: 使用 SO 上的固定值
2. **AttributeBased**: 从施加者/目标快照指定属性值
3. **Custom**: 通过 `IMagnitudeCalculation` 接口自定义计算
4. **SetByCaller**: 调用方通过 Tag 在运行时指定值

---

## 能力系统 (Ability)

### 激活流程 (Spec API)

```
1. GiveAbility(ability, level, inputID)
   └─ 创建 GameplayAbilitySpec → 加入 _activatableAbilities

2. TryActivateAbilityByHandle(handle) / TryActivateAbilitiesByTag(tag)
   ├─ 检查 BlockAbilitiesWithTag (被其他活跃能力阻塞)
   ├─ 检查 CanActivateAbility (冷却/消耗/标签)
   ├─ InstancingPolicy 决定实例策略
   ├─ PreActivate
   ├─ CancelAbilitiesWithTag (取消冲突能力)
   ├─ 授予 ActivationOwnedTags
   ├─ ActivateAbility(handle, actorInfo)
   │  ├─ CommitAbility → CommitCost + CommitCooldown
   │  ├─ 授予 GrantedTags
   │  └─ Activate() 子类实现
   └─ OnAbilityActivated 事件
```

### 资源消耗 (Cost)

通过 `_costEffect` (GameplayEffect, Instant 类型) 实现：
- `CheckCost()`: 预检查属性是否足够
- `CommitCost()`: 原子扣除

### 冷却管理 (Cooldown)

3种策略，优先级：**充能 > 标签驱动 > 简单时间戳**

**标签驱动 CD 的巧妙设计**:
```
CommitCooldown → 创建 CooldownEffectSpec →
施加 Duration GE (cooldownEffect) + 授予 cooldownTag →
IsOnCooldown() 检查 TagComp.HasTag(cooldownTag) →
GE 到期自动移除 tag → CD 结束
```
这意味着 CD 完全融入 GE 生命周期，支持 Immunity/RemoveEffects 等高级特性。

---

## 效果系统 (Effect)

### GameplayEffect (效果数据资产)

**文件**: `Assets/Scripts/ScriptsObject/GameplayEffect.cs` (131行)

最复杂的 ScriptableObject，包含：

```
┌─ 效果类型: Custom / Damage / Heal / Buff / Cooldown / Cost
├─ 数值: damage / poiseDamage / damageMultiplier
├─ 持续时间: DurationPolicy (Instant / Duration / Infinite)
├─ 周期Tick: period (0 = 无周期)
├─ 堆叠: StackingPolicy (None / RefreshDuration / AddStacks)
│   ├─ OverflowPolicy (RejectNew / TriggerOverflowEffect)
│   ├─ ExpirationPolicy (RemoveAllStacks / RemoveOneStack)
│   └─ DurationRefreshPolicy (ResetOnRefresh / ExtendOnRefresh)
├─ 标签条件: applicationRequiredTags / applicationBlockedTags
├─ 标签授予: grantedTags
├─ 属性修改器: modifiers (EffectAttributeModifier 列表)
├─ 执行计算: executionCalculation (GameplayEffectExecutionCalculation)
├─ 免疫查询: applicationImmunityQueries (GameplayTagQuery 列表)
├─ 移除GE: removeGameplayEffectsWithTags
├─ 授予Ability: grantedAbilities (GameplayAbilitySpecDef 列表)
├─ 过期效果: expirationEffects / prematureExpirationEffects
├─ 技能绑定: cancelOnAbilityEnd
└─ 视觉反馈: cueTag
```

### 效果类型与 Spec 子类

通过 `EffectType` 枚举 + `EffectSpecFactory` 工厂模式：

| EffectType | Spec 子类 | 说明 |
|-----------|----------|------|
| `Custom` | `GameplayEffectSpec` | 默认基类行为 |
| `Damage` | `DamageEffectSpec` | 伤害计算 |
| `Heal` | `HealEffectSpec` | 治疗计算 |
| `Buff` | `BuffEffectSpec` | 属性修改器管理 |
| `Cooldown` | `CooldownEffectSpec` | CD标签管理 |
| `Cost` | `CostEffectSpec` | 消耗检查扣除 |

### 效果施加完整流程

```
ApplyEffectSpec(spec)
├─ Application Tags 检查 (requiredTags / blockedTags)
├─ Immunity 查询 (applicationImmunityQueries)
├─ RemoveGameplayEffectsWithTags
├─ ↓ 按 DurationPolicy 分支
│
├─ Instant:
│  ├─ ExecutionCalculation（优先）
│  └─ Fallback 硬编码公式
│  └─ Instant 属性修改器直接改 BaseValue
│  └─ NotifyCueExecute
│
├─ Duration / Infinite:
│  ├─ 堆叠检查 (None→拒 / RefreshDuration→刷新 / AddStacks→叠加)
│  ├─ 事务性施加 (Transactional Apply)
│  │  ├─ 创建 ActiveGameplayEffect
│  │  ├─ 注册属性 Modifiers
│  │  ├─ 授予 Tags
│  │  └─ 失败时完整回滚 (rollback modifiers + tags)
│  ├─ 周期 Tick 注册
│  ├─ Grant Abilities (授予能力关联)
│  ├─ NotifyCueAdd
│  └─ OnInitialApply 生命周期回调
```

### 堆叠系统

完整的 UE5 对齐堆叠策略：
- **None**: 不可叠加，重复施加被拒绝
- **RefreshDuration**: 刷新剩余时间 (支持 Reset/Extend 策略)
- **AddStacks**: 增加层数，达到 maxStacks 时触发 `overflowPolicy`

### 效果上下文 (GameplayEffectContext)

**文件**: `Assets/Scripts/GASSystem/Core/GameplayEffectContext.cs` (148行)

对应 UE5 `FGameplayEffectContext`：
```csharp
public class GameplayEffectContext
{
    public AbilitySystemComponent InstigatorASC;   // 谁发起的
    public GameObject Instigator;                   // 发起者 Actor
    public GameObject SourceObject;                 // 来源对象 (武器/技能)
    public Vector3 Origin;                          // 世界位置
    public Vector3 Normal;                          // 方向法线
    public HitResultInfo HitResult;                 // 命中结果
    public Dictionary<GameplayTagSO, float> SetByCallerMagnitudes; // 动态数值
}
```

### 事务性施加与回滚

Duration/Infinite 效果施加失败时自动回滚（128行实现）：
```csharp
// 记录所有已应用的 modifiers 和 tags
// 失败时遍历回滚:
foreach (var (attrVal, modifier) in appliedModifiers)
    attrVal.RemoveModifier(modifier);
foreach (var tag in appliedTags)
    tagComponent.RemoveTag(tag);
```

### ActiveGameplayEffect

**文件**: `Assets/Scripts/GASSystem/ActiveGameplayEffect.cs` (109行)

运行时效果实例，管理：
- 持续时间更新 (Tick + TimeRemaining)
- 周期 Tick (period > 0 时按间隔触发 OnPeriodicTick)
- 堆叠管理 (AddStack / RemoveStack)
- 刷新策略 (Refresh / Extend)

---

## 标签系统 (Tag)

### GameplayTagSO (层级标签)

**文件**: `Assets/Scripts/ScriptsObject/GameplayTagSO.cs` (42行)

支持父子层级关系：
```
State
├── State.Combat
│   ├── State.Combat.Attacking
│   ├── State.Combat.Guarding
│   └── State.Combat.Stunned
├── State.Dead
└── State.Invincible
```

**核心方法**:
- `HasChild(other)`: 检查 otherTag 是否为自己(或祖先)的子标签
- `GetFullPath()`: 获取完整层级路径 (如 "State.Combat.Guarding")

### TagComponent (标签组件)

**文件**: `Assets/Scripts/GASSystem/TagComponent.cs` (284行)

挂载在角色上，管理标签的引用计数和生命周期：

**三种标签类型**:
1. **永久标签** (RefCount): GE 授予的标签，引用计数管理
2. **瞬态标签** (Transient): 单帧有效的标签 (连招窗口/攻击判定)
3. **缓存标签** (Cached): 0.25秒内有效的瞬态标签历史

**核心方法**:
- `AddTag(tag)`: 增加引用计数
- `RemoveTag(tag)`: 减少引用计数
- `HasTag(tag)`: 检查是否拥有 (含瞬态)
- `HasTagOrChild(tag)`: 层级匹配查询
- `ConsumeTag(tag)`: 消耗性查询 (连招触发用)
- `AddTransientTag(tag)`: 添加单帧标签
- `ApplyBuff(buffData)`: 旧版 Buff 管理 (已标记 Obsolete)

### GameplayTagQuery (标签查询)

**文件**: `Assets/Scripts/GASSystem/Core/GameplayEventData.cs` (145行)

支持复杂布尔逻辑：
```csharp
public class GameplayTagQuery
{
    public List<GameplayTagSO> MatchAllTags;  // 必须全部匹配
    public List<GameplayTagSO> MatchAnyTags;  // 匹配任意一个
    public List<GameplayTagSO> NoMatchTags;   // 不能拥有
}
```

用途：GE Application Requirements、Ability Activation Checks、Immunity Queries。

---

## GameplayCue 反馈系统

### 架构

```
GameplayCueManager (单例)
  └─ Dictionary<GameplayTagSO, IGameplayCue>
       ├─ ParticleCue: 粒子特效
       ├─ SoundCue: 音效
       ├─ FloatingTextCue: 浮动文字
       ├─ HitImpactCue: 受击特效
       └─ ... 可扩展任意实现
```

### IGameplayCue 接口

**文件**: `Assets/Scripts/GASSystem/IGameplayCue.cs` (23行)

```csharp
public interface IGameplayCue
{
    void OnExecute(GameObject target, GameplayEffectSpec spec);  // Instant 效果
    void OnAdd(GameObject target, GameplayEffectSpec spec);      // Duration 施加
    void OnRemove(GameObject target);                             // Duration 移除
}
```

### 分发流程

GE 施加/移除时自动调用 CueManager:
- Instant GE → `ExecuteCue(tag)` → `cue.OnExecute()`
- Duration GE 施加 → `AddCue(tag)` → `cue.OnAdd()`
- Duration GE 移除 → `RemoveCue(tag)` → `cue.OnRemove()`

---

## AbilityTask 异步任务系统

**文件**: `Assets/Scripts/GASSystem/Task/AbilityTask.cs` (94行)

对应 UE5 `UAbilityTask`，支持异步任务管道：

```csharp
public abstract class AbilityTask
{
    public bool IsActive { get; }         // 是否活跃
    public bool IsFinished { get; }       // 是否完成
    public EAbilityTaskWaitState WaitState; // 等待策略
    public event Action OnTaskCompleted;
    public event Action OnTaskCancelled;
}
```

**WaitState 等待策略**:
- `WaitingOnGame`: 等待游戏逻辑
- `WaitingOnUser`: 等待用户输入
- `WaitingOnAvatar`: 等待角色动画/物理

**已实现的任务**:
| 任务 | 说明 |
|------|------|
| `WaitDelayTask` | 等待指定时间 |
| `WaitInputTasks` | 等待玩家输入 |
| `WaitTargetDataTask` | 等待目标数据 |
| `WaitGameplayEventTask` | 等待 GameplayEvent |
| `WaitAttributeChangeTask` | 等待属性变化 |
| `WaitGameplayTagTask` | 等待标签变化 |
| `WaitOverlapTask` | 等待碰撞重叠 |
| `SearchTargetTask` | 搜索目标 |
| `PlayMontageAndWaitTask` | 播放动画并等待 |
| `EndAbilityTask` | 结束能力 |
| `RemainingAbilityTasks` | 等待剩余任务 |

---

## Event 事件系统

### GameplayEventData

**文件**: `Assets/Scripts/GASSystem/Core/GameplayEventData.cs` (66行)

对应 UE5 `FGameplayEventData`，Ability 间通信的核心：

```csharp
public class GameplayEventData
{
    public GameplayTagSO EventTag;        // 事件标签
    public GameObject Instigator;         // 发起者
    public GameObject Target;             // 目标
    public Object OptionalObject;         // 可选对象1 (武器等)
    public float EventMagnitude;          // 事件数值
    public GameplayEffectContext Context; // 效果上下文
}
```

### HandleGameplayEvent 流程

```
ASC.HandleGameplayEvent(eventTag, payload)
├─ 查找 _triggeredAbilityMap[eventTag] (显式注册)
├─ 遍历所有 Ability 的 ShouldAbilityRespondToEvent
├─ 通过层级标签匹配 (tag / HasChild / parent-child)
└─ TryActivateAbilityBySpec (触发匹配能力)
```

---

## GASHost 全局调度

**文件**: `Assets/Scripts/GASSystem/Core/GASHost.cs` (87行)

全局管理器，集中 Tick 所有注册的 ASC：
```csharp
public class GASHost : MonoBehaviour
{
    public float TimeScale { get; set; } = 1f;  // 全局时间缩放
    private List<AbilitySystemComponent> _registeredASCs;
}
```

支持全局 TimeScale: 1 = 正常, 0 = 暂停, 0.5 = 慢动作，可用于子弹时间等效果。

---

## 执行流程示例

### 玩家释放一次攻击技能

```
1. 输入触发
   PlayerSkillComponent.ActivateAbilityViaSpec("Attack_Light")
   → ASC.TryActivateAbilityByHandle(handle)

2. 激活检查
   ├─ 检查 BlockAbilitiesWithTag
   ├─ CanActivateAbility: 冷却? 耐力够? 被眩晕?
   ├─ InstancedPerExecution → 复用实例

3. PreActivate → CancelAbilitiesWithTag → 授予 ActivationOwnedTags

4. ActivateAbility
   ├─ CommitCost: 扣除 Stamina (CostEffectSpec)
   ├─ CommitCooldown: 施加 CooldownEffect + cooldownTag
   ├─ 授予 GrantedTags
   └─ Activate(): DefaultGameplayAbility
       └─ 施加 effectsToApply 中的 GE 列表

5. 技能 Timeline 播放
   ├─ HitBoxEvent.OnStart: 激活攻击碰撞体
   ├─ AttackEvent.OnStart: MeleeWeapon 初始化
   ├─ [攻击判定帧]
   │   └─ MeleeWeapon.OnTriggerEnter
   │       └─ HurtBoxManager.ProcessHit
   │           ├─ 拼刀检测 (ClashManager)
   │           ├─ 格挡检测 (Guard Tag)
   │           ├─ 精准闪避检测 (perfect_dodge Tag)
   │           ├─ 受击反馈 (HitFreeze + VFX + SFX + Camera)
   │           └─ ASC.ApplyGameplayEffect (Damage GE)
   │               └─ ExecuteInstantEffect
   │                   ├─ ExecutionCalculation.Execute()
   │                   └─ ModifyHealth / ModifyPoise
   ├─ CancelEvent: 允许取消窗口
   ├─ LoopEvent: 循环等待输入
   └─ [技能结束]
       └─ EndAbility: 移除 GrantedTags + ActivationOwnedTags
           └─ 移除 cancelOnAbilityEnd 关联的 GE
```

### Buff 施加与移除

```
1. ASC.ApplyGameplayEffect(buffEffect, attackerASC, targetASC)
   → DurationPolicy.Duration → ApplyDurationEffect

2. 堆叠检查
   ├─ 已有: AddStacks → AddStack() → Refresh()
   └─ 首次: 创建 ActiveGameplayEffect

3. 事务性施加
   ├─ 注册 AttributeModifier 到 AttributeValue.modifiers
   │   (Additive +30 AttackPower via StackCount感知)
   ├─ 授予 grantedTags → TagComp.AddTag
   └─ NotifyCueAdd → GameplayCueManager.AddCue

4. 每帧 Tick
   ├─ ActiveGameplayEffect.Tick(deltaTime) → TimeRemaining -= dt
   └─ period > 0 → OnPeriodicTick (周期伤害等)

5. GE 到期
   └─ RemoveActiveEffectInternal
       ├─ attrValue.RemoveModifier (移除属性修改器)
       ├─ tagComponent.RemoveTag (减少引用计数)
       └─ NotifyCueRemove (清理特效)
```
