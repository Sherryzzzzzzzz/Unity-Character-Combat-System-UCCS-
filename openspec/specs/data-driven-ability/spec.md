## MODIFIED Requirements

### Requirement: GameplayAbility 从 SO 数据初始化
GameplayAbility 运行时实例 MUST 能够从 GameplayAbilitySO 数据资产初始化其配置字段（cooldown、标签列表、canBeInterrupted、costEffect）。GameplayAbility MUST 提供 `InitializeFromData(GameplayAbilitySO)` 方法，该方法 SHALL 同时初始化 CostEffect 字段。

#### Scenario: 从 SO 初始化能力运行时参数
- **WHEN** 调用 GameplayAbility.InitializeFromData(abilitySO)，其中 abilitySO.cooldown=3
- **THEN** GameplayAbility 的 Cooldown SHALL 被设置为 3

#### Scenario: 从 SO 初始化标签配置
- **WHEN** 调用 InitializeFromData(abilitySO)，其中 abilitySO.activationRequiredTags 包含 Tag_HasWeapon
- **THEN** GameplayAbility 的 ActivationRequiredTags SHALL 包含 Tag_HasWeapon

#### Scenario: 从 SO 初始化 CostEffect
- **WHEN** 调用 InitializeFromData(abilitySO)，其中 abilitySO.costEffect 引用了一个 Instant GameplayEffect
- **THEN** GameplayAbility 的 CostEffect SHALL 引用同一个 GameplayEffect

### Requirement: GameplayAbilitySO 数据资产
系统 MUST 提供 `GameplayAbilitySO` ScriptableObject 类，作为能力的数据资产。GameplayAbilitySO SHALL 包含以下可在 Inspector 中配置的字段：abilityName（字符串）、cooldown（float）、activationRequiredTags（List\<GameplayTagSO\>）、activationBlockedTags（List\<GameplayTagSO\>）、grantedTags（List\<GameplayTagSO\>）、canBeInterrupted（bool）、effectsToApply（List\<GameplayEffect\>）、costEffect（GameplayEffect，可选的资源消耗效果）。

#### Scenario: 在编辑器中创建能力资产
- **WHEN** 设计师在 Unity 编辑器中通过 CreateAssetMenu 创建一个 GameplayAbilitySO 资产
- **THEN** SHALL 能够在 Inspector 中配置冷却时间、标签需求、授予标签、关联效果和 Cost Effect

#### Scenario: GameplayAbilitySO 包含 costEffect 字段
- **WHEN** GameplayAbilitySO 的 costEffect 引用了一个 Instant GameplayEffect
- **THEN** 该 GameplayEffect 引用 SHALL 在 Inspector 中可见且可配置
