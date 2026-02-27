## Context

当前项目的 GAS 系统（AbilitySystemComponent + AttributeSet + TagComponent）存在多个关键缺陷，导致战斗伤害流程无法正常工作。

**现状问题：**
1. `AttackEvent` 的形状检测管线（Sphere/Capsule/Cone）中，调用 `ownerASC.ApplyGameplayEffect(effect, targetASC)` 时攻击者/目标参数传反——`this` 是攻击者但方法将 `this` 视为受击方
2. 武器碰撞管线（MeleeWeapon → HurtBoxManager.ProcessHit）只处理格挡/弹反和受击动画，不施加伤害
3. 形状检测管线直接施加伤害，但不走格挡/弹反判定
4. `GameplayEffectSpec` 类和 `ApplyEffect(spec)` 方法存在但从未被调用，与 `ApplyGameplayEffect` 形成冗余双接口
5. `HandleTagAdded` 使用脆弱的字符串比较 `tag.name == "State.Stunned"`
6. `AbilitySystemComponent.Awake` 对 `AttributeSet` 缺失无任何提示

**约束条件：**
- 不改变 ScriptableObject 数据结构（AttackData、GameplayEffect、GameplayTagSO）
- 不改变时间轴事件系统的工厂/注册架构
- 保持现有的格挡/弹反/拼刀逻辑不变
- 修复后两条管线（武器碰撞 + 形状检测）都需要能正确施加伤害

## Goals / Non-Goals

**Goals:**
- 修复 `AttackEvent` 中 `ApplyGameplayEffect` 的调用方向，使伤害正确施加到目标
- 在 `HurtBoxManager.ProcessHit` 中集成伤害施加，使武器碰撞管线也能扣血
- 移除未使用的 `GameplayEffectSpec` 和 `ApplyEffect(spec)` 方法，消除 API 混淆
- 为 `AbilitySystemComponent` 添加必要的空值检查和运行时警告
- 将字符串标签比较替换为可序列化的 `GameplayTagSO` 引用

**Non-Goals:**
- 不扩展 Buff 系统（BuffSO 不修改属性的问题留待后续）
- 不实现 GameplayAbility 子类
- 不重构 AttackEvent 的形状检测架构
- 不修改 AttributeSet 的事件订阅机制
- 不修改伤害公式本身

## Decisions

### 决策 1：统一伤害施加点 — 在 HurtBoxManager 中施加伤害

**选择**：将伤害施加逻辑放入 `HurtBoxManager.ProcessHit`，武器碰撞管线通过 `MeleeWeapon` 传递攻击者 ASC。

**替代方案**：
- *方案 A*：在 MeleeWeapon.OnTriggerEnter 中直接调用 ApplyGameplayEffect → 问题：绕过格挡/弹反判定，且与 HurtBoxManager 的职责重叠
- *方案 B*：让 HurtBoxManager 只发事件，由外部系统施加伤害 → 过度设计，目前不需要

**理由**：HurtBoxManager 已经是受击判定的中心节点（格挡、弹反、无敌检查都在此），伤害施加在这些检查之后自然发生。MeleeWeapon 需要持有攻击者 ASC 引用以便传递。

### 决策 2：修复 AttackEvent 调用方向而非修改 ApplyGameplayEffect 签名

**选择**：将 `ownerASC.ApplyGameplayEffect(effect, targetASC)` 改为 `targetASC.ApplyGameplayEffect(effect, ownerASC)`。

**替代方案**：
- *方案 A*：将 ApplyGameplayEffect 改为静态方法 → 改动更大，且实例方法语义更清晰（"目标.接收伤害"）

**理由**：`ApplyGameplayEffect` 的签名和实现本身是正确的（`this` 为目标，参数为攻击者），只是调用方传错了。修改调用方是最小改动。

### 决策 3：移除 GameplayEffectSpec 而非统一到 Spec 模式

**选择**：删除 `GameplayEffectSpec.cs` 和 `AbilitySystemComponent.ApplyEffect(spec)`，只保留 `ApplyGameplayEffect(effect, attackerASC)` 接口。

**替代方案**：
- *方案 A*：将所有调用迁移到 GameplayEffectSpec 模式 → 需要更多改动且 Spec 目前无人使用

**理由**：Spec 模式在整个代码库中零使用，而 `ApplyGameplayEffect` 是实际的伤害路径。保留单一接口减少混淆。

### 决策 4：通过序列化字段替换字符串标签比较

**选择**：在 `AbilitySystemComponent` 上新增 `[SerializeField] private GameplayTagSO stunnedTag` 字段，用引用比较替换 `tag.name == "State.Stunned"`。

**理由**：项目中所有标签系统（TagComponent、HurtBoxManager）均使用 GameplayTagSO 引用比较，保持一致。

## Risks / Trade-offs

- **[风险] 形状检测管线不经过格挡/弹反** → 本次修复范围内，形状检测管线（Sphere/Capsule/Cone）只修正调用方向，不加入格挡/弹反判定。这意味着非 WeaponCollider 类型的攻击仍会绕过格挡。→ 缓解：当前所有近战技能实际使用 WeaponCollider 形状，形状检测主要用于 AOE 效果，AOE 不受格挡是合理设计。
- **[风险] MeleeWeapon 需要传递攻击者 ASC** → MeleeWeapon 已有 `_ownerASC` 字段和 `Init(ASC)` 方法但未被调用。需要在 `AttackEvent.OnStart` 中调用 `weapon.Init(ownerASC)`。→ 缓解：改动极小，只需一行。
- **[风险] 删除 GameplayEffectSpec 可能影响未来扩展** → 如果将来需要更复杂的效果（持续时间、堆叠），可能需要重新引入类似概念。→ 缓解：当前 Spec 类完全未使用，删除后重新引入的成本很低。
- **[风险] stunnedTag 序列化字段需要在 Inspector 中赋值** → 如果忘记赋值，眩晕打断能力不会工作。→ 缓解：在 Awake 中添加空值警告日志。

## 需要修改的文件

| 文件 | 修改内容 |
|------|---------|
| `Assets/Scripts/EventFactory/Events/AttackEvent.cs` | 修正 3 处 ApplyGameplayEffect 调用方向；在 OnStart 中调用 weapon.Init(ownerASC) |
| `Assets/Scripts/Attack And Hit/Hit/HurtBoxManager.cs` | ProcessHit 中在格挡/弹反判定后新增伤害施加逻辑 |
| `Assets/Scripts/Attack And Hit/Attack/MeleeWeapon.cs` | OnTriggerEnter 中传递 _ownerASC 给 HurtBoxManager；移除未使用的 Init 方法冗余 |
| `Assets/Scripts/GASSystem/AbilitySystemComponent.cs` | 移除 ApplyEffect(spec) 和 CalculateDamage(spec)；新增 stunnedTag 序列化字段；HandleTagAdded 用引用比较；Awake 增加空值检查 |
| `Assets/Scripts/GASSystem/GameplayEffectSpec.cs` | 删除整个文件 |
| `Assets/Scripts/GASSystem/GameplayEffectSpec.cs.meta` | 删除对应 meta 文件 |
