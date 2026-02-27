## Context

当前 GAS 系统已完成两个阶段的开发：Phase 1 建立了基础框架（AttributeSet/TagComponent/GameplayEffect/GameplayAbility），Phase 2 对齐了 UE GAS 的核心 Spec 流程（GameplayEffectSpec 快照、Handle 系统、GameplayCue、层级标签、属性变更事件、Ability Cost/Commit）。

现有限制：
1. `_effectLookup` 使用 `Dictionary<GameplayEffect, ActiveGameplayEffect>` 按 SO 引用键控 — 同一 GameplayEffect SO 只能存在一个 ActiveGameplayEffect，无法支持多施加者场景（如两个敌人同时对玩家施加燃烧）
2. 属性修改在 AttributeValue.AddModifier 中直接生效，无 PreAttributeChange 拦截点
3. 能力取消/阻止仅通过 stunnedTag 硬编码在 ASC 中，无通用标签驱动机制
4. 能力执行是完全同步的（Activate 立即返回），无法实现"等待 2 秒后爆炸"等延迟逻辑

## Goals / Non-Goals

**Goals:**
- 支持同一 GameplayEffect SO 从不同施加者产生独立的 ActiveGameplayEffect（效果聚合键）
- 提供 PreAttributeChange 钩子用于属性变更前拦截（钳位、免疫等）
- 实现标签驱动的能力取消/阻止系统
- 提供轻量级 AbilityTask 异步任务框架

**Non-Goals:**
- 不实现网络同步/预测（单机项目）
- 不实现 UE 的完整 GameplayEffectExecutionCalculation（保持现有伤害公式）
- 不实现效果等级（Level）和等级缩放
- 不实现 Tag Query（AND/OR/NOT 复合查询）— 保持简单列表
- 不修改现有伤害计算公式

## Decisions

### 决策 1：效果聚合键设计

**选择**: 引入 `EffectAggregationPolicy` 枚举（None / AggregateBySource / AggregateByTarget）作为 GameplayEffect SO 上的配置字段。将 `_effectLookup` 替换为 `List<ActiveGameplayEffect> _activeEffects`（已有）配合线性查找。聚合判定逻辑：查找现有 ActiveGameplayEffect 时，根据 policy 决定匹配条件 — None 匹配 effectData 相同即可（现有行为），AggregateBySource 还需匹配 InstigatorASC。

**理由**:
- 最小侵入修改：`_activeEffects` 列表已存在，仅需移除 `_effectLookup` 字典
- 线性查找对于活跃效果数量（通常 < 20）完全可接受
- 与 UE GAS 的 AggregateBySource 语义一致
- 备选方案：使用复合键字典 `Dictionary<(GameplayEffect, ASC), ActiveGameplayEffect>` — 否决，因为 None 策略下不需要 ASC 键，结构更复杂

### 决策 2：PreAttributeChange 钩子

**选择**: 在 AttributeSet 上添加 `Func<GameplayAttribute, float, float, float> PreAttributeChange` 委托。签名为 `(attribute, oldValue, proposedNewValue) => clampedNewValue`。在 AttributeValue.BaseValue setter 和 AddModifier/RemoveModifier 中，在实际赋值前调用此钩子。

**理由**:
- UE GAS 使用 PreAttributeChange 虚函数让 AttributeSet 子类修正属性值
- 使用委托而非虚函数，保持 AttributeSet 为具体类（Unity MonoBehaviour 不适合深继承）
- 钩子返回修正后的值，调用方用修正值替代原始值

### 决策 3：Ability 取消/阻止标签

**选择**: 在 GameplayEffect 和 GameplayAbilitySO 上各添加 `List<GameplayTagSO> cancelAbilitiesWithTag` 和 `List<GameplayTagSO> blockAbilitiesWithTag`。ASC 在效果施加授予标签时，遍历所有正在执行的能力，检查其 GrantedTags 是否与 cancelAbilitiesWithTag 匹配。阻止检查在 TryActivate 中增加：若 ASC 上当前有任何效果/能力的 blockAbilitiesWithTag 包含待激活能力的 GrantedTags，则拒绝激活。

**理由**:
- 与 UE GAS 的 Cancel/Block 语义一致
- 检查时机：Cancel 在效果/标签施加后立即执行，Block 在能力激活前检查
- 在 ASC 中集中管理，而非分散到各能力类中
- 备选：仅在 TagComponent.OnTagAdded 回调中处理 — 否决，因为需要 ASC 上下文来取消特定能力

### 决策 4：AbilityTask 异步任务框架

**选择**: 创建 `GameplayAbilityTask` 抽象基类，持有 ownerAbility 引用。任务通过 ASC 的 Update 循环 Tick。提供 `Action OnCompleted` 回调。内置三种任务：WaitDelayTask（等待指定秒数）、WaitGameplayEventTask（等待 ASC 上的 GameplayEvent 触发）、WaitTagAddedTask（等待 TagComponent 上特定标签添加）。

**理由**:
- UE GAS 的 AbilityTask 是异步能力的核心，但其实现依赖 UE 的 Latent Action 框架
- Unity 中使用简单的 Tick + 回调模式即可实现相同语义
- 任务注册到 ASC 的任务列表，在 Update 中统一 Tick，能力结束时自动清理
- 备选：使用 Coroutine — 否决，GameplayAbility 是纯 C# 类非 MonoBehaviour，无法直接启动协程

## 文件变更清单

### 需要修改的现有文件
| 文件 | 变更内容 |
|------|----------|
| `Assets/Scripts/ScriptsObject/GameplayEffect.cs` | 添加 aggregationPolicy、cancelAbilitiesWithTag、blockAbilitiesWithTag 字段 |
| `Assets/Scripts/GASSystem/AbilitySystemComponent.cs` | 移除 _effectLookup，重构 ApplyDurationEffect 聚合查找，添加任务 Tick、Cancel/Block 标签检查 |
| `Assets/Scripts/GASSystem/ActiveGameplayEffect.cs` | 添加 InstigatorASC 字段，构造函数传入 |
| `Assets/Scripts/GASSystem/AttributeSet.cs` | 添加 PreAttributeChange 委托 |
| `Assets/Scripts/GASSystem/AttributeValue.cs` | 在值变更路径中集成 PreAttributeChange 回调 |
| `Assets/Scripts/ScriptsObject/GameplayAbilitySO.cs` | 添加 cancelAbilitiesWithTag、blockAbilitiesWithTag 字段 |
| `Assets/Scripts/GASSystem/GameplayAbility.cs` | 添加 cancel/block 标签字段，InitializeFromData 读取，TryActivate 集成 block 检查 |

### 需要新建的文件
| 文件 | 说明 |
|------|------|
| `Assets/Scripts/GASSystem/GameplayAbilityTask.cs` | AbilityTask 抽象基类 |
| `Assets/Scripts/GASSystem/Tasks/WaitDelayTask.cs` | 等待延迟任务 |
| `Assets/Scripts/GASSystem/Tasks/WaitGameplayEventTask.cs` | 等待 GameplayEvent 任务 |
| `Assets/Scripts/GASSystem/Tasks/WaitTagAddedTask.cs` | 等待标签添加任务 |

## Risks / Trade-offs

- **[风险] 移除 _effectLookup 后查找性能** → 活跃效果通常 < 20 个，线性查找开销可忽略。若未来需要优化可改用多键字典
- **[风险] PreAttributeChange 递归调用** → 钩子内不应修改属性，仅返回钳位值。文档和注释明确说明
- **[风险] AbilityTask 生命周期泄漏** → 能力结束时 ASC 自动清理所有关联任务，任务持有弱引用到 ability
- **[权衡] Cancel/Block 检查频率** → Cancel 仅在新标签授予时检查（事件驱动），Block 仅在 TryActivate 时检查，不会影响帧率
