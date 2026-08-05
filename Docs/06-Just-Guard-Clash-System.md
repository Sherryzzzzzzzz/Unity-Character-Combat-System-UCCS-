# 06 — 鬼泣式拼刀 / 完美格挡 (Just Guard) / 反击系统

> 本文档描述本次「鬼泣化战斗手感」改造。以《鬼泣 5》Royal Guard / 拼刀为参考：
> **Just Guard（完美格挡）** = 攻击命中前极短窗口内格挡 → 无伤、零消耗、弹开攻击者、时间冻结、反击加成。
> **拼刀（Clash）** = 双方攻击同时相撞 → 火花四溅、慢动作定格、特写镜头、双方弹开。

---

## 1. 系统架构总览

```
                    攻击命中 (MeleeWeapon.OnTriggerEnter)
                                   │
                                   ▼
                     HurtBoxManager.ProcessHit
                                   │
        ┌──────────────┬───────────┼──────────────┬─────────────┐
        ▼              ▼           ▼              ▼             ▼
   Just Guard     完美弹反      普通弹反       格挡(Guard)    正常受击
  (justGuardTag) (perfectParry) (normalParry) (guardingTag)
        │                                                    │
        ▼                                                    ▼
  ┌──────────────┐                                    HandleBlockedHit
  │HandleJustGuard│                                    (减伤+韧性/体力消耗)
  └──────────────┘
   ├─ 0 伤害 / 0 韧性 / 0 体力（直接 return，不进入任何扣减）
   ├─ 攻击者被弹开：ParrySuccess Tag → Parryable 中断攻击 + 弹反硬直
   ├─ 攻击者被击退（力度 = forceType × justGuardPushMultiplier）
   ├─ 全局慢动作：TimeScaleDirector（时间冻结后回弹）
   ├─ 蓝白火花 + 冲击波 + 金属音 + 相机震动 + FOV
   └─ 授予反击标签 State.Counter.Ready（0.8s）
              │
              ▼（玩家下次攻击命中时）
     ApplyDamageToTarget → TryConsumeCounterTag
     └─ 伤害 × counterMultiplier（默认 1.5）并消耗标签
```

**拼刀路径**（独立于上述命中流程）：

```
武器碰撞 / 身体→武器检测 (MeleeWeapon / ClashDetector)
   → ClashManager.ResolveClash(IClashable A, IClashable B)
   ├─ 蓝白火花 + 冲击波 + 多角度火花喷射（clashSparkCount）
   ├─ 双金属音效（clashSound + clashSoundExtra）
   ├─ 全局慢动作定格（clashTimeScale=0.05，持续 freezeDuration+0.15s）
   ├─ 特写相机 + 震屏 + FOV Kick（原有）
   ├─ 冻结双方动画 → 恢复并施加 击退 + 硬直（按 clash level 差）
   └─ 恢复主相机 + 锁敌状态（原有）
```

---

## 2. Just Guard（完美格挡）—— 判定与效果

### 判定
- 进入格挡状态（`PlayerGuardState.Enter`）时，授予 `State.Guarding.JustGuard` 标签
- 窗口时长：`HurtBoxManager.justGuardWindow`（默认 **0.13s ≈ 8 帧@60fps**，鬼泣 Just Guard 约为 8 帧）
- 窗口内被命中 → `HurtBoxManager.ProcessHit` 优先走 `HandleJustGuard` 分支
- 窗口外被命中 → 落入完美弹反(0.18s)/普通弹反(0.35s)/普通格挡

### 效果（全部集中在 HandleJustGuard）
| 效果 | 实现 |
|------|------|
| 无伤 | 直接 return，不调用任何伤害/消耗 |
| 弹开攻击者 | 攻击者施加 `Event.ParrySuccess` → 其中断技能 + 弹反硬直动画 |
| 击退攻击者 | CharacterController 击退，力度按攻击 forceType 分级 × `justGuardPushMultiplier`(2) |
| 时间冻结 | `TimeScaleDirector.DoSlowMotion(0.15, 0.12, smooth)`——时间回弹手感 |
| 视觉 | `GlobalVFXPool.SpawnClashVFX`（蓝白火花 + 程序化冲击波）|
| 听觉 | `parrySuccessSound`（金属"叮"）|
| 相机 | ImpulseSource 震动 ×1.5 + FOV Kick(Heavy) |
| 反击状态 | 授予 `State.Counter.Ready`，持续 `counterWindowDuration`(0.8s) |

### 与弹反的区别
| | Just Guard | 完美弹反 | 普通弹反 |
|---|---|---|---|
| 窗口 | 0.13s | 0.18s | 0.35s |
| 慢动作 | ✅ 时间回弹 | ❌ | ❌ |
| 反击加成 | ✅ | ❌ | ❌ |
| 弹开力度 | 强（×2） | 中（仅中断） | 中（仅中断） |

---

## 3. 反击系统（Just Guard → 反击伤害）

- 持有 `State.Counter.Ready` 的角色，下一次攻击命中时：
  - 伤害所有数值型 modifier × `counterMultiplier`（默认 1.5，即 150%）
  - 命中后立即消耗标签（单次有效）
- 实现位置：`HurtBoxManager.ApplyDamageToTarget` → `TryConsumeCounterTag` + `SetMagnitudeOverride`
- 优势：无需改任何 GameplayEffect 资产，纯运行时增强；对 AttributeBased 等动态 modifier 同样生效（先 GetMagnitude 再乘）

---

## 4. 拼刀强化（Clash）

`ClashManager` 新增字段：

| 字段 | 默认 | 说明 |
|------|------|------|
| `clashTimeScale` | 0.05 | 拼刀慢动作时间缩放（5% 速度 ≈ 时间近乎冻结）|
| `clashSparkCount` | 5 | 围绕碰撞点喷射的火花数量 |
| `clashSoundExtra` | null | 第二金属音效，叠加出厚重撞击感 |

流程顺序：火花中心 + 冲击波 → 多角度火花喷射 → 双音效 → 慢动作（`freezeDuration + 0.15s`，回弹式恢复）→ 特写相机/震屏 → 冻结动画 → 卡肉（`WaitForSecondsRealtime` 保证不被自身缩放拖长）→ 弹开 + 硬直。

---

## 5. 新资产

| 资产 | 路径 | 说明 |
|------|------|------|
| `State.Guarding.JustGuard` | Assets/ScriptObjects/Tag/ | 完美格挡窗口标签（父：State.Guarding）|
| `State.Counter.Ready` | Assets/ScriptObjects/Tag/ | 反击状态标签 |

**运行时兜底**：HurtBoxManager 的 `Awake` 中若字段未配置，自动 `CreateInstance` 同名标签，保证开箱即用（标签资产则用于 Inspector 配置 + 层级匹配）。

---

## 6. 场景配置（一步完成）

菜单 **Tools → Combat → Auto-Configure Just Guard Tags**：
遍历场景中所有 HurtBoxManager，把两个标签资产自动写入空字段并标记场景 dirty。
（新角色用 CharacterSetup 创建后跑一次即可。）

---

## 7. 手感调参指南（鬼泣参考）

| 想调什么 | 字段 | 推荐区间 |
|----------|------|----------|
| Just Guard 判定松紧 | `justGuardWindow` | 0.10–0.16s（越小越硬核）|
| 时间冻结强度 | `justGuardTimeScale` | 0.05–0.25（越小越"定格"）|
| 时间冻结长度 | `justGuardSlowMotionDuration` | 0.08–0.2s |
| 反击伤害 | `counterMultiplier` | 1.3–2.0 |
| 反击窗口 | `counterWindowDuration` | 0.6–1.2s |
| 拼刀定格强度 | `clashTimeScale` | 0.03–0.1 |
| 拼刀火花密度 | `clashSparkCount` | 3–8 |
| 弹开力度 | `justGuardPushMultiplier` | 1.5–3 |

> 💡 提示：Just Guard 对「格挡刚按下」的瞬间判定最公平。若敌人 AI 攻击太快导致玩家总是按不准，可适当放宽 `justGuardWindow` 或缩小敌人攻击判定帧。

---

## 8. 代码位置索引

| 文件 | 改动 |
|------|------|
| `Assets/Scripts/Combat/TimeScaleDirector.cs` | **新增**：全局慢动作导演（最后请求者优先 + 回弹恢复）|
| `Assets/Scripts/Attack And Hit/Hit/HurtBoxManager.cs` | **修改**：Just Guard 三级判定、HandleJustGuard、反击伤害、标签兜底 |
| `Assets/Scripts/Player/PlayerState/PlayerGuardState.cs` | **修改**：格挡进入时授予 Just Guard 窗口标签 |
| `Assets/Scripts/Attack And Hit/ClashManager.cs` | **修改**：慢动作 + 火花喷射 + 双音效 |
| `Assets/Scripts/Attack And Hit/Attack/MeleeWeapon.cs` | **修改**：生产路径日志包 UNITY_EDITOR |
| `Assets/Scripts/Attack And Hit/ClashDetector.cs` | **修改**：Awake 日志包 UNITY_EDITOR |
| `Assets/Editor/Combat/JustGuardAutoConfig.cs` | **新增**：一键场景配置工具 |
| `Assets/ScriptObjects/Tag/State.Guarding.JustGuard.asset` | **新增**：标签资产 |
| `Assets/ScriptObjects/Tag/State.Counter.Ready.asset` | **新增**：标签资产 |
