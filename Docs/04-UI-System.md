# 04 — UI 系统

## 目录

1. [UI 架构](#ui-架构)
2. [PlayerHUD](#playerhud)
3. [生命值系统](#生命值系统)
4. [韧性与体力条](#韧性与体力条)
5. [技能槽 UI](#技能槽-ui)
6. [状态效果 UI](#状态效果-ui)
7. [伤害数字](#伤害数字)
8. [目标信息](#目标信息)
9. [Boss 血条](#boss-血条)

---

## UI 架构

```
┌─────────────────────────────────────────────────────┐
│                   CombatHUD                          │
│  ┌──────────────┐  ┌─────────┐  ┌────────────────┐ │
│  │ PlayerHUD    │  │ Target  │  │ StatusEffect   │ │
│  │ - HealthBar  │  │ InfoUI  │  │ ListUI         │ │
│  │ - PoiseBar   │  │         │  │                │ │
│  │ - StaminaBar │  └─────────┘  └────────────────┘ │
│  │ - SkillSlots │                                   │
│  └──────────────┘                                   │
│                                                     │
│  ┌──────────────────────────────────────────────┐   │
│  │            World Space UI                      │   │
│  │  EnemyWorldHealthBar │ BossHealthBar          │   │
│  │  DamageNumberManager │ StatusEffectListUI     │   │
│  └──────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────┘
```

UI 分为两大类别：
- **Screen Space UI**: 玩家 HUD、目标信息
- **World Space UI**: 敌人头顶血条、伤害数字、Boss 血条

---

## PlayerHUD

**文件**: `Assets/Scripts/UI/PlayerHUD.cs` / `PlayerHUDController.cs`

玩家 HUD 的主控制器，管理：
- 玩家血条 (Health Bar)
- 韧性条 (Poise Bar)
- 体力条 (Stamina Bar)
- 技能槽 (Skill Slots)

通过监听 `AttributeSet.OnAttributeChanged` 事件驱动 UI 更新：

```csharp
// 伪代码
PlayerAttributes.OnAttributeChanged += (attr, oldVal, newVal) =>
{
    switch (attr)
    {
        case GameplayAttribute.Health: healthBar.UpdateValue(newVal); break;
        case GameplayAttribute.Poise: poiseBar.UpdateValue(newVal); break;
        case GameplayAttribute.Stamina: staminaBar.UpdateValue(newVal); break;
    }
};
```

---

## 生命值系统

### HealthBarController

**文件**: `Assets/Scripts/UI/HealthBarController.cs`

管理血条的显示逻辑：
- 当前值/最大值显示
- 平滑过渡动画
- 低血量警告特效
- 伤害闪烁效果 (Damage Flash)

### EnemyWorldHealthBar

**文件**: `Assets/Scripts/UI/EnemyWorldHealthBar.cs`

挂载在敌人头顶的 World Space 血条：
- 朝向相机 (Billboard)
- 仅在战斗中显示
- 血量平滑过渡

### HealthBarPool

**文件**: `Assets/Scripts/UI/HealthBarPool.cs`

血条对象池，避免频繁 Instantiate/Destroy。

### HealthBarStyleSO

**文件**: `Assets/Scripts/UI/HealthBarStyleSO.cs`

ScriptableObject 资产，定义血条视觉样式（颜色、大小、字体等）。

---

## 韧性与体力条

### PoiseBarUI

**文件**: `Assets/Scripts/UI/PoiseBarUI.cs`

独立显示韧性值进度条，支持：
- 破韧状态高亮
- 韧性恢复动画

### 体力条 (集成在 PlayerHUD 中)

显示当前体力/最大体力，消耗时闪烁提示。

---

## 技能槽 UI

### SkillSlotUI

**文件**: `Assets/Scripts/UI/SkillSlotUI.cs`

显示技能图标 + 冷却状态：
- 技能图标 (从 GameplayAbilitySO 读取)
- CD 遮罩动画
- 充能次数显示
- CD 剩余时间数字

通过 `GameplayAbility.GetCooldownInfo()` 获取统一的冷却数据：
```csharp
SkillCooldownInfo info = ability.GetCooldownInfo();
// info.IsOnCooldown, info.RemainingTime, info.RemainingCharges, ...
```

---

## 状态效果 UI

### StatusEffectListUI

**文件**: `Assets/Scripts/UI/StatusEffectListUI.cs`

显示角色身上所有活跃 Buff/Debuff 的图标列表：
- 监听 ASC 的 GE 施加/移除事件
- 每个图标显示堆叠层数和剩余时间
- 支持 Buff/Debuff 颜色区分

---

## 伤害数字

### DamageNumberManager

**文件**: `Assets/Scripts/UI/DamageNumberManager.cs`

浮动伤害数字系统：
- 根据伤害类型选择颜色/大小
- 暴击特效变体
- 对象池管理
- 弹出动画 (随机偏移 + 淡出)

---

## 目标信息

### TargetInfoUI

**文件**: `Assets/Scripts/UI/TargetInfoUI.cs`

显示锁定目标的信息：
- 目标名称
- 目标血条
- 目标韧性条

### EnemyHealthBinder

**文件**: `Assets/Scripts/UI/EnemyHealthBinder.cs`

动态绑定敌人 AttributeSet 到 UI 元素。

---

## Boss 血条

### BossHealthBar

**文件**: `Assets/Scripts/UI/BossHealthBar.cs`

Boss 专用的醒目血条：
- 多阶段血量显示
- Boss 名称 + 称号
- 登场/退场动画
- 阶段切换特效

---

## UI 更新机制

UI 的更新主要依赖两种方式：

### 1. 事件驱动 (Event-Driven)
```csharp
// AttributeSet 中的事件
OnAttributeChanged += (attr, oldVal, newVal) => UpdateUI();
OnDeath += () => ShowDeathScreen();
OnPoiseBreak += () => ShowPoiseBreakEffect();

// ASC 中的事件
OnGameplayEffectAppliedToSelf += (asc, spec, handle) => RefreshStatusList();
OnTagCountChanged += (tag, count) => UpdateStatusIcons();
```

### 2. 每帧轮询 (Polling)
```csharp
// CD 更新
void Update() {
    cdFill = ability.GetCooldownRemaining() / ability.Cooldown;
}
```
