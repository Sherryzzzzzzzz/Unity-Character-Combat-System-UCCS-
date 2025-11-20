# *Unity-Character-Combat-System-UCCS*

**一个基于 Unity 引擎，以《艾尔登法环》等魂类游戏为灵感的第三人称动作角色扮演游戏（ARPG）原型。**

该项目旨在探索和实现现代动作游戏中的核心机制，包括一个高度可配置的技能系统、基于 Gameplay Tag 的效果系统，以及一个复杂的 AI 行为系统。

---

## ✨ 核心特性 (Core Features)

这个项目包含了以下几个关键系统的实现：

*   **🔧 模块化角色控制器 (Modular Character Controller)**
    *   基于 Unity 的 Character Controller，实现了灵活的地面与空中状态管理。
    *   相机驱动的移动逻辑，确保流畅的第三人称操控体验。

*   **🎨 数据驱动的技能系统 (Data-Driven Skill System)**
    *   **可视化技能编辑器**: 内置一个基于 `ScriptableObject` 和 `UIElements` 的时间轴技能编辑器，允许设计师在不编写代码的情况下，直观地创建和调整技能。
    *   **多轨道事件**: 支持在技能时间轴上配置攻击判定、受击盒开关、连招窗口、特效播放、音效触发等多种事件。
    *   **动画驱动**: 使用 **Animancer** 插件进行动画播放，支持多层动画混合，实现了攻击动画对移动动画的无缝覆盖。

*   **🥊 精确的战斗系统 (Precise Combat System)**
    *   **部位打击**: 角色模型被划分为多个 `HurtBox`（受击盒），支持精确的身体部位命中检测。
    *   **动态击退力**: 攻击造成的击退/击飞力的方向，是根据攻击判定轨迹的**起始和结束点**动态计算的，实现了符合物理直觉的力反馈。
    *   **韧性系统 (Poise System)**: 仿照魂类游戏，角色拥有韧性值。受到攻击会削减韧性，韧性归零时会被打出大硬直。
    *   **四向受击动画**: 根据攻击来源的前、后、左、右四个方向，以及攻击强度（轻、中、重），播放不同的受击动画。

*   **🏷️ Gameplay Tag 系统 (Gameplay Tag System)**
    *   一个轻量级、高性能的标签系统，用于处理所有状态和效果。
    *   **攻击效果**: 攻击命中时，不再直接造成伤害，而是向目标施加一个 `AttackTag`。
    *   **受击响应**: 受击方通过监听 `TagComponent` 来响应收到的 Tag，从而触发扣血、添加Debuff、播放受击动画等逻辑。完全解耦了攻击方和受击方。
    *   **输入缓存与连招**: 玩家的输入被转换为瞬时 Tag，`ComboEvent` 通过消耗这些 Tag 来判断连招是否成功。

*   **🤖 行为树 AI (Behavior Tree AI)**
    *   使用 **Behavior Designer** 插件构建敌人AI。
    *   实现了 AI 的基本行为逻辑，如巡逻、追击、以及基于距离选择不同攻击技能。
    *   **(待实现/扩展)** 视野系统、攻击调度系统（“看戏AI”）。

---

## 🛠️ 技术栈 (Tech Stack)

*   **引擎**: Unity 2022.3.x (LTS) 或更高版本
*   **渲染管线**: Universal Render Pipeline (URP)
*   **核心插件**:
    *   **Animancer Pro**: 用于高性能、代码驱动的动画播放。
    *   **Behavior Designer**: 用于构建 AI 行为树。
    *   **Cinemachine**: 用于实现智能第三人称相机。
*   **输入系统**: Unity's New Input System

---

## 🚀 如何开始 (Getting Started)

1.  **克隆仓库**:
    ```bash
    git clone https://github.com/YourUsername/YourProjectName.git
    ```
2.  **安装插件**:
    *   打开 Unity Hub，添加并打开克隆下来的项目。
    *   从 Unity Asset Store 或包管理器，导入项目所需的插件：**Animancer Pro**, **Behavior Designer**。
3.  **探索核心场景**:
    *   打开位于 `Assets/Scenes/` 文件夹下的主场景（例如 `Main_Playground.unity`）。
4.  **运行游戏**:
    *   点击 Unity 编辑器顶部的播放按钮，即可开始体验。

---

## 📖 核心系统指南 (System Guides)

### 如何创建一个新技能？

1.  在 `Project` 窗口中，右键 -> `Create` -> `[你的技能资产菜单路径]` -> `SkillTimelineAsset`，创建一个新的技能资产。
2.  通过菜单栏 `Tools` -> `Skill Editor` 打开技能编辑器窗口。
3.  将新创建的技能资产拖入编辑器顶部的 `ObjectField` 中。
4.  配置动画片段、添加 `AttackEvent`、`ComboEvent` 等事件轨道。
5.  **配置 `AttackEvent`**:
    *   `Hit Box Name`: 填写武器/判定区 GameObject 的名称。
    *   `Damage`, `Poise Damage`, `Hit Force`: 直接填写数值。
    *   `Force Type`: 选择力的类型（轻、中、重）。
    *   `Hit Stun Tag`: 填写一个字符串标签，用于标识本次攻击造成的硬直类型（例如 "Hit.Stun.Light"）。
6.  保存资产。

---

## 🚧 未来计划 (Roadmap)

*   [ ] 实现完整的韧性系统反馈（破韧倒地等）。
*   [ ] 完善敌人 AI，加入a*寻路，视野和听觉感知。
*   [ ] 开发攻击调度系统，实现多个敌人间的协同攻击。
*   [ ] 增加更多的技能和敌人种类。
*   [ ] 拼刀和格挡系统，Buff系统。
