## Why

当前 GAS 系统已实现基础的 GameplayEffect 生命周期、Spec 快照、Handle 系统、GameplayCue 和 Ability Cost/Commit 流程。但与 UE GAS 相比，仍缺少多项核心机制：1) 效果聚合仅按 SO 引用键控，无法支持同一效果来自不同施加者的独立实例（如多个敌人同时施加燃烧）；2) 无 PreAttributeChange 钩子，属性变更前无法拦截或修正（如生命值钳位）；3) 无 Ability 取消/阻止标签系统，效果和能力无法通过标签自动取消其他能力；4) 能力生命周期过于简单，缺少 AbilityTask 异步任务机制。本次变更旨在补全这些关键差距。

## What Changes

- 重构效果聚合键（Effect Aggregation Key）：将 `_effectLookup` 从按 GameplayEffect SO 键控改为按 EffectAggregationKey（effect + source + policy）键控，支持 AggregateBySource/AggregateByTarget 策略，允许同一 GameplayEffect SO 从不同施加者产生独立的 ActiveGameplayEffect 实例
- 添加属性变更拦截钩子（PreAttributeChange / PostGameplayEffectExecute）：在 AttributeSet 中添加 PreAttributeChange 委托，在属性修改生效前允许外部逻辑钳位或修改 delta 值；添加 PostGameplayEffectExecute 回调用于响应 Instant 效果执行后的后处理
- 实现 Ability 取消/阻止标签系统：在 GameplayEffect 和 GameplayAbilitySO 上添加 cancelAbilitiesWithTag 和 blockAbilitiesWithTag 字段，ASC 在施加效果/激活能力时根据这些标签自动取消或阻止其他能力
- 实现 AbilityTask 异步任务框架：提供 GameplayAbilityTask 基类和常用任务（WaitDelay、WaitGameplayEvent、WaitTagAdded），使能力可以等待异步条件完成后再继续执行

## Capabilities

### New Capabilities
- `effect-aggregation`: 效果聚合键系统 — 支持 AggregateBySource/AggregateByTarget/None 策略，允许同一 GameplayEffect SO 产生多个独立运行时实例
- `pre-attribute-change`: 属性变更拦截钩子 — PreAttributeChange 委托和 PostGameplayEffectExecute 回调
- `ability-cancel-block-tags`: 能力取消/阻止标签系统 — cancelAbilitiesWithTag 和 blockAbilitiesWithTag 字段和 ASC 集成
- `ability-task`: AbilityTask 异步任务框架 — 基类和常用内置任务（WaitDelay/WaitGameplayEvent/WaitTagAdded）

### Modified Capabilities
- `gameplay-effect-lifecycle`: 效果施加流程需要支持新的聚合键查找逻辑，替代当前按 SO 键控的 `_effectLookup`
- `data-driven-ability`: GameplayAbilitySO 需要添加 cancelAbilitiesWithTag 和 blockAbilitiesWithTag 字段

## Impact

- **AbilitySystemComponent.cs**: 重构 `_effectLookup` 数据结构，修改 ApplyDurationEffect 逻辑，添加能力取消/阻止标签检查
- **GameplayEffect.cs**: 添加 aggregationPolicy、cancelAbilitiesWithTag、blockAbilitiesWithTag 字段
- **GameplayAbilitySO.cs / GameplayAbility.cs**: 添加 cancelAbilitiesWithTag、blockAbilitiesWithTag 字段和运行时检查
- **AttributeSet.cs / AttributeValue.cs**: 添加 PreAttributeChange 委托和 PostGameplayEffectExecute 回调
- **ActiveGameplayEffect.cs**: 添加 InstigatorASC 引用字段用于聚合键
- **新文件**: GameplayAbilityTask.cs（基类）、WaitDelayTask.cs、WaitGameplayEventTask.cs、WaitTagAddedTask.cs
