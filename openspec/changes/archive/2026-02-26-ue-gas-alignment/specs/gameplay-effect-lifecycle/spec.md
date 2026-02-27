## MODIFIED Requirements

### Requirement: 单一伤害施加 API
AbilitySystemComponent MUST 保留 `ApplyGameplayEffect(GameplayEffect, AbilitySystemComponent)` 作为便捷的效果施加方法。该方法内部 SHALL 先调用 `MakeEffectSpec` 创建 GameplayEffectSpec（捕获施加者属性快照），再委托给 `ApplyEffectSpec(spec)` 执行实际施加。此外 ASC MUST 提供 `MakeEffectSpec(GameplayEffect)` 和 `ApplyEffectSpec(GameplayEffectSpec)` 方法作为完整的两步施加流程。伤害计算公式 MUST 使用 Spec 中的快照属性值：`finalDamage = (effect.damage × effect.damageMultiplier + capturedAttackPower - targetDefense)`。Duration 效果的周期 Tick 也 SHALL 使用 Spec 中的快照属性值。

#### Scenario: ApplyGameplayEffect 是唯一效果入口
- **WHEN** 任何系统需要对目标施加效果（伤害、Buff、属性修改）
- **THEN** SHALL 通过 `targetASC.ApplyGameplayEffect(effect, attackerASC)` 或 `targetASC.ApplyEffectSpec(spec)` 调用

#### Scenario: Instant 效果使用快照伤害公式
- **WHEN** Spec 创建时施加者 AttackPower 快照为 20，目标 Defense 聚合值为 5
- **AND** effect 为 Instant、damage=10、damageMultiplier=1.5
- **THEN** finalDamage SHALL 为 (10 × 1.5 + 20 - 5) = 30

#### Scenario: Duration 效果通过 ApplyEffectSpec 创建 ActiveEffect
- **WHEN** ASC 施加一个 DurationPolicy=Duration 的 GameplayEffectSpec
- **THEN** ASC SHALL 创建 ActiveGameplayEffect 实例而非立即执行伤害

#### Scenario: 属性修改器影响伤害计算（通过快照）
- **WHEN** 施加者有一个 Additive +10 的 AttackPower 修改器（baseValue=20，快照值=30）
- **AND** 施加一个 damage=10、damageMultiplier=1 的 Instant 效果给 Defense=5 的目标
- **THEN** finalDamage SHALL 为 (10 × 1 + 30 - 5) = 35

#### Scenario: 周期 Tick 使用快照而非实时属性
- **WHEN** Spec 创建时施加者 AttackPower 快照为 20
- **AND** Duration 效果周期 Tick 触发时施加者 AttackPower 已变为 50
- **THEN** Tick 伤害计算使用的 AttackPower SHALL 为 20

### Requirement: ASC Effect 生命周期管理
AbilitySystemComponent MUST 在 Update 中 tick 所有活跃的 ActiveGameplayEffect：更新剩余时间、处理周期 Tick、移除到期效果。ASC MUST 提供 `RemoveActiveEffect` 方法和 `RemoveActiveEffectByHandle(int handle)` 方法用于移除效果。效果施加和移除时，如果 GameplayEffect 包含 cueTag，ASC SHALL 通知 GameplayCueManager。ApplyEffectSpec 对 Duration/Infinite 效果 SHALL 返回 EffectHandle。

#### Scenario: ASC 每帧更新所有活跃效果
- **WHEN** ASC 拥有 3 个活跃的 ActiveGameplayEffect
- **THEN** 每帧 Update 中 SHALL 对每个实例执行时间递减和周期检查

#### Scenario: 到期效果在 Tick 中被自动移除
- **WHEN** 一个 ActiveGameplayEffect 的 TimeRemaining 降至 0 或以下
- **THEN** ASC SHALL 在当前帧 Tick 中移除该效果并清理其属性修改器和授予标签

#### Scenario: 手动移除效果
- **WHEN** 外部系统调用 ASC.RemoveActiveEffect 传入一个 ActiveGameplayEffect
- **THEN** 该效果 SHALL 被立即从活跃列表移除，属性修改器和授予标签 SHALL 被清理

#### Scenario: 效果移除时通知 CueManager
- **WHEN** 一个 cueTag=Tag_Cue_Burning 的 Duration 效果到期被移除
- **THEN** ASC SHALL 通知 GameplayCueManager 调用对应 IGameplayCue 的 OnRemove
