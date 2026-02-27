## ADDED Requirements

### Requirement: IGameplayCue 接口
系统 MUST 提供 `IGameplayCue` 接口，定义 Cue 的三种回调方法：`OnExecute(GameObject target)`（Instant 效果触发）、`OnAdd(GameObject target)`（Duration/Infinite 效果施加时触发）、`OnRemove(GameObject target)`（Duration/Infinite 效果移除时触发）。实现该接口的 MonoBehaviour SHALL 通过 `cueTag` 字段标识自身处理哪个标签的 Cue。

#### Scenario: IGameplayCue 实现定义完整回调
- **WHEN** 一个 MonoBehaviour 实现 IGameplayCue 接口
- **THEN** 该组件 SHALL 包含 OnExecute、OnAdd、OnRemove 三个方法实现

### Requirement: GameplayCueManager 单例管理器
系统 MUST 提供 `GameplayCueManager` MonoBehaviour 单例。GameplayCueManager SHALL 维护一个标签到 IGameplayCue 实现列表的注册表。IGameplayCue 组件 SHALL 在 Awake/OnEnable 时自动向 GameplayCueManager 注册。

#### Scenario: GameplayCueManager 场景中唯一
- **WHEN** 场景中存在 GameplayCueManager 实例
- **THEN** 通过 GameplayCueManager.Instance SHALL 获取该实例

#### Scenario: IGameplayCue 自动注册到 Manager
- **WHEN** 一个实现 IGameplayCue 的 MonoBehaviour OnEnable 时
- **THEN** 该 Cue SHALL 被注册到 GameplayCueManager 的注册表中，以其 cueTag 为 key

### Requirement: GameplayEffect 包含 cueTag 配置
GameplayEffect ScriptableObject MUST 包含可选的 `cueTag` 字段（GameplayTagSO 引用）。cueTag 为 null 时表示该效果不触发任何 Cue。

#### Scenario: 在 Inspector 中配置效果的 cueTag
- **WHEN** 设计师在 GameplayEffect 的 Inspector 中为 cueTag 赋值 Tag_Cue_FireBurst
- **THEN** 该引用 SHALL 在运行时可被 ASC 读取并传递给 CueManager

#### Scenario: cueTag 为 null 不触发 Cue
- **WHEN** GameplayEffect 的 cueTag 为 null
- **AND** 该效果被施加
- **THEN** ASC SHALL 不通知 GameplayCueManager

### Requirement: ASC 效果施加时通知 CueManager
AbilitySystemComponent 在施加效果时 MUST 通知 GameplayCueManager。对于 Instant 效果，SHALL 调用 CueManager 的 Execute 分发（触发 OnExecute）；对于 Duration/Infinite 效果施加时，SHALL 调用 Add 分发（触发 OnAdd）；效果移除时，SHALL 调用 Remove 分发（触发 OnRemove）。

#### Scenario: Instant 效果触发 OnExecute Cue
- **WHEN** ASC 施加一个 Instant 效果且 cueTag=Tag_Cue_HitSpark
- **THEN** GameplayCueManager SHALL 找到注册了 Tag_Cue_HitSpark 的 IGameplayCue 并调用其 OnExecute

#### Scenario: Duration 效果施加触发 OnAdd Cue
- **WHEN** ASC 施加一个 Duration 效果且 cueTag=Tag_Cue_Burning
- **THEN** GameplayCueManager SHALL 调用对应 IGameplayCue 的 OnAdd

#### Scenario: Duration 效果移除触发 OnRemove Cue
- **WHEN** ASC 移除一个 cueTag=Tag_Cue_Burning 的 Duration 效果
- **THEN** GameplayCueManager SHALL 调用对应 IGameplayCue 的 OnRemove

#### Scenario: CueManager 未找到匹配 Cue 时静默忽略
- **WHEN** ASC 施加的效果 cueTag=Tag_Cue_Unregistered 且无对应 IGameplayCue 注册
- **THEN** SHALL 不产生错误，静默忽略
