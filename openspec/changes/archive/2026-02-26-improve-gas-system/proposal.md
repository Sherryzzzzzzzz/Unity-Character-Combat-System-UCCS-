## Why

当前项目中的 GAS（Gameplay Ability System）仅实现了基础框架：AbilitySystemComponent 只支持即时伤害施加，GameplayEffect 缺少持续时间和属性修改器支持，Buff 系统与 Effect 系统各自独立未统一，能力（Ability）无法通过数据驱动创建。这些限制使得设计师难以通过编辑器配置复杂的战斗效果（如持续伤害、属性增益/减益、条件触发效果），制约了战斗系统的深度和可扩展性。现在完善 GAS 系统，可以为后续增加更多技能、敌人类型和战斗机制奠定坚实基础。

## What Changes

- 扩展 `GameplayEffect` ScriptableObject，支持即时/持续/周期三种持续时间策略，以及属性修改器（加法/乘法）
- 在 `AbilitySystemComponent` 中实现完整的 Effect 生命周期管理：申请、激活、周期tick、移除、堆叠处理
- 统一 Buff 系统和 Effect 系统：将 `BuffSO` 的功能迁移为 `GameplayEffect` 的持续时间类型，消除两套并行机制
- 扩展 `AttributeSet`，支持动态属性修改器（Base + 加法修改器 + 乘法修改器 = 当前值），而非仅支持直接修改
- 为 `GameplayAbility` 增加 ScriptableObject 数据资产支持，使能力可在编辑器中配置和自动注册
- 为 `GameplayEffect` 增加 Application Tag 需求（需要/阻止标签），支持条件化效果施加
- 利用现有的 `GameplayEffect.damageMultiplier` 字段，将其整合到伤害计算公式中

## Capabilities

### New Capabilities
- `gameplay-effect-lifecycle`: GameplayEffect 的完整生命周期管理，包括持续时间策略（即时/持续/周期）、堆叠规则、条件标签和属性修改器
- `attribute-modifier-system`: AttributeSet 的动态属性修改器框架，支持 Base/Additive/Multiplicative 修改器的注册、移除和聚合计算
- `data-driven-ability`: GameplayAbility 的 ScriptableObject 数据资产化，支持编辑器配置、自动注册和 Buff-Effect 统一模型

### Modified Capabilities
- `unified-damage-pipeline`: 伤害计算公式需要整合属性修改器系统的聚合值和 GameplayEffect.damageMultiplier，替代当前直接读取 AttributeSet 原始字段的方式

## Impact

- **GASSystem/**: `AbilitySystemComponent.cs`、`AttributeSet.cs`、`GameplayAbility.cs`、`TagComponent.cs` 均需要修改或扩展
- **ScriptsObject/**: `GameplayEffect.cs` 需要大幅扩展；`BuffSO.cs` 将被标记为废弃并逐步用 GameplayEffect 替代
- **Attack And Hit/**: `HurtBoxManager.cs` 中的 ProcessHit 方法需要适配新的 Effect 施加 API
- **EventFactory/**: `AttackEvent.cs` 中的 `ApplyGameplayEffect` 调用需要适配
- **ScriptObjects/**: 现有的 Buff 资产（`Buff_Parry_*`、`Buff_Buff_GuardStance` 等）需要迁移为 GameplayEffect 资产
- **Editor/**: 可能需要为扩展后的 GameplayEffect 添加自定义 Inspector
- **依赖**: 无新外部依赖，均基于现有 Unity + ScriptableObject 架构
