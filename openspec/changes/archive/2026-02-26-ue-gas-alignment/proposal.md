## Why

当前 GAS 系统已具备基础的 Effect 生命周期、属性修改器和数据驱动能力框架，但与 UE GAS 相比仍缺少若干核心机制：效果缺乏动态数值计算（如按能力等级缩放、捕获施加时属性快照），能力没有资源消耗（耐力/法力）和提交机制，属性变更缺少通用事件通知，标签系统不支持层级匹配，也没有 GameplayCue 来解耦视觉/音效反馈。这些缺失限制了战斗系统的深度设计能力。本次变更将补齐这些关键差距，使系统更接近 UE GAS 的能力表达力。

## What Changes

- 引入 `GameplayEffectSpec` 运行时规格对象，支持动态数值计算、施加时属性快照（Captured Attributes）和自定义 Magnitude 计算
- 为 GameplayAbility 添加资源消耗（Cost）和提交（Commit）机制，支持耐力/法力等资源检查与扣除
- 实现 `GameplayCue` 系统，通过标签驱动 VFX/SFX 播放，解耦效果逻辑与视觉表现
- 扩展标签系统支持层级匹配（如 `State.Debuff.*` 匹配所有 Debuff 子标签）
- 为 AttributeSet 添加通用 `OnAttributeChanged` 事件通知机制
- 为 ActiveGameplayEffect 引入 `EffectHandle` 标识符，支持外部追踪和按句柄移除效果

## Capabilities

### New Capabilities
- `gameplay-effect-spec`: GameplayEffectSpec 运行时规格对象，支持动态 Magnitude 计算、属性快照捕获、施加上下文（等级、施加者属性）
- `ability-cost-commit`: GameplayAbility 的资源消耗检查与提交机制，支持 Cost Effect 和原子化的 Commit 流程
- `gameplay-cue`: GameplayCue 系统，通过标签驱动的视觉/音效反馈解耦机制
- `hierarchical-tags`: 标签层级匹配系统，支持父子标签关系和通配符查询
- `attribute-change-events`: AttributeSet 通用属性变更事件通知
- `effect-handle`: ActiveGameplayEffect 的唯一标识句柄，支持外部追踪和按句柄移除

### Modified Capabilities
- `gameplay-effect-lifecycle`: ApplyGameplayEffect 需要适配 GameplayEffectSpec 作为中间层，Effect 施加流程需要经过 Spec 创建 → 应用的两步流程
- `data-driven-ability`: GameplayAbility 激活流程需要整合 Cost 检查和 Commit 步骤

## Impact

- **GASSystem/**: `AbilitySystemComponent.cs` 需要大幅扩展（Spec 创建、Commit 流程、Cue 分发、Effect Handle 管理）；`GameplayAbility.cs` 需要添加 Cost/Commit 机制；`TagComponent.cs` 需要支持层级匹配；`AttributeSet.cs` 需要添加通用变更事件；`ActiveGameplayEffect.cs` 需要添加 Handle 标识
- **新建文件**: `GameplayEffectSpec.cs`、`GameplayCueManager.cs`、`GameplayCue.cs`（或接口）
- **ScriptsObject/**: `GameplayEffect.cs` 需要添加 CueTags 和 Magnitude 配置；`GameplayAbilitySO.cs` 需要添加 costEffect 字段
- **EventFactory/**: 现有 AttackEvent 中的 `ApplyGameplayEffect` 调用需要适配新的 Spec 流程
- **Attack And Hit/**: HurtBoxManager 的 ProcessHit 需要适配
- **依赖**: 无新外部依赖
