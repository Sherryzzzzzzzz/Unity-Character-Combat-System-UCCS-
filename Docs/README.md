# Unity Character Combat System (UCCS) — 技术文档

## 项目概述

UCCS 是一个基于 Unity 的 **第三人称动作战斗系统**，核心特色是深度参考 **UE5 GAS (Gameplay Ability System)** 架构实现的属性/能力/效果框架。项目融合了 Animancer 动画系统、Cinemachine 相机系统、Behavior Designer 行为树等工业级插件，构建了一个完整的角色战斗演示。

## 技术栈总览

| 层级 | 技术 | 说明 |
|------|------|------|
| **引擎** | Unity 2022+ | C# 脚本 |
| **核心架构** | GAS-like (类 UE5 GAS) | Ability System Component + GameplayAbility + GameplayEffect + AttributeSet |
| **动画** | Animancer | 纯代码驱动的动画状态机 |
| **AI** | Behavior Designer | 可视化行为树 |
| **相机** | Cinemachine | 自由视角 + 锁定 + 拼刀特写 |
| **输入** | Unity Input System | 新版输入系统 |
| **数据驱动** | ScriptableObject | 能力/效果/攻击/标签均为 SO 资产 |
| **特效反馈** | GameplayCue + VFX | 标签驱动的特效解耦系统 |
| **技能时间轴** | 自定义 Timeline 系统 | 逐帧事件驱动的技能编辑器 |

## 文档索引

1. **[GAS 系统架构](01-GAS-System.md)** — 最核心的技术亮点
2. **[战斗系统](02-Combat-System.md)** — 攻击/受击/拼刀/格挡/精准闪避
3. **[技能时间轴与事件系统](03-Event-Timeline-System.md)** — 数据驱动的技能编辑器
4. **[UI 系统](04-UI-System.md)** — HUD/血条/状态效果
5. **[其他系统](05-Other-Systems.md)** — AI行为树/相机/调试工具

## 项目目录结构

```
Assets/
├── Scripts/
│   ├── GASSystem/              # ★ GAS 核心框架
│   │   ├── Core/               # 核心数据结构 (Spec, Context, ActorInfo, Host)
│   │   ├── Effect/             # 效果子类 (Damage/Heal/Buff/Cost/Cooldown)
│   │   ├── Task/               # 异步任务系统 (AbilityTask)
│   │   ├── Target/             # 目标搜索 (TargetActor, TargetData)
│   │   └── Cue/                # 视觉/音效反馈系统 (GameplayCue)
│   ├── ScriptsObject/          # ScriptableObject 数据资产
│   ├── EventFactory/           # 技能时间轴事件系统
│   ├── Attack And Hit/         # 攻击/受击/拼刀
│   ├── Player/                 # 玩家角色逻辑
│   ├── Enemy/                  # 敌人角色逻辑
│   ├── UI/                     # UI 系统
│   ├── Camera/                 # 相机逻辑
│   ├── Combat/                 # 战斗反馈
│   └── Base/                   # 基础工具
├── Animancer/                  # Animancer 插件
├── Behavior Designer/          # 行为树插件
└── Editor/                     # 编辑器工具
```

## 架构核心设计理念

### 1. UE5 GAS 同构映射

整个 GAS 系统高度对齐 UE5 GAS 的 API 和命名：

| UE5 GAS | UCCS 实现 | 说明 |
|---------|----------|------|
| `UAbilitySystemComponent` | `AbilitySystemComponent` | 能力系统核心组件 |
| `UGameplayAbility` | `GameplayAbility` | 能力基类 |
| `UGameplayEffect` | `GameplayEffect` (SO) | 效果数据资产 |
| `FGameplayEffectSpec` | `GameplayEffectSpec` | 效果运行时规格 |
| `FActiveGameplayEffect` | `ActiveGameplayEffect` | 活跃效果实例 |
| `UAttributeSet` | `AttributeSet` | 属性集 |
| `FGameplayTag` | `GameplayTagSO` (SO) | 层级标签系统 |
| `FGameplayAbilitySpec` | `GameplayAbilitySpec` | 能力运行时描述符 |
| `FGameplayEffectContext` | `GameplayEffectContext` | 效果上下文 |
| `FGameplayEventData` | `GameplayEventData` | 事件负载数据 |
| `UGameplayCueManager` | `GameplayCueManager` | 特效分发管理器 |
| `UAbilityTask` | `AbilityTask` | 异步任务基类 |
| `FGameplayTagQuery` | `GameplayTagQuery` | 标签查询条件 |
| `FGameplayModifierInfo` | `EffectAttributeModifier` | 属性修改器配置 |
| `FGameplayEffectExecutionCalculation` | `GameplayEffectExecutionCalculation` | 自定义执行计算 |

### 2. 数据驱动 (ScriptableObject)

所有能力、效果、攻击数据、标签均以 ScriptableObject 资产形式存在，策划和设计师可通过 Unity Inspector 直接配置，无需修改代码。

### 3. 标签驱动通信

系统间通信大量使用 `GameplayTagSO` 层级标签，实现了高度解耦：
- Ability 的激活/取消/阻塞基于标签
- GE 的施加条件/免疫/标签授予基于标签
- 连招系统通过瞬态标签触发
- 特效反馈通过 Cue 标签解耦
