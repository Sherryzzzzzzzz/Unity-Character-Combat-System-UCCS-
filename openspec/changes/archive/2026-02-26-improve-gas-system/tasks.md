## 1. 基础数据结构

- [x] 1.1 创建 `AttributeModifier` 结构体和 `ModifierType` 枚举（Additive/Multiplicative），新建文件 `Assets/Scripts/GASSystem/AttributeModifier.cs`
- [x] 1.2 创建 `AttributeValue` 结构体，包含 baseValue、修改器列表、GetCurrentValue() 聚合计算方法、AddModifier/RemoveModifier 方法，新建文件 `Assets/Scripts/GASSystem/AttributeValue.cs`
- [x] 1.3 创建 `EffectAttributeModifier` 可序列化结构体（目标属性枚举 `GameplayAttribute`、修改器类型、修改值），用于 GameplayEffect 的 modifiers 配置，放入 `Assets/Scripts/GASSystem/AttributeModifier.cs`

## 2. AttributeSet 重构

- [x] 2.1 重构 `AttributeSet.cs`：将 AttackPower、Defense、HealthMax、PoiseMax 从 public float 改为 `AttributeValue` 结构体，提供兼容性属性访问器（getter 返回 GetCurrentValue()）
- [x] 2.2 保持 Health 和 Poise 为直接 float 字段，ModifyHealth/ModifyPoise 逻辑不变
- [x] 2.3 添加 `GetAttributeValue(GameplayAttribute)` 方法，根据枚举返回对应的 AttributeValue 引用，供 ASC 注册/移除修改器时使用

## 3. GameplayEffect 扩展

- [x] 3.1 在 `GameplayEffect.cs` 中添加 `DurationPolicy` 枚举（Instant/Duration/Infinite）和 `durationPolicy` 字段，默认值为 Instant
- [x] 3.2 添加 `duration`（float）和 `period`（float）字段，用于持续时间和周期 Tick 配置
- [x] 3.3 添加 `StackingPolicy` 枚举（None/RefreshDuration/AddStacks）、`stackingPolicy` 字段和 `maxStacks` 字段
- [x] 3.4 添加 `grantedTags`（List\<GameplayTagSO\>）字段，用于 Duration/Infinite 效果授予标签
- [x] 3.5 添加 `applicationRequiredTags` 和 `applicationBlockedTags`（List\<GameplayTagSO\>）字段，用于条件化施加
- [x] 3.6 添加 `modifiers`（List\<EffectAttributeModifier\>）字段，用于属性修改器配置

## 4. ActiveGameplayEffect 运行时实例

- [x] 4.1 创建 `ActiveGameplayEffect` 类，新建文件 `Assets/Scripts/GASSystem/ActiveGameplayEffect.cs`，包含：EffectData（GameplayEffect SO 引用）、InstigatorASC、TimeRemaining、CurrentStacks、周期计时器、已注册的 AttributeModifier 引用列表
- [x] 4.2 实现 `Tick(float deltaTime)` 方法：递减 TimeRemaining，处理周期计时和触发周期效果逻辑
- [x] 4.3 实现 `Refresh()` 和 `AddStack()` 方法，支持堆叠策略

## 5. AbilitySystemComponent Effect 生命周期

- [x] 5.1 在 `AbilitySystemComponent.cs` 中添加 `List<ActiveGameplayEffect> _activeEffects` 和按 GameplayEffect 查找的字典
- [x] 5.2 重构 `ApplyGameplayEffect` 方法：添加 Application Tags 检查（requiredTags/blockedTags），根据 DurationPolicy 分派 Instant 即时执行或创建 ActiveGameplayEffect
- [x] 5.3 实现 Instant 效果的新伤害公式：`finalDamage = (effect.damage × effect.damageMultiplier + attackerAttr.AttackPower - targetAttr.Defense)`，使用 AttributeValue 聚合值
- [x] 5.4 实现 Duration/Infinite 效果施加逻辑：创建 ActiveGameplayEffect、处理堆叠策略、注册属性修改器到目标 AttributeSet、添加 grantedTags 到目标 TagComponent
- [x] 5.5 添加 `Update` 方法中的 Effect Tick 循环：遍历 _activeEffects 调用 Tick、处理周期效果、移除到期效果
- [x] 5.6 实现 `RemoveActiveEffect(ActiveGameplayEffect)` 方法：从列表移除、注销属性修改器、移除 grantedTags
- [x] 5.7 实现周期 Tick 的效果执行逻辑（对目标施加周期伤害/治疗）

## 6. GameplayAbility 数据驱动

- [x] 6.1 创建 `GameplayAbilitySO` ScriptableObject，新建文件 `Assets/Scripts/ScriptsObject/GameplayAbilitySO.cs`，包含 abilityName、cooldown、标签列表、canBeInterrupted、effectsToApply 字段，添加 `[CreateAssetMenu]` 特性
- [x] 6.2 在 `GameplayAbility.cs` 中添加 `InitializeFromData(GameplayAbilitySO)` 方法，从 SO 数据初始化运行时参数
- [x] 6.3 在 `AbilitySystemComponent.cs` 中添加 `[SerializeField] List<GameplayAbilitySO> abilityDataList` 字段和 Start 时自动注册逻辑

## 7. Buff 系统迁移

- [x] 7.1 在 `BuffSO.cs` 的 BuffSO 类和 Buff 类上添加 `[System.Obsolete]` 特性
- [x] 7.2 确认 `TagComponent.ApplyBuff` 方法在 Obsolete 标记后仍正常工作（功能保留，仅产生编译警告）

## 8. 调用方适配

- [x] 8.1 验证 `HurtBoxManager.ProcessHit` 中的 `ApplyGameplayEffect` 调用与新方法签名兼容（签名未变，无需修改）
- [x] 8.2 验证 `AttackEvent` 中所有 `ApplyGameplayEffect` 调用与新方法签名兼容
- [x] 8.3 验证现有 GameplayEffect 资产（GreatSwordLight1/2/3 等）的 durationPolicy 默认为 Instant，行为不变

## 9. 编译验证与集成测试

- [x] 9.1 确保项目编译无错误（Obsolete 警告可接受）
- [x] 9.2 在编辑器中创建一个测试用 Duration GameplayEffect 资产（带 grantedTags 和 modifiers），验证 Inspector 序列化正常
- [x] 9.3 运行游戏，验证现有即时伤害流程不受影响（普通攻击命中扣血正常）
- [x] 9.4 测试 Duration 效果：施加一个带属性修改器的持续效果，验证属性值变化和到期自动移除
- [x] 9.5 测试周期效果：施加一个带 period 的持续效果，验证周期 Tick 正常触发
