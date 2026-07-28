# 03 — 技能时间轴与事件系统

## 目录

1. [系统概述](#系统概述)
2. [核心数据结构](#核心数据结构)
3. [事件类型](#事件类型)
4. [执行流程](#执行流程)
5. [连招系统](#连招系统)
6. [技能管理器对比](#技能管理器对比)
7. [编辑器工具](#编辑器工具)

---

## 系统概述

UCCS 的技能系统采用 **自定义 Timeline + 逐帧事件驱动** 架构。技能以 `SkillTimelineAsset` (ScriptableObject) 形式存储，包含多个轨道 (Track)，每个轨道包含多个事件 (Event)。运行时由 `PlayerSkillComponent` / `EnemySkillComponent` 驱动动画播放并逐帧触发事件。

```
SkillTimelineAsset (SO)
├── animationClip                    // 动画片段
├── tracks[]
│   ├── Track 0 (攻击轨道)
│   │   ├── HitBoxEvent              // 碰撞体激活/关闭
│   │   ├── AttackEvent              // 攻击数据注入
│   │   └── CancelEvent              // 可取消窗口
│   ├── Track 1 (连招轨道)
│   │   ├── ComboEvent               // 连招窗口 + 下一技能
│   │   └── LoopEvent                // 循环等待
│   ├── Track 2 (GAS 轨道)
│   │   ├── GameplayEffectEvent      // 施加效果
│   │   ├── GameplayAbilityEvent     // 激活能力
│   │   ├── CooldownEvent            // 冷却控制
│   │   └── BuffEvent                // Buff 施加
│   ├── Track 3 (反馈轨道)
│   │   ├── SoundEvent               // 音效
│   │   ├── EffectEvent              // 粒子特效
│   │   └── CueEvent                 // GameplayCue 触发
│   └── Track N (...更多自定义轨道)
└── ...
```

---

## 核心数据结构

### SkillTimelineAsset

技能的数据容器 (ScriptableObject)，包含：
- `animationClip`: 动画片段
- `tracks`: 轨道数组
- 其他技能元数据

### Track (轨道)

每个 Track 包含一组事件，用于逻辑分组：
```csharp
class Track
{
    public string name;           // 轨道名称
    public List<TimelineEvent> events;  // 事件列表
}
```

### ITimelineEventRuntime (事件运行时接口)

所有事件实现的核心接口：
```csharp
interface ITimelineEventRuntime
{
    int StartFrame { get; }   // 开始帧
    int EndFrame { get; }     // 结束帧
    void OnStart(GameObject owner);  // 进入区间时调用
    void OnEnd(GameObject owner);    // 离开区间时调用
}
```

### 事件生命周期

```
帧 [StartFrame]: OnStart() 被调用
   ↓ (区间内活跃)
   ↓ (可能持续多帧)
帧 [EndFrame]: OnEnd() 被调用 → 清理 / 关闭
```

---

## 事件类型

### 战斗事件

| 事件 | 文件 | 功能 |
|------|------|------|
| `HitBoxEvent` | `HitBoxEvent.cs` | 激活/关闭武器碰撞体 (MeleeWeapon) |
| `AttackEvent` | `AttackEvent.cs` | 注入 AttackData 到武器/攻击判定器 |
| `CancelEvent` | `CancelEvent.cs` | 定义可取消窗口 (受击/闪避/跳跃取消等) |
| `AttackShapeDebugger` | `AttackShapeDebugger.cs` | 编辑器调试: 可视化攻击判定形状 |

### GAS 事件

| 事件 | 文件 | 功能 |
|------|------|------|
| `GameplayEffectEvent` | `GameplayEffectEvent.cs` | 在指定帧施加 GE |
| `GameplayAbilityEvent` | `GameplayAbilityEvent.cs` | 在指定帧激活 GA |
| `CooldownEvent` | `CooldownEvent.cs` | 控制 CD 开始/结束 |
| `BuffEvent` | `BuffEvent.cs` | Buff 施加 (旧版 BuffSO) |
| `CueEvent` | `CueEvent.cs` | 触发 GameplayCue (VFX/SFX) |
| `TargetSearchEvent` | `TargetSearchEvent.cs` | 搜索目标 |

### 逻辑事件

| 事件 | 文件 | 功能 |
|------|------|------|
| `LoopEvent` | `LoopEvent.cs` | 循环播放区间 + 条件跳出 |
| `ComboEvent` | `ComboEvent.cs` | 连招窗口 + 下一技能 |
| `CancelEvent` | `CancelEvent.cs` | 可取消行为类型 |

### 反馈事件

| 事件 | 文件 | 功能 |
|------|------|------|
| `SoundEvent` | `SoundEvent.cs` | 播放指定音效 |
| `EffectEvent` | `EffectEvent.cs` | 生成粒子特效 |

---

## 执行流程

### PlayerSkillComponent 更新循环

**文件**: `Assets/Scripts/Player/PlayerSkillComponent.cs` (484行)

```
PlaySkill(skill)
├─ StopAndCleanup (上一个技能)
├─ 解析 Track → frameStartEvents / frameEndEvents 字典
├─ 播放动画 (Animancer Attack Layer)
└─ isPlaying = true

Update() (每帧)
├─ 计算 currentFrame = state.Time × frameRate
├─ TriggerEventsForFrameRange(prevFrame+1, currentFrame)
│   ├─ 触发 frameStartEvents[frame] → OnStart()
│   │   ├─ LoopEvent → +activeLoopEvents
│   │   ├─ CancelEvent → +_activeCancelEvents
│   │   └─ ComboEvent → HandleComboEvent()
│   ├─ 触发 frameEndEvents[frame] → OnEnd()
│   │   ├─ LoopEvent → -activeLoopEvents
│   │   └─ CancelEvent → -_activeCancelEvents
├─ 循环逻辑: LoopEvent 结束帧回跳
└─ 动画结束 → HandleAnimationEnd → StopAndCleanup
```

### EnemySkillComponent (由外部驱动)

**文件**: `Assets/Scripts/Enemy/EnemySkillComponent.cs` (296行)

与 PlayerSkillComponent 类似，但 `ManualUpdate()` 由外部（行为树 `PlaySkill` Action 节点）调用，而非自己的 Update。

---

## 连招系统

### 基于瞬态标签的连招窗口

```
1. ComboEvent.OnStart()
   └─ tagComponent.AddTransientTag(combo.RequiredTag)
      // 如 "Input.LightAttack" 瞬态标签

2. 玩家按下攻击键
   └─ tagComponent.AddTransientTag("Input.LightAttack")

3. ComboEvent.OnStart() 在同一帧
   └─ HandleComboEvent()
       ├─ comboMode == Normal_Cacheable:
       │   └─ tagComponent.ConsumeTag("Input.LightAttack")
       │       如果消耗成功 → 播放 nextSkill
       └─ comboMode == Strict_Immediate:
           └─ tagComponent.HasTag("Input.LightAttack")
               如果拥有 → ConsumeTag → 播放 nextSkill

4. 连招窗口结束 (ComboEvent.EndFrame)
   └─ ComboEvent.OnEnd() → 窗口关闭
```

### 输入缓存

```csharp
// PlayerSkillComponent 中
public void CacheInputAction(InputActionReference input)
{
    cachedInputAction = input;
    cachedInputTimer = CachedInputExpire;  // 0.25s
}

// 消费缓存的输入
public bool ConsumeCachedInputIfMatch(InputActionReference input)
```

连招窗口内的输入会被缓存最多 0.25 秒，即使玩家提前按了也能在窗口内触发。

### Cancel 系统

```csharp
public enum CancelActionType
{
    None    = 0,
    Hit     = 1 << 0,   // 受击可取消
    Dodge   = 1 << 1,   // 闪避可取消
    Jump    = 1 << 2,   // 跳跃可取消
    Skill   = 1 << 3,   // 其他技能可取消
}

// CancelEvent 配置
public CancelActionType CancelableBy;
```

在 CancelEvent 的 [StartFrame, EndFrame] 区间内，技能可以被指定类型的行动取消。

---

## 技能管理器对比

| 特性 | PlayerSkillComponent | EnemySkillComponent |
|------|---------------------|---------------------|
| 更新驱动 | 自己的 Update() | 外部 ManualUpdate() |
| 动画系统 | Animancer (full) | Animancer (精简) |
| 连招支持 | ✅ 完整连招系统 | ❌ 无连招 |
| 输入缓存 | ✅ InputActionRef + 缓存 | ❌ 无输入 |
| Cancel 系统 | ✅ CancelEvent + CancelableBy | ❌ 无 Cancel |
| 循环事件 | ✅ LoopEvent + BreakConditions | ❌ 无循环 |
| IClashable | ✅ | ✅ |
| GAS 集成 | ✅ ASC + Spec API | ❌ 仅拼刀 Tag |

---

## 编辑器工具

### SkillEditorWindow

**文件**: `Assets/Editor/Timeline/SkillEditorWindow.cs`

自定义 Unity Editor 窗口，提供可视化的时间轴编辑器：
- 拖拽式轨道和事件编辑
- 帧级别的精确事件定位
- 动画波形预览
- 攻击判定形状可视化

### SkillDebugManager

**文件**: `Assets/Scripts/Debugging/SkillDebugManager.cs`

运行时调试工具：
- 实时显示当前播放的技能和帧数
- 攻击判定形状可视化 (AttackShapeDebugger)
- 事件触发日志
