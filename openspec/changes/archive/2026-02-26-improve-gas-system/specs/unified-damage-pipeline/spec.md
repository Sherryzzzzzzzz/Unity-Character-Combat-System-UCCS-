## MODIFIED Requirements

### Requirement: 单一伤害施加 API
AbilitySystemComponent MUST 保留 `ApplyGameplayEffect(GameplayEffect, AbilitySystemComponent)` 作为主要的效果施加方法。该方法 SHALL 根据 GameplayEffect 的 DurationPolicy 分派处理：Instant 效果立即执行伤害计算，Duration/Infinite 效果创建 ActiveGameplayEffect 实例。伤害计算公式 MUST 整合 `damageMultiplier` 和属性修改器聚合值：`finalDamage = (effect.damage × effect.damageMultiplier + attackerAttr.AttackPower - targetAttr.Defense)`，其中 AttackPower 和 Defense 通过 AttributeValue.GetCurrentValue() 获取（包含修改器影响）。

#### Scenario: ApplyGameplayEffect 是唯一效果入口
- **WHEN** 任何系统需要对目标施加效果（伤害、Buff、属性修改）
- **THEN** SHALL 通过 `targetASC.ApplyGameplayEffect(effect, attackerASC)` 调用

#### Scenario: Instant 效果使用新伤害公式
- **WHEN** ASC 施加一个 DurationPolicy=Instant、damage=10、damageMultiplier=1.5 的 GameplayEffect
- **AND** 攻击者 AttackPower 聚合值为 20，目标 Defense 聚合值为 5
- **THEN** finalDamage SHALL 为 (10 × 1.5 + 20 - 5) = 30

#### Scenario: Duration 效果通过 ApplyGameplayEffect 创建 ActiveEffect
- **WHEN** ASC 施加一个 DurationPolicy=Duration 的 GameplayEffect
- **THEN** ASC SHALL 创建 ActiveGameplayEffect 实例而非立即执行伤害

#### Scenario: 属性修改器影响伤害计算
- **WHEN** 攻击者有一个 Additive +10 的 AttackPower 修改器（baseValue=20，聚合值=30）
- **AND** 施加一个 damage=10、damageMultiplier=1 的 Instant 效果给 Defense=5 的目标
- **THEN** finalDamage SHALL 为 (10 × 1 + 30 - 5) = 35
