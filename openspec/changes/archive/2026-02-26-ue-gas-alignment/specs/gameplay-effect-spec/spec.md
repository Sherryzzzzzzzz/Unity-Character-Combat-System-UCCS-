## ADDED Requirements

### Requirement: GameplayEffectSpec 运行时规格对象
AbilitySystemComponent MUST 提供 `MakeEffectSpec(GameplayEffect effect)` 方法，返回一个 `GameplayEffectSpec` 实例。GameplayEffectSpec SHALL 持有：源 GameplayEffect SO 引用、施加者 ASC 引用、捕获的属性快照字典（`capturedAttackerAttributes`）、可选的动态 Magnitude 覆盖。

#### Scenario: MakeEffectSpec 创建 Spec 并捕获施加者属性
- **WHEN** 攻击者 ASC（AttackPower=30, Defense=10）调用 MakeEffectSpec(damageEffect)
- **THEN** 返回的 GameplayEffectSpec SHALL 持有 damageEffect 引用
- **AND** capturedAttackerAttributes SHALL 包含 AttackPower=30

#### Scenario: Spec 保留施加者 ASC 引用
- **WHEN** ASC_A 调用 MakeEffectSpec(effect)
- **THEN** 返回的 GameplayEffectSpec 的 InstigatorASC SHALL 引用 ASC_A

### Requirement: ApplyEffectSpec 施加流程
AbilitySystemComponent MUST 提供 `ApplyEffectSpec(GameplayEffectSpec spec)` 方法，通过 Spec 施加效果。该方法 SHALL 使用 Spec 中的快照属性值进行伤害计算，而非实时读取施加者属性。

#### Scenario: ApplyEffectSpec 使用快照属性计算伤害
- **WHEN** Spec 创建时施加者 AttackPower=30
- **AND** 创建后施加者 AttackPower 变为 50
- **AND** 调用 targetASC.ApplyEffectSpec(spec)，effect 为 Instant、damage=10、damageMultiplier=1
- **THEN** finalDamage SHALL 使用快照值 30（而非实时值 50）

#### Scenario: ApplyEffectSpec 返回 EffectHandle
- **WHEN** 对 Duration/Infinite 效果调用 ApplyEffectSpec
- **THEN** SHALL 返回一个有效的 int EffectHandle（大于 0）

#### Scenario: Instant 效果的 ApplyEffectSpec 返回无效 Handle
- **WHEN** 对 Instant 效果调用 ApplyEffectSpec
- **THEN** SHALL 返回 0（无效 Handle，因为 Instant 效果不创建 ActiveEffect）

### Requirement: ApplyGameplayEffect 向后兼容
现有的 `ApplyGameplayEffect(GameplayEffect, AbilitySystemComponent)` 方法 MUST 继续工作，内部 SHALL 创建 GameplayEffectSpec 后委托给 ApplyEffectSpec。所有现有调用点无需修改。

#### Scenario: 原有 ApplyGameplayEffect 继续正常工作
- **WHEN** 调用 targetASC.ApplyGameplayEffect(effect, attackerASC)
- **THEN** 效果 SHALL 正常施加，行为与之前一致

#### Scenario: ApplyGameplayEffect 内部使用 Spec 流程
- **WHEN** 调用 ApplyGameplayEffect(effect, attackerASC)
- **THEN** 内部 SHALL 先调用 MakeEffectSpec 创建 Spec，再调用 ApplyEffectSpec 施加

### Requirement: Magnitude 计算模式
EffectAttributeModifier MUST 包含 `MagnitudeCalculation` 枚举字段，支持三种模式：`Static`（使用 SO 上的固定 value）、`AttributeBased`（从施加者/目标捕获指定属性值作为 Magnitude）、`Custom`（通过 IMagnitudeCalculation 接口计算）。默认值 SHALL 为 Static。

#### Scenario: Static 模式使用 SO 配置的固定值
- **WHEN** EffectAttributeModifier 的 MagnitudeCalculation 为 Static、value=15
- **THEN** Spec 创建时该修改器的最终 Magnitude SHALL 为 15

#### Scenario: AttributeBased 模式从施加者属性捕获 Magnitude
- **WHEN** EffectAttributeModifier 的 MagnitudeCalculation 为 AttributeBased、captureAttribute=AttackPower
- **AND** 施加者 AttackPower 聚合值为 25
- **THEN** Spec 创建时该修改器的最终 Magnitude SHALL 为 25

#### Scenario: Custom 模式调用 IMagnitudeCalculation 接口
- **WHEN** EffectAttributeModifier 的 MagnitudeCalculation 为 Custom、且配置了 IMagnitudeCalculation 实现
- **THEN** Spec 创建时 SHALL 调用 IMagnitudeCalculation.CalculateMagnitude(spec) 获取 Magnitude

### Requirement: IMagnitudeCalculation 接口
系统 MUST 提供 `IMagnitudeCalculation` 接口，包含 `float CalculateMagnitude(GameplayEffectSpec spec)` 方法。自定义 Magnitude 计算类 SHALL 实现此接口。

#### Scenario: 实现 IMagnitudeCalculation 自定义伤害公式
- **WHEN** 一个实现 IMagnitudeCalculation 的类返回 spec 中 AttackPower 快照 × 2
- **AND** 施加者 AttackPower 快照为 20
- **THEN** CalculateMagnitude SHALL 返回 40

### Requirement: 属性快照在 Spec 创建时捕获
GameplayEffectSpec 在创建时 MUST 捕获施加者的所有属性当前值（通过 AttributeSet），存储为 `capturedAttackerAttributes` 字典（key 为 GameplayAttribute 枚举，value 为 float）。Duration 效果的周期 Tick 也 SHALL 使用快照值。

#### Scenario: 快照值不随施加者属性后续变化而改变
- **WHEN** 施加者 AttackPower=20 时创建 Spec
- **AND** 施加后施加者 AttackPower 变为 100
- **AND** Duration 效果的周期 Tick 触发
- **THEN** Tick 使用的 AttackPower SHALL 仍为 20

#### Scenario: 施加者被销毁后快照值仍可用
- **WHEN** Spec 创建时捕获了施加者 AttackPower=20
- **AND** 施加后施加者 GameObject 被销毁
- **AND** Duration 效果的周期 Tick 触发
- **THEN** Tick 使用的 AttackPower SHALL 为快照值 20（不产生 NullReferenceException）
