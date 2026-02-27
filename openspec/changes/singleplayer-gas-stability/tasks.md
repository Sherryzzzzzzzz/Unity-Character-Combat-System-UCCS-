# Change: singleplayer-gas-stability

目标：修复并补强当前单机 GAS 实现，优先保证正确性与健壮性，随后提高可扩展性与开发体验。

高优先级（必须先做 — 确保正确性与健壮性）

1. 确保 Ability Commit 的原子性
- 描述：当能力的 costEffect 无法成功施加时，不应启动冷却、授予标签或设置当前能力。
- 涉及文件：Assets/Scripts/GASSystem/GameplayAbility.cs
- 实现要点：ApplyGameplayEffect 返回值检查（成功才启动冷却/授予标签）；对可能抛出的异常做捕获并回滚。
- 验收标准：资源不足时激活能力不会进入冷却，未授予 GrantedTags，ASC.CurrentAbility 未被设置。

2. 增强 null 检查并加防御性编程
- 描述：在关键路径加入空值检查以避免 NRE（NullReferenceException）。
- 涉及文件示例：
  - Assets/Scripts/GASSystem/AbilitySystemComponent.cs (RemoveActiveEffectInternal 移除 modifiers 部分)
  - Assets/Scripts/GASSystem/GameplayCueManager.cs (Cue 调用处)
  - 其它遍历/移除属性 modifier 的函数
- 实现要点：在使用 Attributes.GetAttributeValue 后检查 null，遍历列表前检查计数，出现异常时记录 Debug.LogWarning。
- 验收标准：对无效或缺失数据路径不会抛异常，控制台输出合理警告。

中优先级（提升功能一致性与可扩展性）

3. 改进 Effect 实例索引（避免仅用 SO 作为 key）
- 描述：支持同一 GameplayEffect SO 被多个 instigator/多次施加而不冲突，并保持 stackingPolicy 语义。
- 涉及文件：Assets/Scripts/GASSystem/AbilitySystemComponent.cs、Assets/Scripts/GASSystem/ActiveGameplayEffect.cs
- 实现要点：将 _effectLookup 改为以 Handle 为主索引或使用 Dictionary<GameplayEffect, List<ActiveGameplayEffect>>，按 stackingPolicy 在列表中决策。
- 验收标准：不同 instigator 对同一 target 施加相同 duration-effect 时分别存在独立的 ActiveGameplayEffect 实例，各自计时与移除。

4. 明确 AttributeBased Magnitude 的来源（Attacker 快照 vs Target 实时）
- 描述：明确并实现 CaptureSource.Attacker 使用施加时快照，CaptureSource.Target 在施加时以目标实时值计算。
- 涉及文件：Assets/Scripts/GASSystem/GameplayEffectSpec.cs、Assets/Scripts/GASSystem/AbilitySystemComponent.cs
- 实现要点：为 MakeEffectSpec/ApplyEffectSpec 增加 targetASC 参数或新增重载；调用链在需要时传入 targetASC。
- 验收标准：AttributeBased(Target) modifier 在施加时以目标当前属性为基础计算数值。

5. 引入 TargetData 抽象（支持 AOE / 命中点 / 多目标）
- 描述：统一描述目标集合、命中点和 HitInfo，使 Ability/Effect 支持 AOE 与命中点语义。
- 建议新增文件：Assets/Scripts/GASSystem/TargetData.cs
- 实现要点：TargetData 包含 GameObject[] targets, Vector3 hitPoint, Collider hitCollider, HitInfo 字段；为 ApplyGameplayEffect 提供重载以接受 TargetData。
- 验收标准：相同 API 可将 effect 施加到多个目标，实现简单 AOE 示例。

6. Tag 查询增强（支持 AND/OR/NOT）
- 描述：提供可组合的 TagQuery，方便在 Ability/Effect 的匹配条件中使用复杂逻辑。
- 建议新增文件/类型：TagQuery
- 实现要点：实现 requiredAll/requiredAny/blockedAny 的匹配逻辑；保持向后兼容 List<GameplayTagSO>。
- 验收标准：能用复合查询表达复杂条件并正确判断。

稳健性（回滚/一致性）

7. Effect 施加的原子性与回滚
- 描述：ApplyDurationEffect 在注册 modifiers 或授予 tags 出错时能回滚已做的更改，避免半挂状态。
- 涉及文件：Assets/Scripts/GASSystem/AbilitySystemComponent.cs
- 实现要点：在做多步改动前先建立临时记录，若失败则撤销已注册的 modifiers 与已授予的 tags 并返回失败。
- 验收标准：在模拟异常路径下不会留下残余 modifiers 或 tags，_activeEffects 与 _effectLookup 状态一致。

开发体验（可提升效率）

8. ASC / ActiveEffects Inspector（Editor 调试工具）
- 描述：在 Unity Inspector Play 模式下展示 AttributeSet、ActiveEffects、Current Tags、Current Ability，便于调试。
- 建议新增文件：Assets/Editor/GAS/AbilitySystemComponentInspector.cs
- 验收标准：选中含 ASC 的 GameObject 时可在 Inspector 中查看并展开 active effects 详情。

测试 & 验证

9. 添加 PlayMode / EditMode 测试用例（Unity Test Framework）
- 描述：覆盖关键路径防止回归。
- 建议测试用例：
  - Cost 测试：资源不足时 ability 不进入冷却；
  - Instant Effect 测试：Apply Instant 修改 Health；
  - Duration/Stack 测试：堆叠策略行为；
  - Transient Tag 测试：AddTransientTag / ConsumeTag / 缓存超时。
- 文件位置：Tests/PlayMode 或 Tests/EditMode
- 验收标准：测试通过且可在本地运行复现。

低优先级 / 可选

10. GameplayCue 扩展（层级匹配 / 多 cue 触发）
11. 性能优化（批量 Tick、减少分配）

——

要求：如需我把这些任务写入 openspec proposal/design/其他 artifact（例如 proposal.md 或 design.md），或把任务拆得更细以便直接分支开发，请回复说明。我已将任务写入：
openspec/changes/singleplayer-gas-stability/tasks.md

如果需要我也可以为每项生成更细的子任务（例如按文件逐行列出修改点）。