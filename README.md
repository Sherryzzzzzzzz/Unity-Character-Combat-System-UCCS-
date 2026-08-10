# Unity-Character-Combat-System (UCCS)

一个基于 Unity 引擎、以《艾尔登法环》等魂类游戏为灵感的第三人称动作角色扮演游戏（ARPG）原型。

该项目旨在探索和实现现代动作游戏中的核心机制，包括一个高度可配置的技能系统、深度参考 UE5 GAS 的属性/能力/效果框架、基于 Gameplay Tag 的解耦通信，以及一套自研的行为树 AI 系统。

---

## 核心特性 (Core Features)

### 模块化角色控制器 (Modular Character Controller)

- 基于 Unity Character Controller，实现地面 / 空中 / 跳跃等状态管理。
- 相机驱动的移动逻辑，保证流畅的第三人称操控体验。
- 玩家侧状态机（PlayerState）管理攻击、防御、瞄准、空中等状态；动画状态机（AnimationState）管理移动 / 瞄准 / 跳跃 / 下落。

### 数据驱动的技能系统 (Data-Driven Skill System)

- **可视化技能编辑器**: 内置基于 `ScriptableObject` 与 UIElements 的时间轴技能编辑器，设计师无需编写代码即可创建和调整技能。
- **多轨道事件**: 支持在技能时间轴上配置攻击判定、受击盒开关、连招窗口、特效播放、音效触发等多种事件。
- **动画驱动**: 使用 Animancer 进行代码驱动的动画播放，支持多层动画混合，攻击动画无缝覆盖移动动画。

### 精确的战斗系统 (Precise Combat System)

- **部位打击**: 角色模型划分为多个 `HurtBox`（受击盒），支持精确的身体部位命中检测。
- **动态击退力**: 击退 / 击飞方向根据攻击判定轨迹的起始与结束点动态计算，反馈符合物理直觉。
- **韧性系统 (Poise)**: 仿照魂类游戏，角色拥有韧性值，受到攻击会削减韧性，韧性归零会被打出大硬直。
- **四向受击动画**: 根据攻击来源方向（前 / 后 / 左 / 右）与攻击强度（轻 / 中 / 重）播放不同的受击动画。

### 拼刀 / 完美格挡 / 反击 (Clash / Just Guard / Counter)

- **拼刀 (Clash)**: 双方攻击窗口重叠时触发拼刀判定，火花四溅、慢动作定格、特写镜头、双方弹开。
- **完美格挡 (Just Guard)**: 攻击命中前的极短时间窗口内格挡，触发无伤、零消耗、弹开攻击者、时间冻结与反击加成。
- **反击**: 完美格挡成功后获得反击窗口，可打出高额伤害。

### Gameplay Tag 系统 (Gameplay Tag System)

- 轻量、高性能的层级标签系统，用于处理所有状态与效果。
- 攻击命中时不直接造成伤害，而是向目标施加 `AttackTag`；受击方通过监听 `TagComponent` 响应收到的 Tag，触发扣血、Debuff、受击动画等逻辑，完全解耦攻击方与受击方。
- 输入缓存与连招：玩家输入被转换为瞬时 Tag，`ComboEvent` 通过消耗这些 Tag 判断连招是否成功。

### GAS-like 属性 / 能力 / 效果框架

- 深度参考 UE5 GAS 架构：`AbilitySystemComponent` + `GameplayAbility` + `GameplayEffect` + `AttributeSet`。
- 属性集（AttributeSet）驱动生命、韧性、耐力等数值，支持修改器（Modifier）、Buff、Cost、Cooldown 等效果规格。
- 支持能力任务（AbilityTask）、目标搜索（TargetActor / TargetData）、GameplayCue 特效反馈。
- 核心类独立于 `UCCS.GASCore` 程序集，配套 50+ 个 EditMode 单元测试。

### 自研行为树 AI (Behavior Tree AI)

- 不依赖第三方插件，自研 BT 核心（`UCCS.BT` 程序集）：节点（BTAction / BTCondition / BTComposite / BTDecorator）、黑板（Blackboard）、行为树资产（BTreeAsset）。
- 实现了巡逻、追击、基于距离选择不同攻击技能等敌人行为。
- 配套可视化行为树编辑窗口与 BTree 单元测试。

### 战斗反馈与表现 (Combat Feedback & VFX)

- HitStop 顿帧、时间缩放导演（TimeScaleDirector）、相机震动、FOV 冲击。
- 火花、冲击波、多角度喷射等命中特效；武器拖尾与剑气特效（世界空间粒子 + Stretch 拉伸）。
- 对象池化的 VFX（GlobalVFXPool）与伤害飘字（DamageNumberManager）。

### UI 系统

- 玩家 HUD：血条、耐力、韧性、技能槽、状态效果列表、目标信息。
- 敌人血条（世界空间 + 屏幕血条）、Boss 血条、韧性条。
- 锁定目标信息（Lock-On）与自由视角切换。
- 游戏结束（Game Over）UI：死亡提示，随后出现"开始"按钮，点击重新加载当前场景。

---

## 技术栈 (Tech Stack)

| 类别 | 技术 | 说明 |
|------|------|------|
| 引擎 | Unity 6 (6000.1.x) | C# |
| 渲染管线 | Universal Render Pipeline (URP 17.x) | 另含 Toon Shader 卡通渲染 |
| 动画 | Animancer | 代码驱动的动画播放与混合 |
| 相机 | Cinemachine | 自由视角 + 锁定 + 拼刀特写 |
| 输入 | Unity Input System | 新版输入系统 |
| AI | 自研 BT (UCCS.BT) | 行为树核心 + 可视化编辑器 |
| GAS | UCCS.GASCore / GASSystem | UE5 GAS 同构映射 |
| 数据驱动 | ScriptableObject | 能力 / 效果 / 攻击 / 标签均为资产 |
| 特效反馈 | GameplayCue + VFX 对象池 | 标签驱动的特效解耦 |
| 技能时间轴 | 自定义 Timeline 系统 | 逐帧事件驱动的技能编辑器 |
| 测试 | Unity Test Framework | EditMode 单元测试（GASCore / BT） |

---

## 如何开始 (Getting Started)

1. **克隆仓库**:
   ```bash
   git clone <仓库地址>
   ```
2. **打开项目**:
   - 使用 Unity Hub 添加并打开项目（Unity 6000.1.x）。
   - 如缺失 Animancer 插件，请从 Unity Asset Store 导入 Animancer Pro / Animancer。
3. **打开主场景**:
   - `Assets/Scenes/Test.unity`。
4. **运行游戏**:
   - 点击 Unity 编辑器顶部的 Play 按钮即可体验。

---

## 核心系统指南 (System Guides)

### 如何创建一个新技能？

1. 在 `Project` 窗口中右键 -> `Create` -> 技能资产菜单 -> `SkillTimelineAsset`，创建技能资产。
2. 通过菜单栏 `Tools` -> `Skill Editor` 打开技能编辑器窗口。
3. 将技能资产拖入编辑器顶部的 `ObjectField`。
4. 配置动画片段，添加 `AttackEvent`、`ComboEvent` 等事件轨道。
5. **配置 `AttackEvent`**:
   - `Hit Box Name`: 填写武器 / 判定区 GameObject 名称。
   - `Damage`、`Poise Damage`、`Hit Force`: 直接填写数值。
   - `Force Type`: 选择力的类型（轻 / 中 / 重）。
   - `Hit Stun Tag`: 填写字符串标签，标识本次攻击造成的硬直类型（例如 `Hit.Stun.Light`）。
6. 保存资产。

---

## 文档 (Docs)

`Docs/` 目录包含 10+ 篇技术文档，覆盖架构设计、实现细节与面试速查：

1. [GAS 系统架构](Docs/01-GAS-System.md)
2. [战斗系统](Docs/02-Combat-System.md)
3. [技能时间轴与事件系统](Docs/03-Event-Timeline-System.md)
4. [UI 系统](Docs/04-UI-System.md)
5. [其他系统](Docs/05-Other-Systems.md)
6. [拼刀 / 完美格挡 / 反击系统](Docs/06-Just-Guard-Clash-System.md)
7. [双轨系统收敛计划](Docs/07-System-Consolidation-Plan.md)
8. [玩家状态机](Docs/08-Player-State-Machine.md)
9. [GAS 深度剖析](Docs/09-GAS-Deep-Dive.md)
10. [敌人行为树](Docs/10-Enemy-BT.md)
11. [GAS 单元测试说明](Docs/GAS_Tests_README.md)
12. [性能优化](Docs/Performance_Optimization.md)

---

## 目录结构 (Project Structure)

```
Assets/
├── Scenes/                   # 主场景 (Test.unity)
├── Scripts/
│   ├── GASCore/              # GAS 核心（独立程序集，含单元测试）
│   ├── GASSystem/            # 能力 / 效果 / 任务 / 目标 / Cue
│   ├── AI/                   # 自研行为树 (UCCS.BT)
│   ├── Attack And Hit/       # 攻击 / 受击盒 / 拼刀 / 格挡
│   ├── Player/               # 玩家控制器 / 状态机 / 技能组件
│   ├── Enemy/                # 敌人模型 / 技能组件 / 动画驱动
│   ├── Combat/               # 顿帧 / 时间缩放 / 特效反馈
│   ├── EventFactory/         # 技能时间轴事件系统
│   ├── UI/                   # HUD / 血条 / 飘字 / 状态效果
│   ├── Camera/               # 相机 / 锁定 / 拼刀特写
│   ├── GameOver/             # 死亡处理与重试
│   ├── Base/                 # 状态机 / 单例 / 音频等基础工具
│   └── Tests/                # EditMode 单元测试
├── ScriptObjects/            # 技能 / 攻击 / 效果 / 标签数据资产
├── Data/                     # 技能资产 (Abilities) 与 AI 资产
└── Editor/                   # 技能编辑器 / BT 编辑窗口等
```

---

## 未来计划 (Roadmap)

- [ ] 完善韧性系统反馈（破韧倒地等）。
- [ ] 敌人 AI 加入 A* 寻路、视野与听觉感知。
- [ ] 开发攻击调度系统，实现多个敌人间的协同攻击。
- [ ] 增加更多技能与敌人种类。
- [ ] 拼刀 / 格挡与 Buff 系统的进一步扩展。
