# Unity-Character-Combat-System-UCCS-

# 🎮 UE/Unity Hybrid Combat System Demo

一个使用 **Unity + Animancer** 实现的第三人称角色战斗系统 Demo，包含了状态机、动画混合、连击系统与 Root Motion 控制逻辑。  
本项目为本人在求职客户端/游戏客户端岗位时展示的 **Gameplay 编程能力** 作品。

---

## ✨ 特性概览

- 🧩 **状态机系统（State Machine）**
  - 自定义 `StateMachine` 基类。
  - 动画状态（Idle / Move / Jump / Fall / Attack）与逻辑状态分离。
  - 攻击状态与地面/空中状态可自由切换。

- 🎞️ **Animancer 动画系统整合**
  - 使用 `LinearMixerState` 实现行走、慢跑、奔跑动画的平滑混合。
  - 动画淡入淡出控制 (`FadeMode.FixedSpeed`)。
  - 攻击动画可使用 Root Motion 实现位移或跳跃。

- ⚔️ **攻击与连击系统**
  - `PlayerAttackComponent` 管理技能时间轴（SkillTimelineAsset）。
  - 支持前摇、攻击区间、连击判定与输入缓存。
  - 攻击状态下自动锁定动画 Root Motion，移动状态下使用玩家输入控制。

- 🧍 **角色控制系统**
  - `CharacterController` 驱动移动与地面检测。
  - 摄像机方向驱动移动输入（与 TPS 控制一致）。
  - 根运动（Root Motion）在攻击时启用，在移动状态关闭。
  - 平滑的加速与减速插值（运动与动画同步）。

- 🪂 **重力系统**
  - 自定义重力计算与下落检测。
  - 支持跳跃、落地过渡动画。

---

## 🧱 项目结构

| 模块                                      | 功能说明                             |
| ----------------------------------------- | ------------------------------------ |
| `PlayerModel`                             | 管理动画状态机与逻辑状态机，统一入口 |
| `PlayerController`                        | 处理输入、移动与旋转                 |
| `PlayerAttackComponent`                   | 控制技能与攻击动画播放               |
| `StateMachine`                            | 通用状态机实现                       |
| `MoveState` / `JumpState` / `AttackState` | 角色状态逻辑                         |
| `AnimationSet`                            | 储存所有动画引用                     |

---

## ⚙️ 技术要点

- 🎮 使用 **C# + Animancer** 动画系统。
- 🧠 状态模式（State Pattern）组织逻辑。
- 🪞 Root Motion 与 CharacterController 结合。
- 🔁 平滑插值移动（`Mathf.MoveTowards` + Mixer Parameter）。
- 🧍 动画/物理解耦（RootMotion handled by script）。
- ⚡ 可扩展的技能系统接口。

---

## 📦 项目运行

1. 克隆项目：
   ```bash
   git clone https://github.com/YourName/Unity-CombatSystem-Demo.git
   使用 Unity 2022.3+ 打开工程。

导入 Animancer 插件（可从 Asset Store 获取）。

打开场景 DemoScene.unity 并运行。