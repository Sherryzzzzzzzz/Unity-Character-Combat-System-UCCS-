## ADDED Requirements

### Requirement: GameplayAbilitySO 包含 Cost Effect
GameplayAbilitySO MUST 包含可选的 `costEffect` 字段（GameplayEffect 引用）。costEffect SHALL 为 Instant 类型的 GameplayEffect，用于在能力提交时扣除资源（如 Health、或未来的 Stamina 属性）。costEffect 为 null 时表示该能力无资源消耗。

#### Scenario: 在 Inspector 中配置能力的 Cost Effect
- **WHEN** 设计师在 GameplayAbilitySO 的 Inspector 中为 costEffect 赋值一个 Instant GameplayEffect
- **THEN** 该引用 SHALL 在运行时可被 GameplayAbility 读取

#### Scenario: costEffect 为 null 表示无消耗
- **WHEN** GameplayAbilitySO 的 costEffect 为 null
- **THEN** 该能力激活时 SHALL 不进行资源消耗检查

### Requirement: GameplayAbility CheckCost 检查
GameplayAbility MUST 提供 `CheckCost()` 方法，在激活前检查角色是否有足够资源支付 Cost。CheckCost SHALL 模拟 costEffect 的属性扣除，验证目标属性不会降至 0 以下。如果 costEffect 为 null，CheckCost SHALL 返回 true。

#### Scenario: 资源充足时 CheckCost 返回 true
- **WHEN** costEffect 扣除 Health 30 点，角色当前 Health=100
- **THEN** CheckCost() SHALL 返回 true

#### Scenario: 资源不足时 CheckCost 返回 false
- **WHEN** costEffect 扣除 Health 30 点，角色当前 Health=20
- **THEN** CheckCost() SHALL 返回 false

#### Scenario: 无 costEffect 时 CheckCost 返回 true
- **WHEN** costEffect 为 null
- **THEN** CheckCost() SHALL 返回 true

### Requirement: GameplayAbility CommitAbility 原子提交
GameplayAbility MUST 提供 `CommitAbility()` 方法，在能力确认激活时原子执行：扣除 Cost（施加 costEffect）并启动冷却计时。CommitAbility SHALL 在 CanActivate 检查通过后、Activate 执行前调用。

#### Scenario: CommitAbility 扣除 Cost 并启动冷却
- **WHEN** 能力的 costEffect 扣除 Health 20 点，cooldown=3 秒
- **AND** 调用 CommitAbility()
- **THEN** 角色 Health SHALL 减少 20
- **AND** 能力进入 3 秒冷却

#### Scenario: CommitAbility 无 Cost 时仅启动冷却
- **WHEN** costEffect 为 null，cooldown=5 秒
- **AND** 调用 CommitAbility()
- **THEN** 角色属性不变
- **AND** 能力进入 5 秒冷却

### Requirement: TryActivate 集成 Cost 检查和 Commit
GameplayAbility.TryActivate MUST 按以下顺序执行：1) 检查冷却 → 2) 检查标签 → 3) CheckCost() → 4) CommitAbility() → 5) Activate()。任一步骤失败 SHALL 中止后续流程。

#### Scenario: Cost 检查失败时能力不激活
- **WHEN** 角色 Health=10，costEffect 扣除 Health 30
- **AND** 调用 TryActivate()
- **THEN** SHALL 返回 false
- **AND** 角色 Health 不变，冷却不启动

#### Scenario: 完整流程成功执行
- **WHEN** 冷却已过，标签满足，Health=100，costEffect 扣除 Health 20
- **AND** 调用 TryActivate()
- **THEN** SHALL 返回 true
- **AND** 角色 Health 减少 20
- **AND** 冷却启动
- **AND** Activate() 被调用

### Requirement: GameplayAbility 从 SO 初始化 costEffect
InitializeFromData(GameplayAbilitySO) MUST 将 costEffect 引用传递到运行时 GameplayAbility 实例。

#### Scenario: 从 SO 初始化 costEffect
- **WHEN** GameplayAbilitySO 的 costEffect 引用了一个 GameplayEffect
- **AND** 调用 InitializeFromData(abilitySO)
- **THEN** 运行时 GameplayAbility 的 CostEffect SHALL 引用同一个 GameplayEffect
