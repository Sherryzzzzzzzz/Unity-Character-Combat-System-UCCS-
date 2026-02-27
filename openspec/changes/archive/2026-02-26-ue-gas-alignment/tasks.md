## 1. 基础设施 — Magnitude 计算与 EffectAttributeModifier 扩展

- [x] 1.1 在 `Assets/Scripts/GASSystem/AttributeModifier.cs` 中添加 `MagnitudeCalculation` 枚举（Static / AttributeBased / Custom）和 `CaptureSource` 枚举（Attacker / Target）
- [x] 1.2 扩展 `EffectAttributeModifier` 结构体，添加 `magnitudeCalculation`、`captureAttribute`、`captureSource` 和 `customCalculation`（ScriptableObject 引用）字段
- [x] 1.3 新建 `Assets/Scripts/GASSystem/IMagnitudeCalculation.cs`，定义 `IMagnitudeCalculation` 接口，包含 `float CalculateMagnitude(GameplayEffectSpec spec)` 方法
- [x] 1.4 在 `Assets/Scripts/GASSystem/AttributeModifier.cs` 的 `GameplayAttribute` 枚举中添加 `Health` 和 `Poise` 值

## 2. GameplayEffectSpec 核心

- [x] 2.1 新建 `Assets/Scripts/GASSystem/GameplayEffectSpec.cs`，实现 GameplayEffectSpec 类：持有源 GameplayEffect 引用、施加者 ASC 引用、`capturedAttackerAttributes` 字典、`magnitudeOverrides` 字典
- [x] 2.2 实现 GameplayEffectSpec 构造函数：自动捕获施加者 ASC 的所有 GameplayAttribute 聚合值到 capturedAttackerAttributes
- [x] 2.3 实现 `SetMagnitudeOverride(int modifierIndex, float value)` 方法
- [x] 2.4 实现 `float GetMagnitude(int modifierIndex)` 方法：按优先级解析 Override → Custom → AttributeBased → Static

## 3. Effect Handle 系统

- [x] 3.1 在 `Assets/Scripts/GASSystem/ActiveGameplayEffect.cs` 中添加静态全局计数器 `_nextHandle` 和只读 `int Handle` 属性，构造函数中自增赋值
- [x] 3.2 在 `Assets/Scripts/GASSystem/AbilitySystemComponent.cs` 中实现 `RemoveActiveEffectByHandle(int handle)` 方法，遍历 _activeEffects 查找并移除

## 4. ASC 施加流程重构 — Spec 中间层

- [x] 4.1 在 `Assets/Scripts/GASSystem/AbilitySystemComponent.cs` 中实现 `MakeEffectSpec(GameplayEffect effect)` 方法，创建 GameplayEffectSpec 并捕获当前 ASC 属性
- [x] 4.2 实现 `int ApplyEffectSpec(GameplayEffectSpec spec)` 方法，包含 Application Tags 检查、DurationPolicy 分派、返回 Handle（Duration/Infinite 返回正值，Instant 返回 0，拒绝返回 -1）
- [x] 4.3 重构 `ApplyGameplayEffect(GameplayEffect, AbilitySystemComponent)` 为便捷方法，内部创建 Spec 并委托给 ApplyEffectSpec
- [x] 4.4 重构 `ExecuteInstantEffect` 使用 GameplayEffectSpec 的快照属性值和 GetMagnitude 进行伤害计算
- [x] 4.5 重构 `ApplyDurationEffect` 使用 GameplayEffectSpec 的 GetMagnitude 解析修改器值
- [x] 4.6 重构 `ExecutePeriodicTick` 使用 ActiveGameplayEffect 关联的 GameplayEffectSpec 快照值

## 5. GameplayCue 系统

- [x] 5.1 新建 `Assets/Scripts/GASSystem/IGameplayCue.cs`，定义接口：OnExecute(GameObject, GameplayEffectSpec)、OnAdd(GameObject, GameplayEffectSpec)、OnRemove(GameObject)
- [x] 5.2 新建 `Assets/Scripts/GASSystem/GameplayCueManager.cs`，实现单例 MonoBehaviour：Dictionary<GameplayTagSO, IGameplayCue> 注册表、RegisterCue/UnregisterCue、ExecuteCue/AddCue/RemoveCue 分发方法
- [x] 5.3 在 `Assets/Scripts/ScriptsObject/GameplayEffect.cs` 中添加 `cueTag`（GameplayTagSO）字段
- [x] 5.4 在 ASC 的 ApplyEffectSpec 中集成 Cue 分发：Instant 调用 ExecuteCue、Duration/Infinite 调用 AddCue
- [x] 5.5 在 ASC 的 RemoveActiveEffectInternal 中集成 Cue 分发：移除时调用 RemoveCue

## 6. 层级标签匹配

- [x] 6.1 在 `Assets/Scripts/ScriptsObject/GameplayTagSO.cs` 中添加可选的 `parentTag`（GameplayTagSO 引用）字段
- [x] 6.2 在 `Assets/Scripts/GASSystem/TagComponent.cs` 中实现 `HasTagOrChild(GameplayTagSO tag)` 方法：遍历所有 activeTags 和 transientTags，检查每个标签的 parentTag 链是否包含目标标签

## 7. 属性变更事件

- [x] 7.1 在 `Assets/Scripts/GASSystem/AttributeSet.cs` 中添加 `event Action<GameplayAttribute, float, float> OnAttributeChanged`
- [x] 7.2 在 `Assets/Scripts/GASSystem/AttributeValue.cs` 中添加变更通知回调（`Action<float, float> OnValueChanged`），在 AddModifier/RemoveModifier/BaseValue setter 中触发
- [x] 7.3 在 AttributeSet 初始化时为每个 AttributeValue 注册 OnValueChanged 回调，转发到 OnAttributeChanged 事件
- [x] 7.4 在 AttributeSet.ModifyHealth 和 ModifyPoise 中触发 OnAttributeChanged 事件（传递旧值和新值）

## 8. Ability Cost 与 Commit

- [x] 8.1 在 `Assets/Scripts/ScriptsObject/GameplayAbilitySO.cs` 中添加 `costEffect`（GameplayEffect 引用）字段
- [x] 8.2 在 `Assets/Scripts/GASSystem/GameplayAbility.cs` 中添加 `_costEffect` 字段，在 InitializeFromData 中从 SO 读取
- [x] 8.3 实现 `CheckCost()` 方法：若 _costEffect 为 null 返回 true；否则模拟检查资源是否足够
- [x] 8.4 实现 `CommitAbility()` 方法：施加 costEffect（若非 null）并记录 lastCastTime
- [x] 8.5 重构 `TryActivate()` 流程：冷却检查 → 标签检查 → CheckCost → ActivateInternal（内部调用 CommitAbility 替代直接设置 lastCastTime）

## 9. 集成验证

- [x] 9.1 更新 `Assets/Scripts/GASSystem/GASSystemTest.cs`，添加 EffectSpec 流程测试（按键 [I]：通过 MakeEffectSpec + ApplyEffectSpec 施加效果）
- [x] 9.2 更新 GASSystemTest，添加 Handle 移除测试（按键 [O]：施加 Duration 效果后通过 Handle 移除）
- [x] 9.3 确保所有现有 ApplyGameplayEffect 调用点（AttackEvent、HurtBoxManager、DefaultGameplayAbility）无需修改即可正常工作
