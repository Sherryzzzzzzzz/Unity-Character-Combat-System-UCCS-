# 02 — 战斗系统

## 目录

1. [系统架构](#系统架构)
2. [攻击系统](#攻击系统)
3. [受击系统](#受击系统)
4. [拼刀系统 (Clash)](#拼刀系统-clash)
5. [格挡系统](#格挡系统)
6. [精准闪避系统](#精准闪避系统)
7. [HitStop / HitFeedback](#hitstop--hitfeedback)
8. [韧性 (Poise) 系统](#韧性-poise-系统)

---

## 系统架构

```
┌─────────────────────────────────────────────────────────────┐
│                     Skill Timeline                           │
│  (HitBoxEvent → AttackEvent → CancelEvent → ComboEvent ...) │
└──────────────┬──────────────────────────────────────────────┘
               │ 初始化攻击数据
               ▼
┌──────────────────────────┐    ┌──────────────────────────┐
│      MeleeWeapon          │    │    HurtBoxManager         │
│  - OnTriggerEnter         │───▶│  - ProcessHit()          │
│  - 拼刀检测 (优先)        │    │  - 格挡判定              │
│  - 伤害触发               │    │  - 精准闪避判定          │
│  - 攻击者反馈             │    │  - ASC.ApplyEffect       │
└──────────────────────────┘    └──────────┬───────────────┘
                                           │
                    ┌──────────────────────┼──────────────────────┐
                    ▼                      ▼                      ▼
            ┌──────────────┐    ┌──────────────────┐    ┌──────────────────┐
            │ ClashManager │    │ HitReactionCon-  │    │ HitFeedback-     │
            │ - 拼刀判定    │    │ troller          │    │ Manager          │
            │ - 对决镜头    │    │ - 受击动画       │    │ - 受击 VFX       │
            │ - 双方硬直    │    │ - 击退           │    │ - 受击 SFX       │
            └──────────────┘    │ - HitStop        │    │ - Hit Flash      │
                                └──────────────────┘    └──────────────────┘
```

---

## 攻击系统

### AttackData (攻击数据资产)

**文件**: `Assets/Scripts/ScriptsObject/AttackData.cs` (32行)

```csharp
public class AttackData : ScriptableObject
{
    public GameplayEffect effect;              // 命中时施加的 GE
    public GameplayEffect staggerEffect;       // 被格挡时对攻击者的硬直效果
    public GameplayEffect perfectDodgePunishEffect; // 被精准闪避时的惩罚效果
    public float hitForce;                     // 击退力度
    public AttackForceType forceType;          // Light / Medium / Heavy / Blow
    public AttackShape shape;                  // 攻击形状
    public float radius / length / angle;      // 形状参数
    public LayerMask hitLayerMask;             // 目标层过滤
}
```

**AttackForceType 与受击强度的映射**:
| ForceType | HitStrength | 受击时长 | 说明 |
|-----------|-------------|---------|------|
| Light | Light | 0.7s | 轻攻击 |
| Medium | Medium | 1.0s | 中等攻击 |
| Heavy | Heavy | 1.5s | 重攻击 |
| Blow | Blow | 3.0s | 吹飞攻击 |

### MeleeWeapon (近战武器碰撞体)

**文件**: `Assets/Scripts/Attack And Hit/Attack/MeleeWeapon.cs` (111行)

挂载在武器碰撞体上，`OnTriggerEnter` 触发攻击判定：

```
OnTriggerEnter
├─ 最近：拼刀检测 (Weapon层对Weapon层)
│   └─ ClashManager.ResolveClash()
├─ 层过滤 (hittableLayers)
├─ 重复命中过滤 (_collidersHitThisSwing)
├─ HurtBoxManager.ProcessHit() → 伤害/反馈
└─ 攻击者反馈 (HitStop + FOV Kick)
```

每个武器实例维护 `_collidersHitThisSwing` 列表，确保一次挥击不会重复命中同一目标。

### HurtBoxManager (受击盒管理)

负责接收命中事件，进行多层判定后施加效果：
```
ProcessHit(attackEvent, attackerRoot, attackerASC)
├─ 格挡判定 (defender TagComp.HasTagOrChild(guardTag))
│   ├─ 格挡成功: 施加 staggerEffect 给攻击者
│   └─ 继续伤害流程
├─ 精准闪避判定 (defender TagComp.HasTag(perfect_dodge_tag))
│   ├─ 闪避成功: 施加 perfectDodgePunishEffect 给攻击者
│   └─ 不施加伤害
├─ 正常受击:
│   ├─ HitReactionController.PlayHit()
│   ├─ TagComponent.AddTag(stunnedTag)  // 受击硬直标签
│   └─ ASC.ApplyGameplayEffect(damageEffect, attackerASC, targetASC)
└─ 伤害数字 (DamageNumberManager)
```

---

## 受击系统

### HitReactionController

**文件**: `Assets/Scripts/Attack And Hit/Hit/HitReactionController.cs` (267行)

核心特性：
- **受击强度优先级**: 较弱的受击不会打断正在播放的更强受击
- **四方向受击动画**: F(前)/B(后)/L(左)/R(右) × L/M/H/B 强度 = 16种动画
- **击退曲线**: 可配置 AnimationCurve 控制击退力度衰减
- **HitStop / HitFeedback**: 集成 HitStopController 和 HitFeedbackManager
- **相机震动**: Cinemachine Impulse Source
- **状态恢复**: 受击结束后自动恢复到 aim/ground/sky 状态

---

## 拼刀系统 (Clash)

### 核心机制

当双方武器碰撞体同时处于攻击状态（同帧进入 Weapon 层检测），触发拼刀：

```
MeleeWeapon.OnTriggerEnter
└─ other.layer == WeaponLayer  // 双方武器碰撞
    └─ ClashManager.ResolveClash(unitA, unitB)
```

### ClashManager

**文件**: `Assets/Scripts/Attack And Hit/ClashManager.cs` (141行)

```
ResolveClash
├─ 拼接特效 + 音效
├─ 双方冻结动画 (FreezeAnimation)
├─ 切换对决镜头 (Cinemachine 特写)
├─ 卡肉等待 (freezeDuration, 默认 0.15s)
├─ 计算拼刀结果:
│   levelDifference = levelA - levelB
│   高级方: StunDuration × (1 - diff × multiplier) → 更短硬直
│   低级方: StunDuration × (1 + diff × multiplier) → 更长硬直
├─ 双方恢复 + 击退 + 硬直
└─ 延迟切回主相机
```

### IClashable 接口

PlayerSkillComponent 和 EnemySkillComponent 都实现了该接口：
```csharp
public interface IClashable
{
    GameObject GetGameObject();
    int GetClashLevel();              // 攻击等级 (来自 AttackData.forceType)
    void FreezeAnimation();           // 冻结动画
    void ResumeAndExecuteClash(ClashResult result); // 恢复 + 硬直
}
```

### ClashResult

```csharp
public class ClashResult
{
    public float StunDuration;       // 硬直时间
    public float KnockbackForce;     // 击退力度
    public Vector3 KnockbackDirection; // 击退方向
}
```

---

## 格挡系统

基于标签驱动：
- 防御状态: `TagComponent.AddTag(guardTag)` (如 "State.Combat.Guarding")
- HurtBoxManager 检测到 guardTag → 调用格挡逻辑
- 格挡时为攻击者施加 `staggerEffect` (硬直效果)
- 攻防双方通过 GAS 效果系统弹性配置格挡结果

---

## 精准闪避系统

### DodgeAbility

**文件**: `Assets/Scripts/Player/DodgeAbility.cs` (133行)

核心机制：
```
1. 敌人攻击前 → StartPerfectWindow(duration)
   开启精准窗口 (默认 0.2s)

2. 窗口期间玩家按闪避 → AttemptDodge() → true
   ├─ 授予 perfectDodgeTag (瞬态, 默认 0.6s)
   └─ 施加 perfectDodgeSelfEffect (可选 GE)

3. HurtBoxManager 检测到攻击 + 防御者有 perfectDodgeTag
   ├─ 闪避成功: 不施加伤害
   └─ 为攻击者施加 perfectDodgePunishEffect (惩罚)
```

---

## HitStop / HitFeedback

### HitStop (卡肉)

独立于 Time.timeScale 的卡肉系统：
- **受击方卡肉**: 根据 attackData.forceType 应用不同程度
- **攻击方卡肉**: 较轻的卡肉效果，增加打击感
- 旧版 fallback: Time.timeScale = 0.01 冻帧（仅在 HitStopController 不存在时）

### HitFeedback

**文件**: `Assets/Scripts/Combat/HitFeedbackManager.cs`

集中管理命中反馈效果：
- VFX 粒子特效（根据 forceType 选择强度）
- SFX 音效
- Hit Flash（角色材质闪烁）

### 相机反馈

- **Cinemachine Impulse**: 受击时相机震动
- **FOV Kick**: 攻击命中时短暂的 FOV 变化 (`CameraImpactEffects`)
- **对决镜头**: 拼刀时自动切换特写机位

---

## 韧性 (Poise) 系统

### 机制

```
Poise (韧性值)  = AttributeValue(BaseValue + Modifiers)

受击时: ModifyPoise(-poiseDamage)
├─ Poise > 0: 不被打断 (可继续行动)
├─ Poise <= 0: 触发 OnPoiseBreak
│   ├─ _isBroken = true
│   └─ 强制进入硬直状态
└─ 未受击 3 秒后自动恢复: PoiseRecoverRate = 10/s
    恢复到满 → ResetPoise() → OnPoiseRecover
```

韧性系统的数值通过 GAS 的 AttributeModifier 管理，可通过 GE 进行 Buff/Debuff 修改。

### AttackData 中的 Poise Damage

```csharp
// GameplayEffect 中的 poiseDamage 字段
public float poiseDamage = 20f;
```

每次攻击命中的削韧量由 GE 配置决定，不同攻击可以有不同的削韧值。
