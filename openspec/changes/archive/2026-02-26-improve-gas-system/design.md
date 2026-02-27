## Context

当前项目的 GAS 系统处于基础阶段：

- **AbilitySystemComponent** 只支持即时伤害（`ApplyGameplayEffect` 直接扣血/扣韧性），无持续效果管理
- **GameplayEffect** 是一个仅含 damage/poiseDamage/damageMultiplier 的轻量 ScriptableObject，damageMultiplier 未被使用
- **BuffSO + Buff + TagComponent** 构成了一套独立的 Buff 系统，仅支持授予标签和持续时间管理，不支持属性修改
- **AttributeSet** 属性值直接暴露为 public float 字段，无修改器概念
- **GameplayAbility** 是抽象 C# 类，无 ScriptableObject 数据资产支持，无法在编辑器中直观配置

系统之间存在概念重叠（Buff 和 Effect 本质相同但实现分离），且缺乏属性修改器框架来支持增益/减益效果。

## Goals / Non-Goals

**Goals:**
- 扩展 GameplayEffect 为完整的效果容器，支持即时/持续/周期三种持续时间策略
- 实现 AttributeSet 的属性修改器系统（Base + Additive + Multiplicative = CurrentValue）
- 将 Buff 系统统一到 GameplayEffect 框架下，消除双轨机制
- 在 AbilitySystemComponent 中实现 Effect 生命周期管理（申请、tick、移除、堆叠）
- 为 GameplayAbility 增加 ScriptableObject 数据驱动支持
- 保持与现有攻击/受击管线（MeleeWeapon → HurtBoxManager → ASC）的兼容

**Non-Goals:**
- 网络同步/多人预测机制（项目当前为单机）
- GameplayCue 系统（VFX/SFX 仍由 HitReactionController 和 EffectEvent 处理）
- 完全复制 UE GAS 架构（保持适合项目规模的轻量级设计）
- 修改技能时间轴编辑器（SkillEditorWindow）

## Decisions

### 决策 1：GameplayEffect 持续时间模型

**选择**: 在 GameplayEffect ScriptableObject 上添加 `DurationPolicy` 枚举（Instant / Duration / Infinite）和 `period` 字段

**理由**:
- 与 UE GAS 概念一致，降低学习成本
- Instant 效果保持当前即时伤害逻辑不变
- Duration/Infinite 效果由 ASC 管理生命周期
- 备选方案：创建独立的 DurationEffect 子类 —— 被否决，因为会增加资产类型复杂度

### 决策 2：属性修改器架构

**选择**: 在 AttributeSet 中为每个属性引入 `AttributeValue` 结构体（baseValue + List\<Modifier\>），通过 `GetCurrentValue()` 聚合计算

```
CurrentValue = (BaseValue + ΣAdditive) × (1 + ΣMultiplicative)
```

**理由**:
- 直接在 AttributeSet 内部实现，不需要额外管理类
- 修改器与 ActiveEffect 实例关联，Effect 移除时自动清理修改器
- 备选方案：独立的 AttributeModifierManager 组件 —— 被否决，增加不必要的组件间通信

### 决策 3：Buff 到 Effect 的统一

**选择**: 将 BuffSO 的功能迁移到 GameplayEffect（Duration/Infinite 类型），保留 BuffSO 类但标记为 `[System.Obsolete]`，通过适配层在 TagComponent 中支持两种调用

**理由**:
- 渐进式迁移，不会一次性破坏所有现有资产
- 新功能统一使用 GameplayEffect，旧 BuffSO 资产可后续逐个迁移
- 备选方案：立即删除 BuffSO —— 被否决，会破坏现有 Buff 资产和所有 ApplyBuff 调用点

### 决策 4：Effect 堆叠规则

**选择**: 在 GameplayEffect 上添加 `StackingPolicy`（None / RefreshDuration / AddStacks）和 `maxStacks` 字段，复用 BuffSO 已有的堆叠概念

**理由**:
- 与现有 BuffStackingType 语义一致，迁移直观
- 堆叠逻辑由 ASC 的 ActiveEffect 管理，不在 TagComponent 中

### 决策 5：ActiveGameplayEffect 运行时实例

**选择**: 创建 `ActiveGameplayEffect` 类作为运行时效果实例，由 ASC 持有并管理

**理由**:
- 分离数据（GameplayEffect SO）和运行时状态（剩余时间、当前层数、施加者引用）
- 类似 BuffSO/Buff 的关系，但统一在 ASC 管理下

### 决策 6：GameplayAbility ScriptableObject 化

**选择**: 创建 `GameplayAbilitySO` ScriptableObject 作为能力数据资产，保留 `GameplayAbility` 抽象类作为运行时逻辑基类，通过 `[SerializeReference]` 在 SO 中引用具体能力逻辑

**理由**:
- 编辑器友好：设计师可以在 Inspector 中配置冷却、标签、关联效果
- 保持代码灵活性：具体能力行为仍通过 C# 类实现
- 备选方案：完全放弃 C# 能力类，全部用 SO + 配置驱动 —— 被否决，动作游戏的能力逻辑通常需要代码灵活性

### 决策 7：条件化效果施加（Application Tags）

**选择**: 在 GameplayEffect 上添加 `applicationRequiredTags` 和 `applicationBlockedTags` 列表，ASC 在 ApplyGameplayEffect 时检查目标 TagComponent

**理由**:
- 轻量实现，复用现有 TagComponent 基础设施
- 支持设计需求如"免疫中毒状态时无法施加中毒效果"

## 文件变更清单

### 需要修改的现有文件
| 文件 | 变更内容 |
|------|----------|
| `Assets/Scripts/ScriptsObject/GameplayEffect.cs` | 添加 DurationPolicy、period、堆叠规则、属性修改器列表、Application Tags |
| `Assets/Scripts/GASSystem/AbilitySystemComponent.cs` | 添加 ActiveEffect 列表管理、Effect 生命周期（Apply/Tick/Remove）、修改后的伤害公式 |
| `Assets/Scripts/GASSystem/AttributeSet.cs` | 重构属性为 AttributeValue 结构体，添加修改器注册/聚合计算 |
| `Assets/Scripts/GASSystem/GameplayAbility.cs` | 适配 SO 数据引用 |
| `Assets/Scripts/GASSystem/TagComponent.cs` | 添加 BuffSO 到 GameplayEffect 的适配层 |
| `Assets/Scripts/ScriptsObject/BuffSO.cs` | 标记 `[Obsolete]` |
| `Assets/Scripts/Attack And Hit/Hit/HurtBoxManager.cs` | 适配新的 Effect 施加 API（如签名变化） |
| `Assets/Scripts/EventFactory/Events/AttackEvent.cs` | 适配新的 Effect 施加 API |

### 需要新建的文件
| 文件 | 说明 |
|------|------|
| `Assets/Scripts/GASSystem/ActiveGameplayEffect.cs` | Effect 运行时实例（时间、层数、修改器引用） |
| `Assets/Scripts/GASSystem/AttributeModifier.cs` | 属性修改器数据结构 |
| `Assets/Scripts/ScriptsObject/GameplayAbilitySO.cs` | 能力 ScriptableObject 数据资产 |

## Risks / Trade-offs

- **[AttributeSet 重构破坏性]** → 属性从直接 float 字段改为 AttributeValue 结构体，所有读取属性的代码需要适配。缓解：提供 `GetCurrentValue()` 方法并保留属性名兼容的 getter 属性
- **[Buff 迁移周期]** → 现有 BuffSO 资产需要逐个迁移为 GameplayEffect 资产。缓解：保留适配层，两套可并存运行
- **[Effect 生命周期性能]** → ASC 每帧 tick 所有 ActiveEffect。缓解：场景中角色数量有限（魂类游戏），性能影响可忽略
- **[序列化兼容性]** → GameplayEffect SO 字段扩展后，现有 .asset 文件可自动兼容（Unity 对新增 SerializedField 赋默认值）。风险低
- **[伤害公式变更]** → 整合 damageMultiplier 和修改器聚合值后，数值平衡需要重新调试。缓解：保持默认 damageMultiplier=1，修改器默认为空，不影响现有数值
