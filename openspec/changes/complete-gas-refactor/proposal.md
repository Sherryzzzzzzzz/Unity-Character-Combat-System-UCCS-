## Why

当前项目的 GAS 系统是一个轻量级骨架实现，存在多项核心缺失，无法支撑复杂战斗玩法的开发：

1. **GameplayEffect 过于简单**：仅包含 `damage`/`poiseDamage` 两个字段，没有持续时间类型、周期性 Tick、堆叠策略，无法实现持续伤害、增益减益等常见效果。
2. **属性系统缺乏 Modifier 管线**：`AttributeSet` 直接存储裸值，伤害公式硬编码在 `AbilitySystemComponent.ApplyGameplayEffect` 中，Buff 无法实际修改属性（AttackPower/Defense 等），BaseValue 与 CurrentValue 没有分离。
3. **Buff 与 Effect 系统割裂**：`BuffSO` 独立于 `GameplayEffect` 存在，只管理标签生命周期但不修改属性（相关代码被注释掉），两套系统职责重叠。
4. **GameplayAbility 无具体实现**：只有抽象基类，没有子类、没有 Cost/Cooldown 的 Effect 驱动、没有异步 Task 支持，无法用于代码驱动的技能流程。
5. **缺少 Gameplay Cue**：效果触发的视觉/音频反馈完全依赖时间轴事件硬编码，无法由 Gameplay 状态动态触发。
6. **韧性系统不完整**：破韧后 `_isBroken` 永远为 true，无恢复路径；死亡事件 `OnDeath` 无监听者。

随着战斗系统复杂度提升，需要将 GAS 升级为接近 UE GAS 的完整架构，为所有战斗玩法提供统一、可扩展的基础设施。

## What Changes

### 模块一：Attribute Modifier 系统
- **BREAKING** 重构 `AttributeSet`：每个属性拆分为 BaseValue + CurrentValue，CurrentValue 由 BaseValue + 所有活跃 Modifier 聚合计算
- 新增 `AttributeModifier` 结构：支持 Additive / Multiplicative / Override 三种运算类型
- 新增属性变化事件回调 `OnAttributeChanged`，供 UI 和其他系统监听

### 模块二：GameplayEffect 完整生命周期
- **BREAKING** 重构 `GameplayEffect` ScriptableObject：新增持续时间类型（Instant / Duration / Infinite）、周期性 Tick 配置、堆叠策略（StackCount / StackDuration / StackReset）
- 新增 `GameplayEffectSpec` 运行时实例：携带 Instigator / Target / Level / SourceAbility 等上下文
- 新增 `ActiveGameplayEffect` 容器：管理正在生效的 Duration/Infinite 效果，支持定时移除和周期性 Tick
- `GameplayEffect` 中新增 `List<AttributeModifier>` 字段，定义效果对属性的修改
- 新增 `GameplayEffectContext` 传递命中信息、施法者信息等元数据
- **BREAKING** `AbilitySystemComponent.ApplyGameplayEffect` 签名变更为接收 `GameplayEffectSpec`

### 模块三：Buff 与 Effect 统一
- **BREAKING** 将 `BuffSO` 重构为 Duration/Infinite 类型 `GameplayEffect` 的配置前端，Buff 的属性修改通过 `AttributeModifier` 实现
- `TagComponent` 中的 Buff 管理迁移至 `AbilitySystemComponent` 的 ActiveEffect 容器
- 保留 `BuffEvent` 时间轴事件兼容性，内部改为通过 `GameplayEffect` 管线施加

### 模块四：Ability 框架完善
- 新增 `AbilityTask` 抽象基类，支持协程式异步等待
- 实现核心 Task：`WaitForDuration`、`WaitForInput`、`WaitForTagChange`
- 扩展 `GameplayAbility`：新增 Cost Effect（消耗）、Cooldown Effect（冷却）、CommitAbility 提交机制
- 与现有时间轴技能系统共存：时间轴驱动的技能保留，GameplayAbility 提供代码驱动的替代方案

### 模块五：Gameplay Cue 系统
- 新增 `GameplayCue` 抽象基类和 `GameplayCueManager` 单例
- GameplayCue 通过 GameplayTag 触发：Effect 施加/移除/Tick 时自动派发 Cue 事件
- 实现 `GameplayCueNotify_Static`（一次性 VFX/SFX）和 `GameplayCueNotify_Looping`（持续特效）
- 现有 EffectEvent/SoundEvent 保持兼容，GameplayCue 作为补充方案

### 模块六：战斗事件连接
- 连接 `AttributeSet` 事件：死亡处理（`OnDeath`）、破韧硬直（`OnPoiseBreak`）、韧性恢复机制
- 修复韧性恢复逻辑：破韧后可通过延时自动恢复
- 伤害管线适配：`AttackEvent`、`HurtBoxManager`、`MeleeWeapon` 迁移到新的 EffectSpec 调用

## Capabilities

### New Capabilities
- `attribute-modifier`: 属性修改器栈系统——Base/Current 值分离、Additive/Multiplicative/Override 修改器、Modifier 聚合计算、属性变更事件广播
- `gameplay-effect-lifecycle`: GameplayEffect 完整生命周期——EffectSpec 实例化、Instant/Duration/Infinite/Periodic 效果类型、堆叠策略、ActiveEffect 容器管理
- `buff-effect-unification`: Buff 与 Effect 统一——BuffSO 重构为 GameplayEffect 的配置前端、Buff 管理迁移至 ASC、时间轴事件兼容
- `ability-task-framework`: Ability 异步任务框架——AbilityTask 基类、WaitForDuration/WaitForInput/WaitForTagChange 核心 Task、Ability Cost/Cooldown/Commit 机制
- `gameplay-cue`: Gameplay Cue 视觉/音频反馈系统——Tag 驱动的 Cue 派发、Static/Looping Cue 实现、GameplayCueManager 单例
- `combat-event-wiring`: 战斗事件连接——死亡处理、破韧硬直、韧性恢复、伤害管线迁移到 EffectSpec

### Modified Capabilities
- `unified-damage-pipeline`: 伤害施加流程从 `ApplyGameplayEffect(effect, attackerASC)` 变更为通过 `GameplayEffectSpec` 施加，所有调用方需适配新 API

## Impact

### 核心重构文件
- `Assets/Scripts/GASSystem/AbilitySystemComponent.cs` — 成为 ActiveEffect 容器、Modifier 管理、Cue 派发的中心枢纽
- `Assets/Scripts/GASSystem/AttributeSet.cs` — BaseValue/CurrentValue 拆分，Modifier 聚合
- `Assets/Scripts/GASSystem/GameplayAbility.cs` — 新增 Task 生命周期管理、Cost/Cooldown
- `Assets/Scripts/GASSystem/TagComponent.cs` — Buff 管理职责迁移至 ASC
- `Assets/Scripts/ScriptsObject/GameplayEffect.cs` — 从简单数据容器升级为完整 Effect 定义
- `Assets/Scripts/ScriptsObject/BuffSO.cs` — 重构为 GameplayEffect 的配置前端
- `Assets/Scripts/ScriptsObject/AttackData.cs` — 适配新的 Effect 引用方式

### 新增文件（约 12-15 个 .cs 文件）
- `GameplayEffectSpec.cs`、`ActiveGameplayEffect.cs`、`GameplayEffectContext.cs`
- `AttributeModifier.cs`
- `AbilityTask.cs`、`WaitForDuration.cs`、`WaitForInput.cs`、`WaitForTagChange.cs`
- `GameplayCue.cs`、`GameplayCueManager.cs`、`GameplayCueNotify_Static.cs`、`GameplayCueNotify_Looping.cs`

### 调用方适配
- `AttackEvent.cs`、`HurtBoxManager.cs`、`MeleeWeapon.cs` — 迁移到 GameplayEffectSpec 调用
- `PlayerSkillComponent.cs`、`EnemySkillComponent.cs` — 适配新的 Ability 激活方式
- `BuffEvent.cs` — 内部改为通过 GameplayEffect 管线施加

### 数据资产影响
- `Assets/ScriptObjects/GF/*.asset` — 需要重新配置新增字段（Duration、Modifiers 等），旧字段保持兼容
- `Assets/ScriptObjects/Buff/*.asset` — 需迁移到新的 GameplayEffect 关联模式
- `Assets/ScriptObjects/AttackData/*.asset` — 可能需要适配

### 无外部依赖新增
全部基于纯 C# + Unity API 实现
