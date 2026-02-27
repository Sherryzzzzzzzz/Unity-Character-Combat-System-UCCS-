## Why

当前项目的 GAS 系统、攻击判定管线和蓄力机制存在大量关键 Bug 和未完成的功能，导致战斗系统无法正常运作：

1. **伤害管线完全断裂**：武器碰撞路径（MeleeWeapon → HurtBoxManager）只播放受击动画但从不造成伤害；形状检测路径（Physics.Overlap）虽然调用了 ApplyGameplayEffect，但攻击者/目标对象被颠倒，实际是攻击者对自己造成伤害。
2. **蓄力攻击系统不可用**：HoldInput.ProcessInput() 从未被调用，LoopEvent 从错误的 GameObject 获取 PlayerController 导致循环永远无法中断，输入释放信号未传递到技能系统。
3. **GAS 系统大量死代码**：GameplayAbility 无任何子类实现、GameplayEffectSpec 从未被使用、Buff 无法影响属性、死亡/破韧事件无监听者、韧性破碎后永远无法恢复。

这些问题阻塞了所有后续战斗内容的开发，必须现在修复。

## What Changes

### 伤害管线统一与修复
- **BREAKING**: 统一武器碰撞和形状检测两条伤害路径，所有命中都经过同一条管线处理
- 修复 ApplyGameplayEffect 中攻击者/目标颠倒的致命 Bug
- 武器碰撞路径增加 GAS 伤害应用（调用 AbilitySystemComponent）
- 形状检测路径增加受击动画、防御/格挡/无敌检查、多次命中防护、图层过滤
- 形状检测改为持续检测（在攻击窗口期间每帧执行），而非仅在 OnStart 时单次执行

### 蓄力攻击系统修复
- 在 PlayerController.Update 中调用 HoldInput.ProcessInput()，激活蓄力标签授予逻辑
- 修复 LoopEvent.BreakConditionsMet() 使用 PlayerController.Instance 而非 GetComponent
- 在 PlayerAttackState 中增加按钮持续按住/释放状态追踪
- 为攻击 InputActionWatcher 连接 onLongPressEnd 事件

### GAS 系统完善
- 完善 GameplayEffect：增加持续时间类型（瞬时/持续/永久）、属性修饰器（加法/乘法）、标签授予/需求
- **BREAKING**: 移除死代码 GameplayEffectSpec，将其功能合并入 GameplayEffect 管线
- 实现 Buff 属性效果：BuffSO 能够修改 AttributeSet（攻击力加成、防御加成、持续伤害等）
- 连接 AttributeSet 事件：死亡处理、破韧硬直状态、韧性恢复机制
- 修复韧性恢复逻辑（破韧后可通过定时或条件恢复）
- 清理或实装 GameplayAbility（至少提供一个可用的基础实现框架）

### 拼刀系统与辅助修复
- 修复 ClashManager 重复协程导致的相机异常
- 统一 MeleeWeapon 和 ClashDetector 的拼刀检测，消除重复触发
- 修复 HitBoxEvent 无敌帧未引用计数、OnEnd 不关闭受击盒的问题
- 修复 HitReactionController 冻帧使用全局 Time.timeScale 的安全隐患

## Capabilities

### New Capabilities
- `unified-damage-pipeline`: 统一的伤害处理管线，所有命中（武器碰撞/形状检测）经过统一的伤害计算、防御检查和受击反馈流程
- `charge-attack-system`: 完整的蓄力攻击系统，包含输入检测、循环动画、释放判定的完整链路
- `gameplay-effect-system`: 完善的 GameplayEffect 系统，支持持续效果、属性修饰、标签关联
- `buff-attribute-system`: Buff 对属性的实际影响系统，BuffSO 能够修改 AttributeSet 数值
- `combat-event-handling`: 战斗事件响应系统（死亡处理、破韧硬直、韧性恢复）

### Modified Capabilities
（当前 openspec/specs/ 中无已有 spec，所有能力均为新建）

## Impact

### 受影响的核心文件
- `Assets/Scripts/GASSystem/` — AbilitySystemComponent, AttributeSet, TagComponent, GameplayAbility, GameplayEffectSpec 全部需要修改
- `Assets/Scripts/ScriptsObject/` — GameplayEffect.cs, AttackData.cs, BuffSO.cs 需要扩展
- `Assets/Scripts/EventFactory/Events/` — AttackEvent.cs, HitBoxEvent.cs, LoopEvent.cs 需要修复
- `Assets/Scripts/Attack And Hit/` — MeleeWeapon.cs, HurtBoxManager.cs, HitReactionController.cs, ClashManager.cs, ClashDetector.cs 需要修改
- `Assets/Scripts/Player/` — PlayerController.cs, PlayerAttackState.cs, PlayerSkillComponent.cs 需要修改
- `Assets/Scripts/Enemy/` — EnemyModel.cs, EnemySkillComponent.cs 需要适配

### 数据资产影响
- `Assets/ScriptObjects/GF/` — 所有 GameplayEffect 资产需要适配新字段（向后兼容，新字段有默认值）
- `Assets/ScriptObjects/Buff/` — BuffSO 资产需要配置属性效果
- `Assets/ScriptObjects/AttackData/` — AttackData 资产可能需要调整

### 风险
- GameplayEffect 字段扩展可能导致已序列化的 SO 资产需要重新配置
- 统一伤害管线是架构层变更，需要全面的运行时测试
- 蓄力系统修复需要验证技能编辑器中 LoopEvent 的配置是否兼容
