# 07 — 双轨系统收敛计划 (System Consolidation Plan)

> 背景：项目演进过程中，旧系统与新 GAS 系统并存，形成"双轨"。本文档明确**推荐路径**与**迁移计划**，
> 供后续开发统一方向、逐步清理。属架构决策记录，不执行破坏性删除。

---

## 1. 双轨清单

| # | 领域 | 旧轨（弃用方向） | 新轨（推荐方向） | 现状 |
|---|------|----------------|----------------|------|
| 1 | 能力激活 | string-key API：`ASC.ActivateAbility("name")` | Spec API：`GiveAbility → TryActivateAbilityByHandle` | 共存，新代码已走新 API |
| 2 | 效果系统 | `BuffSO` / `BuffEvent`（已标 `[Obsolete]`） | `GameplayEffect` (SO) + `EffectSpecFactory` | 共存，旧轨仅供 Parryable 等遗留使用 |
| 3 | AI | 自研 `Assets/Scripts/AI/Core` (BTreeRunner) | Behavior Designer（敌人 AI 实际在用） | 自研 BT 仅调试/演示 |
| 4 | 事件/异步 | `EventFactory` 时间轴事件 | GAS `AbilityTask` | 时间轴事件负责技能播放，Task 负责能力异步，职责已大致分离 |
| 5 | 弹反 | 旧 `Parryable`（BuffSO 依赖） | Just Guard（HurtBoxManager + Tag） | Parryable 的 BuffSO 部分待迁移 |

---

## 2. 推荐路径（新代码必须遵守）

1. **能力激活**：一律 `GiveAbility` + `TryActivateAbilityByHandle`；旧 string-key 入口保留 1 个版本周期后删除
2. **效果施加**：一律 `GameplayEffect` SO + `MakeOutgoingSpec`；禁止新增 `BuffSO` 用法
3. **AI**：敌人 AI 一律 Behavior Designer；自研 BT Core 不再扩展
4. **技能播放**：时间轴事件系统负责"技能动画帧事件"，GAS 负责"能力生命周期"，二者通过 `GameplayAbilityEvent`/`GASEffect` 事件桥接

## 3. 迁移计划（按风险从低到高）

- [ ] **P0 低风险**：删除 `BuffSO.ApplyBuff/RemoveBuff` 的调用点（Parryable → 改用 GE），移除 `[Obsolete]` 标记，删除 BuffSO 资产
- [ ] **P0 低风险**：删除自研 BT 的示例资产（BTreeAsset.asset），保留 Core 代码作参考
- [ ] **P1 中风险**：旧 string-key 能力激活入口降级为警告日志，统计调用点后删除
- [ ] **P2 高风险**：删除 BuffEvent / Buff 时间轴事件类型（需检查技能资产是否使用）
- [ ] **P3 待定**：自研 BT Core 归档到 `Assets/Archive/` 或删除（需确认无场景引用）

> ⚠️ 执行任何删除前，先跑一遍主场景确认无丢失引用。当前阶段**只文档化、不执行**删除，
> 避免破坏正在运行的编辑器与场景配置。

---

## 4. 本次改造的落点（与收敛方向一致）

- 新 Just Guard / 反击系统完全基于新 GAS 基建：`MakeOutgoingSpec` / `SetMagnitudeOverride` / `ApplyEffectSpec` / `GameplayTagSO` 标签
- 未新增任何旧 API 调用；`TryConsumeCounterTag` 走 `TagComponent` 新标签体系
- 唯一遗留：`Parryable` 仍用 `BuffSO` 做被弹反硬直——列入上述 P0 迁移项
