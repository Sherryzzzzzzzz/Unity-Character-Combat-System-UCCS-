## Context

当前 GAS 系统已实现：GameplayEffect（Instant/Duration/Infinite + 堆叠 + 周期 Tick + Application Tags + 属性修改器）、AttributeSet（AttributeValue + Additive/Multiplicative 修改器聚合）、GameplayAbility（SO 数据驱动 + 冷却 + 标签门控）、TagComponent（活跃/瞬态标签 + Buff 管理）。

与 UE GAS 的关键差距集中在：效果数值全部静态配置（无动态计算），能力无资源消耗，无视觉反馈解耦，标签无层级匹配，属性变更无事件通知，效果实例无外部句柄。

## Goals / Non-Goals

**Goals:**
- 引入 GameplayEffectSpec 作为效果施加的中间层，支持动态 Magnitude 和属性快照
- 为 GameplayAbility 添加 Cost Effect 和 Commit 原子提交
- 实现轻量级 GameplayCue 系统（标签驱动 VFX/SFX）
- 扩展 TagComponent 支持层级匹配（`HasTagParent`）
- 为 AttributeSet 添加通用 OnAttributeChanged 事件
- 为 ActiveGameplayEffect 添加唯一 EffectHandle

**Non-Goals:**
- 网络同步/预测（单机项目）
- 完整的 UE Ability Task 系统（保持帧事件时间轴驱动技能的现有架构）
- 完整的 Targeting Data 系统（保持现有 AttackEvent + HurtBoxManager 流程）
- GameplayAbility 等级系统（后续按需添加）

## Decisions

### 决策 1：GameplayEffectSpec 架构

**选择**: 创建 `GameplayEffectSpec` 类作为效果施加的中间层。调用方先通过 `ASC.MakeEffectSpec(effect)` 创建 Spec，可选修改 Magnitude/Context，再通过 `ASC.ApplyEffectSpec(spec)` 施加。保留原 `ApplyGameplayEffect(effect, attackerASC)` 作为便捷方法（内部创建 Spec 后施加）。

**理由**:
- 向后兼容：现有调用点无需立即修改
- 灵活性：需要动态数值时使用 Spec 流程，简单场景继续用便捷方法
- 备选方案：直接修改 ApplyGameplayEffect 签名 — 否决，破坏性太大

### 决策 2：Magnitude 计算模型

**选择**: 在 GameplayEffect 的每个 EffectAttributeModifier 上添加可选的 `MagnitudeCalculation` 枚举（Static / AttributeBased / Custom）。Static 使用 SO 上的固定值；AttributeBased 从施加者/目标捕获指定属性值作为 Magnitude；Custom 通过 `IMagnitudeCalculation` 接口自定义。

**理由**:
- 覆盖 UE GAS 中最常用的三种 Magnitude 计算模式
- 不需要完整的 Scalable Float 曲线系统（超出项目需求）

### 决策 3：属性快照（Captured Attributes）

**选择**: GameplayEffectSpec 在创建时捕获施加者的相关属性值（如 AttackPower），存储为 `capturedAttackerAttributes` 字典。Instant 效果的伤害计算使用快照值，Duration 效果的周期 Tick 也使用快照值。

**理由**:
- 与 UE GAS 的 Capture 语义一致：效果的数值在施加时确定，不随施加者后续属性变化而变化
- 解决当前设计中"施加者被击杀后引用丢失"的潜在 null 问题

### 决策 4：Ability Cost 与 Commit

**选择**: 在 GameplayAbilitySO 上添加 `costEffect`（GameplayEffect 引用），在 GameplayAbility.TryActivate 中添加 `CheckCost() → CanActivate() → CommitAbility()` 流程。CommitAbility 扣除 Cost 并启动冷却。

**理由**:
- 与 UE GAS 的 CanActivateAbility → CommitAbility 流程一致
- Cost 复用 GameplayEffect 系统（Instant 效果扣除属性），无需独立资源系统

### 决策 5：GameplayCue 系统

**选择**: 创建 `GameplayCueManager`（MonoBehaviour 单例）和 `IGameplayCue` 接口。GameplayEffect 上添加 `cueTag`（GameplayTagSO），Effect 施加/移除时 ASC 通知 CueManager，CueManager 查找注册的 IGameplayCue 实现并调用 OnExecute/OnAdd/OnRemove。

**理由**:
- 轻量级实现，符合项目规模
- 通过标签查找 Cue 实现，与 UE 的标签驱动 Cue 分发一致
- 备选方案：事件总线模式 — 否决，标签查找更符合 GAS 习惯

### 决策 6：层级标签匹配

**选择**: 在 GameplayTagSO 上添加可选的 `parentTag` 引用字段。TagComponent 添加 `HasTagOrChild(tag)` 方法，检查是否拥有指定标签或其任意子标签。

**理由**:
- 最小侵入性：不改变现有标签存储结构，仅在 SO 上增加父引用
- 查询时遍历 activeTags 检查父链，性能对少量标签可接受
- 备选方案：字符串路径解析 — 否决，当前标签系统基于 SO 引用

### 决策 7：Effect Handle

**选择**: 在 ActiveGameplayEffect 上添加自增 `int Handle` 字段（全局计数器）。ASC 的 ApplyEffectSpec 返回 Handle，提供 `RemoveActiveEffectByHandle(int handle)` 方法。

**理由**:
- 简单高效，避免传递 ActiveGameplayEffect 引用
- 与 UE 的 FActiveGameplayEffectHandle 概念一致

## 文件变更清单

### 需要修改的现有文件
| 文件 | 变更内容 |
|------|----------|
| `Assets/Scripts/GASSystem/AbilitySystemComponent.cs` | 添加 MakeEffectSpec/ApplyEffectSpec、Cue 通知、Handle 查找、重构内部施加流程 |
| `Assets/Scripts/GASSystem/GameplayAbility.cs` | 添加 CheckCost/CommitAbility 流程 |
| `Assets/Scripts/GASSystem/ActiveGameplayEffect.cs` | 添加 Handle 字段和全局计数器 |
| `Assets/Scripts/GASSystem/TagComponent.cs` | 添加 HasTagOrChild 层级查询方法 |
| `Assets/Scripts/GASSystem/AttributeSet.cs` | 添加通用 OnAttributeChanged 事件 |
| `Assets/Scripts/GASSystem/AttributeModifier.cs` | 扩展 EffectAttributeModifier 添加 MagnitudeCalculation 配置 |
| `Assets/Scripts/ScriptsObject/GameplayEffect.cs` | 添加 cueTag 字段 |
| `Assets/Scripts/ScriptsObject/GameplayAbilitySO.cs` | 添加 costEffect 字段 |

### 需要新建的文件
| 文件 | 说明 |
|------|------|
| `Assets/Scripts/GASSystem/GameplayEffectSpec.cs` | 效果施加规格对象（上下文、快照、动态 Magnitude） |
| `Assets/Scripts/GASSystem/GameplayCueManager.cs` | Cue 分发管理器（单例） |
| `Assets/Scripts/GASSystem/IGameplayCue.cs` | Cue 接口（OnExecute/OnAdd/OnRemove） |
| `Assets/Scripts/GASSystem/IMagnitudeCalculation.cs` | 自定义 Magnitude 计算接口 |

## Risks / Trade-offs

- **[ApplyGameplayEffect 向后兼容]** → 保留便捷方法，内部转换为 Spec 流程。现有调用点无需修改。风险低
- **[层级标签性能]** → HasTagOrChild 需要遍历所有活跃标签并检查父链。缓解：魂类游戏角色标签数量有限（通常 <20），性能影响可忽略
- **[属性快照与实时值的语义差异]** → 使用快照后，Duration 效果的周期伤害不再受施加者后续属性变化影响。这是 UE GAS 的标准行为，但需要设计师理解
- **[Cost Effect 复用 GameplayEffect]** → Cost 使用 Instant 效果扣除属性（如 Health 或自定义 Stamina），需要确保 AttributeSet 包含对应属性。如果需要耐力属性，需要在 AttributeSet 中添加
