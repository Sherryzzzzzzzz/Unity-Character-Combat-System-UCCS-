## Why

GAS 系统（AbilitySystemComponent / AttributeSet / TagComponent）存在多个关键错误，导致战斗伤害计算完全失效。最严重的问题是 `AttackEvent` 中调用 `ApplyGameplayEffect` 时**攻击者和目标参数传反**，导致伤害施加在攻击者自身而非目标身上。此外，武器碰撞管线（MeleeWeapon → HurtBoxManager）与形状检测管线（AttackEvent 形状判定）之间存在割裂——前者只触发受击反馈但不扣血，后者只扣血但不走格挡/弹反逻辑。这些问题使得当前战斗系统在运行时无法正常工作，必须立即修复。

## What Changes

- **修复 ApplyGameplayEffect 调用方向**：将 `AttackEvent` 中所有 `ownerASC.ApplyGameplayEffect(effect, targetASC)` 改为 `targetASC.ApplyGameplayEffect(effect, ownerASC)`，确保伤害正确施加到目标身上
- **统一伤害施加管线**：在 `HurtBoxManager.ProcessHit` 中加入伤害施加逻辑，使武器碰撞管线在完成格挡/弹反判定后也能正确调用 `ApplyGameplayEffect` 扣血
- **清理冗余 API**：移除未使用的 `GameplayEffectSpec` 及其相关方法 `ApplyEffect(GameplayEffectSpec)`，消除双重伤害接口的混淆
- **增加防御性空值检查**：在 `AbilitySystemComponent.Awake` 中对 `AttributeSet` 缺失进行运行时警告，防止空引用异常
- **修复脆弱的字符串标签比较**：将 `AbilitySystemComponent.HandleTagAdded` 中的 `tag.name == "State.Stunned"` 改为共享 `GameplayTagSO` 引用比较

## Capabilities

### New Capabilities

- `unified-damage-pipeline`: 统一伤害施加流程，确保武器碰撞管线和形状检测管线都能正确完成伤害计算与施加

### Modified Capabilities

（无现有 spec 需要修改）

## Impact

- **核心受影响文件**：
  - `Assets/Scripts/EventFactory/Events/AttackEvent.cs` — 修复所有 ApplyGameplayEffect 调用
  - `Assets/Scripts/Attack And Hit/Hit/HurtBoxManager.cs` — 新增伤害施加逻辑
  - `Assets/Scripts/GASSystem/AbilitySystemComponent.cs` — 移除 ApplyEffect(spec)、增加空值检查、修复标签比较
  - `Assets/Scripts/GASSystem/GameplayEffectSpec.cs` — 整个文件移除
- **关联影响**：
  - `MeleeWeapon.cs` 的 `OnTriggerEnter` 需传递攻击者 ASC 信息给 `HurtBoxManager`
  - 场景中所有挂载 `AbilitySystemComponent` 的 GameObject 必须同时挂载 `AttributeSet`（已有前提，增加运行时验证）
- **无破坏性变更**：所有修复均为内部逻辑修正，不改变公共 API 签名或 ScriptableObject 数据结构
