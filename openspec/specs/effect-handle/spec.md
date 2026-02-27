## ADDED Requirements

### Requirement: ActiveGameplayEffect 唯一 Handle 标识
ActiveGameplayEffect MUST 包含只读的 `Handle` 属性（int 类型）。Handle 值 SHALL 通过全局自增计数器生成，保证每个 ActiveGameplayEffect 实例的 Handle 唯一。Handle 值 SHALL 大于 0（0 表示无效 Handle）。

#### Scenario: 每个 ActiveGameplayEffect 获得唯一 Handle
- **WHEN** 连续创建两个 ActiveGameplayEffect 实例
- **THEN** 第一个 Handle SHALL 为 1，第二个 SHALL 为 2（或其他递增值，但两者不同）

#### Scenario: Handle 值始终大于 0
- **WHEN** 创建 ActiveGameplayEffect 实例
- **THEN** Handle SHALL 大于 0

### Requirement: ApplyEffectSpec 返回 Handle
AbilitySystemComponent.ApplyEffectSpec MUST 返回 int 类型的 Handle。对于 Duration/Infinite 效果 SHALL 返回新创建的 ActiveGameplayEffect 的 Handle；对于 Instant 效果 SHALL 返回 0。

#### Scenario: Duration 效果施加返回有效 Handle
- **WHEN** 调用 ApplyEffectSpec 施加一个 Duration 效果
- **THEN** 返回值 SHALL 为新创建的 ActiveGameplayEffect 的 Handle（大于 0）

#### Scenario: Instant 效果施加返回 0
- **WHEN** 调用 ApplyEffectSpec 施加一个 Instant 效果
- **THEN** 返回值 SHALL 为 0

### Requirement: 按 Handle 移除活跃效果
AbilitySystemComponent MUST 提供 `RemoveActiveEffectByHandle(int handle)` 方法。该方法 SHALL 在活跃效果列表中查找匹配 Handle 的 ActiveGameplayEffect 并移除（包括清理修改器和标签）。如果未找到匹配 Handle，SHALL 静默忽略。

#### Scenario: 通过 Handle 成功移除效果
- **WHEN** ASC 拥有一个 Handle=5 的 ActiveGameplayEffect
- **AND** 调用 RemoveActiveEffectByHandle(5)
- **THEN** 该效果 SHALL 被移除，属性修改器和标签被清理

#### Scenario: Handle 不匹配时静默忽略
- **WHEN** 调用 RemoveActiveEffectByHandle(999) 且无匹配效果
- **THEN** SHALL 不产生错误，不影响现有效果

#### Scenario: 效果已过期后 Handle 无法再次移除
- **WHEN** Handle=5 的 Duration 效果已到期被自动移除
- **AND** 调用 RemoveActiveEffectByHandle(5)
- **THEN** SHALL 静默忽略（无匹配效果）
