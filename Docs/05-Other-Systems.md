# 05 — 其他系统

## 目录

1. [AI 行为树系统](#ai-行为树系统)
2. [相机系统](#相机系统)
3. [动画系统](#动画系统)
4. [角色状态机](#角色状态机)
5. [调试工具](#调试工具)
6. [基础工具类](#基础工具类)

---

## AI 行为树系统

### Behavior Designer 集成

项目使用 Behavior Designer 插件实现 AI 行为树。核心自定义节点：

#### PlaySkill (自定义 Action)

**文件**: `Assets/Scripts/BehaviorDesigner/PlaySkill.cs`

行为树节点，驱动 EnemySkillComponent 播放技能：
```
PlaySkill Action Node
├─ 选择 SkillTimelineAsset
├─ 调用 enemySkillComponent.PlaySkill(skill)
├─ 每帧 ManualUpdate()
└─ 等待技能结束 (OnSkillEnd) → 返回 Success
```

#### MoveTo (自定义 Action)

**文件**: `Assets/Scripts/BehaviorDesigner/MoveTo.cs`

控制敌人移动到指定位置/目标。

#### StackedConditional / StackedAction

行为树内置的复合节点，支持：
- **Sequence** (AND): 所有子任务依次成功
- **Selector** (OR): 任一子任务成功即返回

### 敌人 AI 流程示例

```
Behavior Tree:
├─ Selector
│   ├─ Sequence (战斗状态)
│   │   ├─ HasTarget? (Conditional)
│   │   ├─ MoveTo target
│   │   └─ PlaySkill (攻击技能)
│   └─ Sequence (巡逻状态)
│       ├─ MoveTo waypoint
│       └─ Wait
```

---

## 相机系统

### FreeLookLockOn

**文件**: `Assets/Scripts/Camera/FreeLookLockOn.cs`

自由视角 + 锁定目标混合系统：
- 无目标时: Cinemachine FreeLook 自由旋转
- 锁定目标时: 自动朝向目标，可切换锁定目标
- 平滑过渡动画

### CameraImpactEffects

**文件**: `Assets/Scripts/Camera/CameraImpactEffects.cs`

相机冲击效果：
- **FOV Kick**: 攻击命中时短暂 FOV 缩放
- 力度分级 (根据 AttackForceType)

### 拼刀特写相机

ClashManager 在拼刀时自动切换到预置的 Cinemachine Virtual Camera：
- 自动定位到双方角色之间
- LookAt 拼刀中心点
- 延迟自动切回主相机

---

## 动画系统

### Animancer

项目使用 **Animancer** 插件替代 Unity Mecanim 状态机，实现纯代码动画控制：

**层级管理**:
| 层级 | 索引 | 用途 |
|------|------|------|
| Base Layer | 0 | 基础运动 (Idle/Move/Jump) |
| Attack Layer | 1 | 攻击动画 |
| Hit Layer | 2 | 受击动画 |

**核心优势**:
- 无需 Animator Controller 状态机
- `layer.Play(clip, fadeDuration, FadeMode)` 直接播放
- `layer.StartFade(weight, duration)` 平滑过渡
- `state.Events(this).OnEnd = callback` 结束回调

### PlayerAnimationSet / EnemyAnimationData

ScriptableObject 资产，集中管理角色动画片段引用：
```csharp
public class PlayerAnimationSet : ExpandableAnimationSet
{
    // 可扩展的动画映射字典
    public AnimationClip GetClip(string name);
}
```

### FootIKController

**文件**: `Assets/Scripts/Player/FootIKController.cs`

玩家角色脚部 IK，使脚部贴合地形。

---

## 角色状态机

### PlayerModel (玩家状态)

玩家状态枚举：
```csharp
enum PlayerState
{
    ground,     // 地面 (可移动)
    sky,        // 空中
    guard,      // 防御姿态
    aim,        // 锁定姿态
    attacking,  // 攻击中
    hit,        // 受击中
}
```

### StateMachine / StateBase

**文件**: `Assets/Scripts/Base/StateMachine.cs` / `StateBase.cs`

通用有限状态机基础设施：
```csharp
public abstract class StateBase
{
    public virtual void Enter();
    public virtual void Exit();
    public virtual void Update();
}
```

### 玩家动画状态

**文件**: `Assets/Scripts/Player/AnimationState/`:
- `IdleState.cs`: 待机状态
- `MoveState.cs`: 移动状态
- `JumpState.cs`: 跳跃/空中状态
- `FallState.cs`: 下落状态

### TargetingSystem

**文件**: `Assets/Scripts/Player/TargetingSystem.cs`

目标锁定系统：
- 自动搜索最近敌人
- 左右切换锁定目标
- 与 FreeLookLockOn 相机联动

---

## 调试工具

### SkillDebugManager

**文件**: `Assets/Scripts/Debugging/SkillDebugManager.cs`

运行时技能调试：
- 可视化当前播放的技能和帧数
- 攻击判定形状显示 (Gizmos)
- 事件触发日志

### AttackShapeDebugger

**文件**: `Assets/Scripts/EventFactory/Events/AttackShapeDebugger.cs`

编辑器/运行时可视化攻击判定形状：
- 扇形 (Angle)
- 圆形 (Radius)
- 矩形 (Length)

---

## 基础工具类

### SingletonPattern (单例)

三重单例模式实现：
```csharp
// 基础 MonoBehaviour 单例
SingletonPatternMonoBase<T>

// DontDestroyOnLoad 单例
SingletonPatternMonoBase_DontDestroyOnLoad<T>
    // GASHost, GameplayCueManager, ClashManager 等

// 非 MonoBehaviour 单例
SingletonPattern<T>
```

### MonoManager

**文件**: `Assets/Scripts/Base/MonoManager.cs`

全局 MonoBehaviour 管理器，提供：
- Update/FixedUpdate/LateUpdate 事件
- 协程管理

### AudioManager

**文件**: `Assets/Scripts/Base/AudioManager.cs`

音效管理器，支持：
- 音效播放/停止
- 音效池 (AudioSource Pool)
- BGM 管理

### InputActionWatcher

**文件**: `Assets/Scripts/Player/InputActionWatcher.cs`

基于 Unity Input System 的输入监控器。

---

## 跨系统集成图

```
┌─────────────────────────────────────────────────────────┐
│                    输入层 (Input System)                  │
│              InputActionWatcher / IComboInput            │
└──────────────┬──────────────────────────────────────────┘
               │
               ▼
┌──────────────────────────────────────────────────────────┐
│                    角色控制器层                            │
│  ┌─────────────────┐  ┌─────────────────┐               │
│  │ PlayerController │  │ EnemyController │               │
│  │ (PlayerModel)    │  │ (Behavior Tree) │               │
│  └────────┬────────┘  └────────┬────────┘               │
│           │                    │                         │
│  ┌────────▼────────────────────▼────────┐               │
│  │     SkillComponent (Player/Enemy)     │               │
│  │  - Timeline 驱动                      │               │
│  │  - Weapon 管理                        │               │
│  └────────┬──────────────────────────────┘               │
└───────────┼──────────────────────────────────────────────┘
            │
            ▼
┌──────────────────────────────────────────────────────────┐
│                    GAS 系统层                              │
│  AbilitySystemComponent + AttributeSet + TagComponent    │
│  - 伤害计算 (ExecutionCalculation)                        │
│  - 效果管理 (GE Spec + ActiveGE)                          │
│  - 属性修改 (Modifier Stack)                              │
│  - 标签驱动 (激活/阻塞/免疫)                               │
└───────────┬──────────────────────────────────────────────┘
            │
            ▼
┌──────────────────────────────────────────────────────────┐
│                    反馈层                                  │
│  ┌───────────┐ ┌───────────┐ ┌───────────┐ ┌──────────┐ │
│  │ HitReact  │ │ VFX/SFX   │ │ Camera    │ │ UI       │ │
│  │ -Controller│ │ (Cue)     │ │ -Impulse  │ │ -HP Bar  │ │
│  │ -Animator │ │ -Particle │ │ -FOV Kick │ │ -Damage  │ │
│  │ -Knockback│ │ -Sound    │ │ -ClashCam │ │ -Status  │ │
│  └───────────┘ └───────────┘ └───────────┘ └──────────┘ │
└──────────────────────────────────────────────────────────┘
```
