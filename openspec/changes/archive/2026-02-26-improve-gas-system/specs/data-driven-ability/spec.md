## ADDED Requirements

### Requirement: GameplayAbilitySO 数据资产
系统 MUST 提供 `GameplayAbilitySO` ScriptableObject 类，作为能力的数据资产。GameplayAbilitySO SHALL 包含以下可在 Inspector 中配置的字段：abilityName（字符串）、cooldown（float）、activationRequiredTags（List\<GameplayTagSO\>）、activationBlockedTags（List\<GameplayTagSO\>）、grantedTags（List\<GameplayTagSO\>）、canBeInterrupted（bool）、effectsToApply（List\<GameplayEffect\>，能力激活时施加的效果列表）。

#### Scenario: 在编辑器中创建能力资产
- **WHEN** 设计师在 Unity 编辑器中通过 CreateAssetMenu 创建一个 GameplayAbilitySO 资产
- **THEN** SHALL 能够在 Inspector 中配置冷却时间、标签需求、授予标签和关联效果

#### Scenario: GameplayAbilitySO 包含效果列表
- **WHEN** GameplayAbilitySO 的 effectsToApply 包含一个攻击力增益 GameplayEffect
- **THEN** 该 GameplayEffect 引用 SHALL 在 Inspector 中可见且可配置

### Requirement: GameplayAbility 从 SO 数据初始化
GameplayAbility 运行时实例 MUST 能够从 GameplayAbilitySO 数据资产初始化其配置字段（cooldown、标签列表、canBeInterrupted）。GameplayAbility MUST 提供 `InitializeFromData(GameplayAbilitySO)` 方法。

#### Scenario: 从 SO 初始化能力运行时参数
- **WHEN** 调用 GameplayAbility.InitializeFromData(abilitySO)，其中 abilitySO.cooldown=3
- **THEN** GameplayAbility 的 Cooldown SHALL 被设置为 3

#### Scenario: 从 SO 初始化标签配置
- **WHEN** 调用 InitializeFromData(abilitySO)，其中 abilitySO.activationRequiredTags 包含 Tag_HasWeapon
- **THEN** GameplayAbility 的 ActivationRequiredTags SHALL 包含 Tag_HasWeapon

### Requirement: ASC 批量注册能力
AbilitySystemComponent MUST 提供 `RegisterAbilitiesFromData(List<GameplayAbilitySO>)` 方法，或支持在 Inspector 中配置能力 SO 列表并在 Awake/Start 时自动注册。每个 GameplayAbilitySO SHALL 对应一个运行时 GameplayAbility 实例。

#### Scenario: ASC 从配置列表自动注册能力
- **WHEN** ASC 的 Inspector 中配置了 3 个 GameplayAbilitySO 资产
- **THEN** Start 时 ASC SHALL 自动注册 3 个对应的 GameplayAbility 运行时实例
- **AND** 每个实例可通过 abilityName 作为 key 激活

### Requirement: BuffSO 标记为废弃
BuffSO 类 MUST 被标记为 `[System.Obsolete("使用 GameplayEffect（Duration/Infinite 类型）替代")]`。现有 BuffSO 资产 SHALL 继续工作，但新功能 SHALL 使用 GameplayEffect 实现 Buff 功能。

#### Scenario: BuffSO 编译时产生废弃警告
- **WHEN** 代码中引用 BuffSO 类
- **THEN** 编译器 SHALL 输出 Obsolete 警告信息

#### Scenario: 现有 ApplyBuff 调用继续工作
- **WHEN** TagComponent.ApplyBuff(buffSO, instigator) 被调用
- **THEN** Buff 功能 SHALL 正常运行（标签授予、持续时间、堆叠）

### Requirement: GameplayEffect 替代 Buff 功能
GameplayEffect 与 Duration/Infinite DurationPolicy 结合 grantedTags 字段 MUST 能够完全替代 BuffSO 的功能：授予标签、持续时间管理、堆叠逻辑。设计师 SHALL 能够用 GameplayEffect 资产替代 BuffSO 资产来实现 Buff 效果。

#### Scenario: Duration GameplayEffect 实现限时 Buff
- **WHEN** 创建一个 DurationPolicy=Duration、duration=10、grantedTags=[Tag_State_AttackUp] 的 GameplayEffect
- **AND** ASC 施加该效果
- **THEN** 目标 SHALL 获得 Tag_State_AttackUp 标签持续 10 秒

#### Scenario: Infinite GameplayEffect 实现永久 Buff
- **WHEN** 创建一个 DurationPolicy=Infinite、grantedTags=[Tag_State_Guarding] 的 GameplayEffect
- **AND** ASC 施加该效果
- **THEN** 目标 SHALL 获得 Tag_State_Guarding 标签直到效果被手动移除

#### Scenario: GameplayEffect 堆叠替代 BuffSO 堆叠
- **WHEN** 创建一个 StackingPolicy=AddStacks、maxStacks=5 的 Duration GameplayEffect
- **AND** 连续施加 3 次
- **THEN** ActiveGameplayEffect 的 CurrentStacks SHALL 为 3
