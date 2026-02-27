## ADDED Requirements

### Requirement: GameplayEffect 持续时间策略
GameplayEffect ScriptableObject MUST 包含 `DurationPolicy` 枚举字段，支持三种策略：`Instant`（即时生效后不保留）、`Duration`（持续指定时间后自动移除）、`Infinite`（永久生效直到手动移除）。默认值 SHALL 为 `Instant`，以保持与现有即时伤害逻辑的向后兼容。

#### Scenario: 即时效果立即生效且不产生 ActiveEffect
- **WHEN** ASC 施加一个 DurationPolicy 为 Instant 的 GameplayEffect
- **THEN** 效果 SHALL 立即执行（扣血/扣韧性），且不在 ASC 的活跃效果列表中创建 ActiveGameplayEffect 实例

#### Scenario: 持续效果创建 ActiveEffect 并在到期后自动移除
- **WHEN** ASC 施加一个 DurationPolicy 为 Duration 且 duration=5 的 GameplayEffect
- **THEN** ASC SHALL 创建一个 ActiveGameplayEffect 实例加入活跃列表
- **AND** 5 秒后该 ActiveGameplayEffect SHALL 被自动移除

#### Scenario: 无限效果持续存在直到手动移除
- **WHEN** ASC 施加一个 DurationPolicy 为 Infinite 的 GameplayEffect
- **THEN** ASC SHALL 创建一个 ActiveGameplayEffect 实例
- **AND** 该实例 SHALL 不会因时间流逝而自动移除

### Requirement: 周期性效果 Tick
当 GameplayEffect 的 `period` 字段大于 0 且 DurationPolicy 为 Duration 或 Infinite 时，ASC MUST 每隔 `period` 秒执行一次效果逻辑（如周期伤害或周期治疗）。

#### Scenario: 持续周期伤害每秒 Tick
- **WHEN** ASC 施加一个 DurationPolicy=Duration、duration=5、period=1、damage=10 的 GameplayEffect
- **THEN** 目标 SHALL 在第 1、2、3、4、5 秒各受到一次 10 点伤害
- **AND** 5 秒后 ActiveGameplayEffect 被移除

#### Scenario: period 为 0 时不产生周期 Tick
- **WHEN** ASC 施加一个 DurationPolicy=Duration、period=0 的 GameplayEffect
- **THEN** 效果 SHALL 仅在施加和移除时执行逻辑，不产生周期 Tick

### Requirement: ActiveGameplayEffect 运行时实例
ASC MUST 使用 `ActiveGameplayEffect` 类管理非即时效果的运行时状态。ActiveGameplayEffect SHALL 持有：效果数据引用（GameplayEffect SO）、施加者 ASC 引用、剩余时间、当前层数、周期计时器。

#### Scenario: ActiveGameplayEffect 持有施加者引用
- **WHEN** 攻击者 ASC 对目标 ASC 施加一个 Duration 效果
- **THEN** 创建的 ActiveGameplayEffect 的 InstigatorASC 字段 SHALL 引用攻击者的 AbilitySystemComponent

#### Scenario: ActiveGameplayEffect 跟踪剩余时间
- **WHEN** 一个 duration=10 的 ActiveGameplayEffect 经过 3 秒
- **THEN** 其 TimeRemaining SHALL 约为 7 秒

### Requirement: Effect 堆叠规则
GameplayEffect MUST 包含 `StackingPolicy` 枚举字段（None / RefreshDuration / AddStacks）和 `maxStacks` 整数字段。当目标已存在同一 GameplayEffect 的 ActiveGameplayEffect 时，ASC SHALL 根据堆叠策略处理。

#### Scenario: None 策略 — 忽略重复施加
- **WHEN** 目标已存在效果 A 的 ActiveGameplayEffect，且再次施加效果 A（StackingPolicy=None）
- **THEN** ASC SHALL 忽略此次施加，不创建新实例也不修改现有实例

#### Scenario: RefreshDuration 策略 — 刷新剩余时间
- **WHEN** 目标已存在效果 A（duration=10，已过 7 秒，剩余 3 秒）的 ActiveGameplayEffect，且再次施加效果 A（StackingPolicy=RefreshDuration）
- **THEN** 现有 ActiveGameplayEffect 的 TimeRemaining SHALL 被重置为 10 秒

#### Scenario: AddStacks 策略 — 增加层数
- **WHEN** 目标已存在效果 A（当前 1 层，maxStacks=5）的 ActiveGameplayEffect，且再次施加效果 A（StackingPolicy=AddStacks）
- **THEN** 现有 ActiveGameplayEffect 的 CurrentStacks SHALL 变为 2
- **AND** TimeRemaining SHALL 被刷新

#### Scenario: AddStacks 达到上限后不再增加
- **WHEN** 目标已存在效果 A（当前 5 层，maxStacks=5）的 ActiveGameplayEffect，且再次施加效果 A（StackingPolicy=AddStacks）
- **THEN** CurrentStacks SHALL 保持 5 不变
- **AND** TimeRemaining SHALL 被刷新

### Requirement: 条件化效果施加（Application Tags）
GameplayEffect MUST 包含 `applicationRequiredTags` 和 `applicationBlockedTags` 列表（类型为 `List<GameplayTagSO>`）。ASC 在施加效果前 MUST 检查目标 TagComponent：目标必须拥有所有 requiredTags 且不拥有任何 blockedTags，否则 SHALL 拒绝施加。

#### Scenario: 目标满足所有条件标签时效果正常施加
- **WHEN** GameplayEffect 要求目标拥有 Tag_Poisonable，且目标 TagComponent 拥有 Tag_Poisonable
- **THEN** 效果 SHALL 正常施加

#### Scenario: 目标缺少必需标签时效果被拒绝
- **WHEN** GameplayEffect 要求目标拥有 Tag_Poisonable，但目标 TagComponent 没有该标签
- **THEN** 效果 SHALL 不被施加

#### Scenario: 目标拥有阻止标签时效果被拒绝
- **WHEN** GameplayEffect 的 applicationBlockedTags 包含 Tag_Immune_Poison，且目标 TagComponent 拥有 Tag_Immune_Poison
- **THEN** 效果 SHALL 不被施加

### Requirement: Effect 授予标签
GameplayEffect MUST 包含 `grantedTags` 列表（类型为 `List<GameplayTagSO>`）。当 Duration/Infinite 效果被施加时，ASC SHALL 将 grantedTags 添加到目标 TagComponent；当效果被移除时，SHALL 从 TagComponent 移除这些标签。

#### Scenario: Duration 效果施加时授予标签
- **WHEN** ASC 施加一个 grantedTags 包含 Tag_State_Burning 的 Duration 效果
- **THEN** 目标 TagComponent SHALL 拥有 Tag_State_Burning

#### Scenario: Duration 效果移除时撤销标签
- **WHEN** 一个授予了 Tag_State_Burning 的 Duration 效果到期被移除
- **THEN** 目标 TagComponent SHALL 不再拥有 Tag_State_Burning

### Requirement: ASC Effect 生命周期管理
AbilitySystemComponent MUST 在 Update 中 tick 所有活跃的 ActiveGameplayEffect：更新剩余时间、处理周期 Tick、移除到期效果。ASC MUST 提供 `RemoveActiveEffect` 方法用于手动移除效果。

#### Scenario: ASC 每帧更新所有活跃效果
- **WHEN** ASC 拥有 3 个活跃的 ActiveGameplayEffect
- **THEN** 每帧 Update 中 SHALL 对每个实例执行时间递减和周期检查

#### Scenario: 到期效果在 Tick 中被自动移除
- **WHEN** 一个 ActiveGameplayEffect 的 TimeRemaining 降至 0 或以下
- **THEN** ASC SHALL 在当前帧 Tick 中移除该效果并清理其属性修改器和授予标签

#### Scenario: 手动移除效果
- **WHEN** 外部系统调用 ASC.RemoveActiveEffect 传入一个 ActiveGameplayEffect
- **THEN** 该效果 SHALL 被立即从活跃列表移除，属性修改器和授予标签 SHALL 被清理
