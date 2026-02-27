## Why

当前项目的 GAS（Gameplay Ability System）是一个轻量级实现：GameplayEffect 仅包含 damage/poiseDamage 两个字段，属性修改通过硬编码公式直接计算，Buff 系统虽有运行时骨架但无法实际修改属性，GameplayAbility 仅有抽象基类无具体子类，缺乏效果规格（EffectSpec）、属性修改器栈、持续/周期效果、Gameplay Cue 等核心机制。

随着战斗系统复杂度提升（需要支持多种 Buff 叠加、属性临时修改、条件触发效果、复杂伤害计算等），轻量级实现已无法满足需求。需要将 GAS 升级为接近 UE GAS 的完整架构，使其具备可扩展的属性修改器系统、完整的效果生命周期管理、以及丰富的 Ability 执行框架。

## What Changes

- **新增属性修改器系统**：为 AttributeSet 引入 Modifier 栈（Additive/Multiplicative/Override），支持 BaseValue 和 CurrentValue 分离，属性变更事件通知
- **重构 GameplayEffect 为完整效果系统**：支持 Instant/Duration/Infinite/Periodic 四种效果类型，引入 GameplayEffectSpec 运行时实例，支持效果叠加策略（StackingPolicy）
- **新增 Execution Calculation 管线**：替代当前硬编码的伤害公式，支持自定义 GameplayEffectExecutionCalculation 类进行复杂计算
- **完善 GameplayAbility 框架**：引入 Ability Cost（消耗效果）、AbilityTask（异步能力任务）、完整的激活/提交/结束生命周期
- **新增 Gameplay Cue 系统**：统一管理效果触发的视觉/音频反馈（粒子、音效、UI 提示等）
- **增强 Tag 系统**：支持层级标签（如 `Damage.Physical.Fire`）和复合标签查询（TagQuery：All/Any/None）
- **重构 Buff 系统与 GameplayEffect 统一**：当前 BuffSO 独立于 GameplayEffect，需将 Buff 统一为 Duration/Infinite 类型的 GameplayEffect，消除系统冗余
- **BREAKING**：`AbilitySystemComponent.ApplyGameplayEffect` 签名和行为将完全重构
- **BREAKING**：`GameplayEffect` ScriptableObject 结构将大幅扩展
- **BREAKING**：`AttributeSet` 的属性访问方式将改变（引入 BaseValue/CurrentValue）

## Capabilities

### New Capabilities
- `attribute-modifier-system`：属性修改器栈系统，支持 Base/Current 值分离、Additive/Multiplicative/Override 修改器、属性变更事件广播
- `gameplay-effect-lifecycle`：完整的 GameplayEffect 生命周期管理，包括 EffectSpec 实例化、Duration/Periodic/Infinite 效果类型、效果叠加策略、效果移除与清理
- `execution-calculation`：可扩展的效果执行计算管线，替代硬编码伤害公式，支持自定义 ExecutionCalculation 类捕获源/目标属性进行复杂运算
- `ability-framework`：完善的 GameplayAbility 执行框架，包括 AbilityCost、CommitAbility、AbilityTask 异步任务、输入绑定与能力激活策略
- `gameplay-cue`：Gameplay Cue 视觉/音频反馈系统，通过 Tag 匹配触发对应的 Cue 处理器（粒子特效、音效、UI 等）
- `tag-query`：增强型标签查询系统，支持层级标签结构和复合条件查询（AllMatch/AnyMatch/NoneMatch）

### Modified Capabilities
<!-- 当前 openspec/specs/ 下无已有规格文件，无需修改 -->

## Impact

### 受影响的代码
- **GASSystem/**：`AbilitySystemComponent.cs`、`AttributeSet.cs`、`GameplayAbility.cs`、`TagComponent.cs` 将被大幅重构
- **ScriptsObject/**：`GameplayEffect.cs`、`AttackData.cs`、`BuffSO.cs` 结构变更
- **Attack And Hit/**：`HurtBoxManager.cs`、`MeleeWeapon.cs` 中的效果应用调用需适配新 API
- **EventFactory/**：`AttackEvent.cs`、`HitBoxEvent.cs` 及对应 Factory 需适配新的效果应用接口
- **Player/**：`PlayerModel.cs`、`PlayerSkillComponent.cs` 中与 ASC 交互的代码需更新
- **Enemy/**：`EnemySkillComponent.cs` 需适配新的 Ability 激活方式

### 依赖与兼容性
- 所有现有的 ScriptableObject 资产（SkillTimelineAsset、AttackData 实例、GameplayEffect 实例）需要迁移适配
- BuffSO 系统将被 GameplayEffect Duration 类型取代，现有 Buff 资产需重新配置
- 无外部依赖新增，全部基于现有 Unity 技术栈实现
