## 1. 清理冗余 API

- [x] 1.1 删除 `Assets/Scripts/GASSystem/GameplayEffectSpec.cs` 和对应的 `.meta` 文件
- [x] 1.2 移除 `AbilitySystemComponent` 中的 `ApplyEffect(GameplayEffectSpec)` 方法和 `CalculateDamage(GameplayEffectSpec)` 方法（`Assets/Scripts/GASSystem/AbilitySystemComponent.cs`）

## 2. 修复 AbilitySystemComponent 防御性检查与标签比较

- [x] 2.1 在 `AbilitySystemComponent` 中新增 `[SerializeField] private GameplayTagSO stunnedTag` 字段（`Assets/Scripts/GASSystem/AbilitySystemComponent.cs`）
- [x] 2.2 修改 `HandleTagAdded` 方法，将 `tag.name == "State.Stunned"` 替换为 `tag == stunnedTag` 引用比较（`Assets/Scripts/GASSystem/AbilitySystemComponent.cs`）
- [x] 2.3 在 `Awake` 中添加空值检查：若 `Attributes` 为 null 则 `Debug.LogWarning`；若 `stunnedTag` 为 null 则 `Debug.LogWarning`（`Assets/Scripts/GASSystem/AbilitySystemComponent.cs`）

## 3. 修复 AttackEvent 伤害调用方向

- [x] 3.1 修正 `ExecuteSphere` 中的调用：`ownerASC.ApplyGameplayEffect(attackData.effect, targetASC)` → `targetASC.ApplyGameplayEffect(attackData.effect, ownerASC)`（`Assets/Scripts/EventFactory/Events/AttackEvent.cs`）
- [x] 3.2 修正 `ExecuteCone` 中的调用：同上替换（`Assets/Scripts/EventFactory/Events/AttackEvent.cs`）
- [x] 3.3 修正 `ExecuteCapsule` 中的调用：同上替换（`Assets/Scripts/EventFactory/Events/AttackEvent.cs`）

## 4. 统一武器碰撞管线的伤害施加

- [x] 4.1 在 `AttackEvent.OnStart` 中，初始化 MeleeWeapon 后调用 `weapon.Init(ownerASC)` 传递攻击者 ASC（`Assets/Scripts/EventFactory/Events/AttackEvent.cs`）
- [x] 4.2 修改 `HurtBoxManager.ProcessHit` 签名，新增 `AbilitySystemComponent attackerASC` 参数，使其接收攻击者 ASC（`Assets/Scripts/Attack And Hit/Hit/HurtBoxManager.cs`）
- [x] 4.3 在 `HurtBoxManager.ProcessHit` 中，受击反应之后/同时调用 `GetComponent<AbilitySystemComponent>().ApplyGameplayEffect(hit.attackData.effect, attackerASC)` 施加伤害（`Assets/Scripts/Attack And Hit/Hit/HurtBoxManager.cs`）
- [x] 4.4 修改 `MeleeWeapon.OnTriggerEnter`，调用 `hurtBoxManager.ProcessHit` 时传递 `_ownerASC`（`Assets/Scripts/Attack And Hit/Attack/MeleeWeapon.cs`）

## 5. 验证

- [ ] 5.1 确认所有修改文件无编译错误（在 Unity 编辑器中打开项目验证）
- [ ] 5.2 在场景中测试：武器近战攻击正确扣除目标 Health，攻击者 Health 不变
- [ ] 5.3 在场景中测试：格挡时不扣血，弹反时不扣血且攻击者获得 parrySuccessTag
