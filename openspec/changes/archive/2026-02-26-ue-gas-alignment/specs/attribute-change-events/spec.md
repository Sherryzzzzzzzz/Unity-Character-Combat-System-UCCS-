## ADDED Requirements

### Requirement: AttributeSet 通用属性变更事件
AttributeSet MUST 提供通用的 `OnAttributeChanged` 事件（类型为 `Action<GameplayAttribute, float, float>`），参数为：变更的属性枚举、旧值、新值。当任何属性的当前值发生变化时 SHALL 触发此事件。

#### Scenario: Health 变更时触发 OnAttributeChanged
- **WHEN** 调用 ModifyHealth(-20)，Health 从 100 变为 80
- **THEN** OnAttributeChanged SHALL 被触发，参数为 (GameplayAttribute.Health, 100, 80)

#### Scenario: 修改器添加导致属性值变化时触发事件
- **WHEN** AttackPower baseValue=10，添加 Additive value=5 修改器
- **AND** AttackPower 从 10 变为 15
- **THEN** OnAttributeChanged SHALL 被触发，参数为 (GameplayAttribute.AttackPower, 10, 15)

#### Scenario: 修改器移除导致属性值变化时触发事件
- **WHEN** AttackPower 当前聚合值为 15（baseValue=10 + Additive=5）
- **AND** 移除 Additive=5 修改器，AttackPower 变回 10
- **THEN** OnAttributeChanged SHALL 被触发，参数为 (GameplayAttribute.AttackPower, 15, 10)

### Requirement: Health 和 Poise 直接值变更也触发事件
AttributeSet 的 `ModifyHealth` 和 `ModifyPoise` 方法 MUST 在值实际变化时触发 OnAttributeChanged 事件。

#### Scenario: ModifyPoise 触发事件
- **WHEN** 调用 ModifyPoise(-10)，Poise 从 50 变为 40
- **THEN** OnAttributeChanged SHALL 被触发，参数为 (GameplayAttribute.Poise, 50, 40)

#### Scenario: 值未变化时不触发事件
- **WHEN** Health=0 且 _isDead=true，调用 ModifyHealth(-10)
- **THEN** OnAttributeChanged SHALL 不被触发（因为值未实际变化）

### Requirement: GameplayAttribute 枚举扩展
GameplayAttribute 枚举 MUST 包含 `Health` 和 `Poise` 条目，以支持 OnAttributeChanged 事件通知这两个直接值属性的变更。

#### Scenario: GameplayAttribute 枚举包含 Health 和 Poise
- **WHEN** 读取 GameplayAttribute 枚举定义
- **THEN** SHALL 包含 AttackPower、Defense、HealthMax、PoiseMax、Health、Poise 六个值
