## ADDED Requirements

### Requirement: GameplayTagSO 支持父标签引用
GameplayTagSO ScriptableObject MUST 包含可选的 `parentTag` 字段（GameplayTagSO 引用）。parentTag 为 null 时表示该标签为根标签。通过 parentTag 形成标签层级树（如 State → State.Debuff → State.Debuff.Burning）。

#### Scenario: 创建子标签引用父标签
- **WHEN** 设计师创建 Tag_State_Debuff_Burning 并将 parentTag 设为 Tag_State_Debuff
- **THEN** Tag_State_Debuff_Burning.parentTag SHALL 引用 Tag_State_Debuff

#### Scenario: 根标签的 parentTag 为 null
- **WHEN** Tag_State 未设置 parentTag
- **THEN** Tag_State.parentTag SHALL 为 null

### Requirement: TagComponent 层级匹配查询
TagComponent MUST 提供 `HasTagOrChild(GameplayTagSO tag)` 方法。该方法 SHALL 检查当前活跃标签（activeTags + transientTags）中是否有任一标签等于指定标签或是其子标签（通过 parentTag 链向上追溯匹配）。

#### Scenario: 精确匹配返回 true
- **WHEN** TagComponent 拥有 Tag_State_Debuff_Burning
- **AND** 查询 HasTagOrChild(Tag_State_Debuff_Burning)
- **THEN** SHALL 返回 true

#### Scenario: 父标签匹配子标签返回 true
- **WHEN** TagComponent 拥有 Tag_State_Debuff_Burning（其 parentTag 为 Tag_State_Debuff）
- **AND** 查询 HasTagOrChild(Tag_State_Debuff)
- **THEN** SHALL 返回 true（因为 Burning 是 Debuff 的子标签）

#### Scenario: 祖先标签匹配孙标签返回 true
- **WHEN** TagComponent 拥有 Tag_State_Debuff_Burning（parentTag 链：Burning → Debuff → State）
- **AND** 查询 HasTagOrChild(Tag_State)
- **THEN** SHALL 返回 true

#### Scenario: 无关标签返回 false
- **WHEN** TagComponent 拥有 Tag_State_Debuff_Burning
- **AND** 查询 HasTagOrChild(Tag_Ability_Cooldown)
- **THEN** SHALL 返回 false

#### Scenario: 子标签查询父标签返回 false
- **WHEN** TagComponent 拥有 Tag_State_Debuff（不拥有 Burning）
- **AND** 查询 HasTagOrChild(Tag_State_Debuff_Burning)
- **THEN** SHALL 返回 false（HasTagOrChild 只向上匹配，不向下匹配）

### Requirement: HasTag 保持精确匹配语义
现有的 `HasTag(GameplayTagSO)` 方法 MUST 保持精确匹配语义不变，不受层级标签引入的影响。

#### Scenario: HasTag 不匹配子标签
- **WHEN** TagComponent 拥有 Tag_State_Debuff_Burning
- **AND** 查询 HasTag(Tag_State_Debuff)
- **THEN** SHALL 返回 false（精确匹配，不做层级检查）
