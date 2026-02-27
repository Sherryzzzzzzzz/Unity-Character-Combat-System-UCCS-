## Why

当前项目的 GAS 系统只是一个轻量级框架：GameplayEffect 是简单的数据容器（damage/poiseDamage），没有持续时间、Modifier 管线或堆叠策略；Buff 系统只管理标签生命周期但不修改属性；GameplayAbility 是同步抽象类，没有异步 Task 支持；特效/音效完全由时间轴事件驱动而非由 Gameplay 状态触发。这使得实现复杂的 RPG 机制（如持续伤害、属性增减益、引导技能、条件特效触发）极其困难。需要将 GAS 升级为接近 UE 完整语义的系统，为后续所有战斗玩法提供统一、可扩展的基础设施。

## What Changes

### 模块一：Effect 系统完善
- **BREAKING** 重构 `GameplayEffect` ScriptableObject，新增持续时间类型（Instant/Duration/Infinite）、周期性 Tick、堆叠策略（StackCount/StackDuration/StackApplication）
- 新增 `GameplayEffectSpec` 运行时实例，携带 Instigator/Target/SourceAbility 上下文
- 新增 `ActiveGameplayEffect` 容器管理正在生效的 Duration/Infinite 效果，支持定时移除和周期性 Tick
- 新增 `GameplayEffectContext` 传递命中信息、施法者信息、世界坐标等元数据
- **BREAKING** `AbilitySystemComponent.ApplyGameplayEffect` 签名变更，接收 `GameplayEffectSpec` 而非直接的 `GameplayEffect + ASC`

### 模块二：Attribute Modifier 系统
- 新增 `AttributeModifier` 结构：支持 Add / Multiply / Override 三种运算类型
- **BREAKING** 重构 `AttributeSet`：每个属性拆分为 BaseValue + CurrentValue，CurrentValue 由 BaseValue + 所有活跃 Modifier 聚合计算
- `GameplayEffect` 中新增 `List<AttributeModifier>` 字段，定义效果对属性的修改
- BuffSO 通过 GameplayEffect 间接修改属性（BuffSO 关联一个 GameplayEffect）
- 新增属性变化回调（OnAttributeChanged 事件），供 UI 和其他系统监听

### 模块三：Ability Task 异步能力
- 新增 `AbilityTask` 抽象基类，支持异步等待模式
- 实现核心 Task：`WaitForAnimationEvent`、`WaitForInput`、`WaitForDuration`、`WaitForTagChange`
- 扩展 `GameplayAbility` 支持协程式异步流程：施法阶段 → 引导阶段 → 执行阶段
- 能力可以 spawn 多个 Task，Task 完成后回调通知能力
- 与现有时间轴技能系统共存：时间轴驱动的技能仍保留，GameplayAbility 提供代码驱动的替代方案

### 模块四：Gameplay Cue 系统
- 新增 `GameplayCue` 抽象基类和 `GameplayCueManager` 单例
- GameplayCue 通过 GameplayTag 触发：当 Effect 施加/移除/Tick 时自动派发 Cue 事件
- 实现 `GameplayCueNotify_Static`（一次性 VFX/SFX）和 `GameplayCueNotify_Looping`（持续特效）
- 现有的 EffectEvent/SoundEvent 保持兼容，GameplayCue 作为补充的 Gameplay 状态驱动特效方案

## Capabilities

### New Capabilities

- `gameplay-effect-system`: GameplayEffect 持续时间、周期性 Tick、堆叠策略、GameplayEffectSpec 运行时实例、ActiveGameplayEffect 容器、GameplayEffectContext
- `attribute-modifier`: AttributeModifier 管线（Add/Multiply/Override）、BaseValue/CurrentValue 拆分、Modifier 聚合计算、属性变化事件
- `ability-task`: AbilityTask 异步等待框架、核心 Task 实现（WaitForAnimationEvent/WaitForInput/WaitForDuration/WaitForTagChange）、能力阶段管理
- `gameplay-cue`: GameplayCue 基类、GameplayCueManager 派发器、Static/Looping Cue 实现、Tag 驱动的特效触发

### Modified Capabilities

- `unified-damage-pipeline`: 伤害施加流程从直接调用 `ApplyGameplayEffect(effect, attackerASC)` 变更为通过 `GameplayEffectSpec` 施加，需要更新 AttackEvent 和 HurtBoxManager 中的调用

## Impact

- **核心重构文件**：
  - `Assets/Scripts/ScriptsObject/GameplayEffect.cs` — 从简单数据容器升级为完整 Effect 定义
  - `Assets/Scripts/GASSystem/AbilitySystemComponent.cs` — 成为 ActiveEffect 容器、Modifier 管理、Cue 派发的中心枢纽
  - `Assets/Scripts/GASSystem/AttributeSet.cs` — BaseValue/CurrentValue 拆分，Modifier 聚合
  - `Assets/Scripts/GASSystem/GameplayAbility.cs` — 新增 Task 生命周期管理
  - `Assets/Scripts/ScriptsObject/BuffSO.cs` — 关联 GameplayEffect 替代独立的 Buff 逻辑
- **新增文件**（约 12-15 个 .cs 文件）：
  - `GameplayEffectSpec.cs`、`ActiveGameplayEffect.cs`、`GameplayEffectContext.cs`
  - `AttributeModifier.cs`
  - `AbilityTask.cs`、`WaitForAnimationEvent.cs`、`WaitForInput.cs`、`WaitForDuration.cs`、`WaitForTagChange.cs`
  - `GameplayCue.cs`、`GameplayCueManager.cs`、`GameplayCueNotify_Static.cs`、`GameplayCueNotify_Looping.cs`
- **调用方适配**：
  - `AttackEvent.cs`、`HurtBoxManager.cs`、`MeleeWeapon.cs` — 迁移到 GameplayEffectSpec 调用
  - `TagComponent.cs` — Buff 应用改为通过 GameplayEffect 管线
  - `PlayerSkillComponent.cs`、`EnemySkillComponent.cs` — 可选集成 GameplayAbility 驱动的技能
- **ScriptableObject 资产**：现有 `Assets/ScriptObjects/GF/*.asset` 需要重新配置新增字段（Duration、Modifiers 等），旧字段保持兼容
- **无外部依赖新增**：所有实现基于纯 C# + Unity API
