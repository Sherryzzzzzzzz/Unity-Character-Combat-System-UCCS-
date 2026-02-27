## Context

AttackEvent 使用 `Physics.OverlapSphere/OverlapCapsule` 进行范围检测，但未传入 LayerMask 参数。检测结果依赖 `GetComponentInParent<AbilitySystemComponent>()` 过滤，但缺少图层过滤会导致：1) 性能浪费（检测所有碰撞体包括地面、装饰物等）；2) 潜在的误检测。同时没有运行时可视化，调试困难。

## Goals / Non-Goals

**Goals:**
- 为 OverlapSphere/OverlapCapsule/OverlapSphere(Cone) 调用添加 LayerMask 过滤
- 在 AttackData SO 上提供可配置的 hitLayerMask
- 运行时通过 Gizmos 绘制攻击检测形状（Sphere/Capsule/Cone）
- 添加调试日志，输出检测中心、半径、命中数

**Non-Goals:**
- 不改变伤害计算逻辑
- 不修改 MeleeWeapon 碰撞管线
- 不添加编辑器可视化（已有 SkillEditorWindow 中的预览）

## Decisions

### 决策 1：LayerMask 来源

**选择**: 在 AttackData ScriptableObject 上添加 `LayerMask hitLayerMask` 字段，默认值为 `Everything`（-1）。ExecuteSphere/Capsule/Cone 读取 `attackData.hitLayerMask` 传入 Physics 方法。

**理由**:
- 每种攻击可独立配置检测层，灵活
- 默认 Everything 保持向后兼容

### 决策 2：运行时 Gizmos 可视化方案

**选择**: 在 AttackEvent.OnStart 时将检测形状参数（类型、中心、半径等）写入 owner GameObject 上的 `AttackShapeDebugger` MonoBehaviour。AttackShapeDebugger 在 OnDrawGizmos 中根据缓存参数绘制 WireSphere/WireCube/线段。OnEnd 时清除。

**理由**:
- Gizmos 需要 MonoBehaviour 的 OnDrawGizmos 回调，AttackEvent 是纯 C# 类无法直接绘制
- 挂载在 owner 上，生命周期与攻击事件一致
- 备选：使用 Debug.DrawLine — 否决，无法绘制 WireSphere

## 文件变更清单

### 需要修改的现有文件
| 文件 | 变更内容 |
|------|----------|
| `Assets/Scripts/ScriptsObject/AttackData.cs` | 添加 hitLayerMask 字段 |
| `Assets/Scripts/EventFactory/Events/AttackEvent.cs` | ExecuteSphere/Capsule/Cone 使用 LayerMask，OnStart/OnEnd 触发 Debugger |

### 需要新建的文件
| 文件 | 说明 |
|------|------|
| `Assets/Scripts/EventFactory/Events/AttackShapeDebugger.cs` | Gizmos 可视化 MonoBehaviour |
