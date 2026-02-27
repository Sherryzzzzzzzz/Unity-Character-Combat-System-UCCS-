## ADDED Requirements

### Requirement: 伤害施加调用方向正确
`AttackEvent` 的所有形状检测方法（ExecuteSphere、ExecuteCapsule、ExecuteCone）中调用 `ApplyGameplayEffect` 时，MUST 在目标 ASC 上调用方法并传入攻击者 ASC 作为参数，即 `targetASC.ApplyGameplayEffect(effect, ownerASC)`。

#### Scenario: 球形范围攻击施加伤害到目标
- **WHEN** AttackEvent 使用 Sphere 形状检测到目标
- **THEN** 目标的 AttributeSet.Health 减少，攻击者的 Health 不变

#### Scenario: 锥形范围攻击施加伤害到目标
- **WHEN** AttackEvent 使用 Cone 形状且目标在角度范围内
- **THEN** 目标的 AttributeSet.Health 减少，攻击者的 Health 不变

#### Scenario: 胶囊范围攻击施加伤害到目标
- **WHEN** AttackEvent 使用 Capsule 形状检测到目标
- **THEN** 目标的 AttributeSet.Health 减少，攻击者的 Health 不变

### Requirement: 武器碰撞管线施加伤害
当 MeleeWeapon 的碰撞触发器击中带有 HurtBoxManager 的目标时，在格挡/弹反判定通过后，MUST 对目标施加伤害。伤害通过 `AbilitySystemComponent.ApplyGameplayEffect` 施加。

#### Scenario: 武器击中未格挡目标 — 施加伤害和受击反应
- **WHEN** MeleeWeapon 的 OnTriggerEnter 触发并命中带有 HurtBoxManager 的目标
- **AND** 目标未处于格挡、弹反或无敌状态
- **THEN** 目标的 AttributeSet.Health 按伤害公式减少
- **AND** 目标播放受击动画

#### Scenario: 武器击中格挡中目标 — 不施加伤害
- **WHEN** MeleeWeapon 击中目标且目标有 guardingTag
- **THEN** 目标的 AttributeSet.Health 不变

#### Scenario: 武器击中弹反中目标 — 不施加伤害并触发弹反
- **WHEN** MeleeWeapon 击中目标且目标有 perfectParryTag 或 normalParryTag
- **THEN** 目标的 AttributeSet.Health 不变
- **AND** 攻击者获得 parrySuccessTag 瞬态标签

### Requirement: MeleeWeapon 持有攻击者 ASC 引用
MeleeWeapon MUST 在初始化时获取攻击者的 AbilitySystemComponent 引用，以便在碰撞时传递给 HurtBoxManager 进行伤害施加。

#### Scenario: AttackEvent 初始化 MeleeWeapon 时传递 ASC
- **WHEN** AttackEvent.OnStart 初始化 MeleeWeapon 组件
- **THEN** MeleeWeapon 的 _ownerASC 字段 SHALL 被设置为攻击者的 AbilitySystemComponent

#### Scenario: MeleeWeapon 碰撞时传递攻击者 ASC 给 HurtBoxManager
- **WHEN** MeleeWeapon.OnTriggerEnter 检测到命中
- **THEN** 调用 HurtBoxManager.ProcessHit 时 SHALL 传递攻击者的 AbilitySystemComponent

### Requirement: 单一伤害施加 API
AbilitySystemComponent MUST 只保留 `ApplyGameplayEffect(GameplayEffect, AbilitySystemComponent)` 作为唯一的伤害施加方法。`ApplyEffect(GameplayEffectSpec)` 和 `GameplayEffectSpec` 类 SHALL 被移除。

#### Scenario: ApplyGameplayEffect 是唯一伤害入口
- **WHEN** 任何系统需要对目标施加伤害
- **THEN** SHALL 通过 `targetASC.ApplyGameplayEffect(effect, attackerASC)` 调用

### Requirement: AbilitySystemComponent 空值防护
AbilitySystemComponent.Awake MUST 检查 AttributeSet 组件是否存在，缺失时 SHALL 输出警告日志。

#### Scenario: 缺少 AttributeSet 时输出警告
- **WHEN** AbilitySystemComponent 所在 GameObject 没有 AttributeSet 组件
- **THEN** 控制台 SHALL 输出 LogWarning 提示缺少 AttributeSet

### Requirement: 标签比较使用引用而非字符串
AbilitySystemComponent 中的标签判定 MUST 使用 GameplayTagSO 引用比较（`==`），而非字符串比较（`tag.name == "xxx"`）。

#### Scenario: 眩晕标签通过引用匹配触发能力打断
- **WHEN** TagComponent 添加了与 stunnedTag 引用相同的 GameplayTagSO
- **THEN** AbilitySystemComponent SHALL 打断当前能力

#### Scenario: stunnedTag 未赋值时输出警告
- **WHEN** AbilitySystemComponent 的 stunnedTag 字段为 null
- **THEN** Awake 时 SHALL 输出 LogWarning 提示 stunnedTag 未配置
