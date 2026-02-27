## ADDED Requirements

### Requirement: AttributeValue 结构体
AttributeSet 中的每个可修改属性（AttackPower、Defense、HealthMax、PoiseMax）MUST 使用 `AttributeValue` 结构体存储，包含 `baseValue` 字段和内部修改器列表。Health 和 Poise 作为当前值字段 SHALL 保持为 float，不使用修改器。

#### Scenario: AttributeValue 初始化时当前值等于基础值
- **WHEN** AttributeSet 初始化时 AttackPower 的 baseValue 为 10
- **AND** 没有任何修改器
- **THEN** AttackPower 的 GetCurrentValue() SHALL 返回 10

### Requirement: 属性修改器类型
系统 MUST 支持两种属性修改器类型：`Additive`（加法）和 `Multiplicative`（乘法）。修改器 SHALL 通过 `AttributeModifier` 结构体表示，包含 ModifierType 枚举和 float value 字段。

#### Scenario: 加法修改器增加属性值
- **WHEN** AttackPower baseValue=10，添加一个 Additive 修改器 value=5
- **THEN** AttackPower.GetCurrentValue() SHALL 返回 15

#### Scenario: 乘法修改器按比例增加属性值
- **WHEN** AttackPower baseValue=10，添加一个 Multiplicative 修改器 value=0.5（+50%）
- **THEN** AttackPower.GetCurrentValue() SHALL 返回 15

### Requirement: 修改器聚合公式
属性当前值的计算公式 MUST 为：`CurrentValue = (BaseValue + ΣAdditive) × (1 + ΣMultiplicative)`。加法修改器先聚合，乘法修改器后聚合。

#### Scenario: 多个加法修改器叠加
- **WHEN** AttackPower baseValue=10，存在两个 Additive 修改器 value=3 和 value=7
- **THEN** GetCurrentValue() SHALL 返回 (10 + 3 + 7) × 1 = 20

#### Scenario: 加法和乘法修改器混合
- **WHEN** Defense baseValue=10，存在 Additive value=10 和 Multiplicative value=0.5
- **THEN** GetCurrentValue() SHALL 返回 (10 + 10) × (1 + 0.5) = 30

#### Scenario: 无修改器时返回基础值
- **WHEN** AttackPower baseValue=20，无任何修改器
- **THEN** GetCurrentValue() SHALL 返回 20

### Requirement: 修改器注册与移除
AttributeValue MUST 提供 `AddModifier(AttributeModifier)` 和 `RemoveModifier(AttributeModifier)` 方法。修改器的添加和移除 SHALL 立即影响 GetCurrentValue() 的返回值。

#### Scenario: 添加修改器后当前值更新
- **WHEN** AttackPower baseValue=10，调用 AddModifier(Additive, 5)
- **THEN** 下次调用 GetCurrentValue() SHALL 返回 15

#### Scenario: 移除修改器后当前值恢复
- **WHEN** AttackPower baseValue=10，已有 Additive value=5 的修改器
- **AND** 调用 RemoveModifier 移除该修改器
- **THEN** GetCurrentValue() SHALL 返回 10

### Requirement: GameplayEffect 属性修改器配置
GameplayEffect ScriptableObject MUST 包含 `modifiers` 列表字段，每个条目指定：目标属性（枚举）、修改器类型（Additive/Multiplicative）、修改值。当 Duration/Infinite 效果被施加时，ASC SHALL 将 modifiers 注册到目标 AttributeSet 对应属性；效果移除时 SHALL 注销这些修改器。

#### Scenario: Duration 效果施加属性修改器
- **WHEN** ASC 施加一个 Duration 效果，其 modifiers 包含 [AttackPower, Additive, +10]
- **THEN** 目标 AttributeSet 的 AttackPower SHALL 增加 10（通过修改器，非直接修改）

#### Scenario: 效果移除时清理属性修改器
- **WHEN** 一个施加了 [AttackPower, Additive, +10] 修改器的 Duration 效果到期
- **THEN** 目标 AttributeSet 的 AttackPower SHALL 恢复到施加前的值（移除修改器）

#### Scenario: Instant 效果不注册持久修改器
- **WHEN** ASC 施加一个 Instant 效果，其 modifiers 包含 [AttackPower, Additive, +10]
- **THEN** ASC SHALL 不注册持久修改器（Instant 效果的属性变更通过直接修改 baseValue 实现）

### Requirement: AttributeSet 向后兼容访问
AttributeSet MUST 提供与现有字段名兼容的属性访问方式。外部代码通过 `attributeSet.AttackPower` 访问时 SHALL 获得 GetCurrentValue() 的聚合值，而非 baseValue。

#### Scenario: 通过属性名访问获得聚合值
- **WHEN** AttackPower baseValue=10，存在 Additive 修改器 value=5
- **AND** 外部代码读取 attributeSet.AttackPower
- **THEN** SHALL 返回 15（聚合值），而非 10（基础值）

#### Scenario: ModifyHealth 继续使用直接修改语义
- **WHEN** 调用 attributeSet.ModifyHealth(-10)
- **THEN** Health 值 SHALL 直接减少 10（不通过修改器系统）
