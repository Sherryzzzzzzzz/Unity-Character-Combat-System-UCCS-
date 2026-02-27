## 1. AttackData — 添加 LayerMask 字段

- [x] 1.1 在 `Assets/Scripts/ScriptsObject/AttackData.cs` 中添加 `LayerMask hitLayerMask` 字段，默认值为 Everything（-1），放在 Attack Shape Settings 分组下

## 2. AttackEvent — ExecuteSphere/Capsule/Cone 添加 LayerMask 过滤

- [x] 2.1 修改 `Assets/Scripts/EventFactory/Events/AttackEvent.cs` 中 `ExecuteSphere` 方法，将 `Physics.OverlapSphere(center, radius)` 改为 `Physics.OverlapSphere(center, radius, attackData.hitLayerMask)`
- [x] 2.2 修改 `ExecuteCone` 方法，将 `Physics.OverlapSphere(center, attackData.length)` 改为 `Physics.OverlapSphere(center, attackData.length, attackData.hitLayerMask)`
- [x] 2.3 修改 `ExecuteCapsule` 方法，将 `Physics.OverlapCapsule(point1, point2, radius)` 改为 `Physics.OverlapCapsule(point1, point2, radius, attackData.hitLayerMask)`
- [x] 2.4 在 `ExecuteSphere` 中添加 `Debug.Log`，输出检测中心坐标、半径和命中碰撞体数量

## 3. AttackShapeDebugger — 运行时 Gizmos 可视化组件

- [x] 3.1 新建 `Assets/Scripts/EventFactory/Events/AttackShapeDebugger.cs`，实现 MonoBehaviour，包含缓存字段：`shapeType`（AttackShape）、`center`、`radius`、`forward`、`length`、`angle`、`isActive` 标志
- [x] 3.2 实现 `SetSphere(Vector3 center, float radius)` 方法，设置 Sphere 可视化参数并激活
- [x] 3.3 实现 `SetCapsule(Vector3 center, Vector3 forward, float radius, float length)` 方法，设置 Capsule 可视化参数并激活
- [x] 3.4 实现 `SetCone(Vector3 center, Vector3 forward, float length, float angle)` 方法，设置 Cone 可视化参数并激活
- [x] 3.5 实现 `Clear()` 方法，将 `isActive` 设为 false 以停止绘制
- [x] 3.6 实现 `OnDrawGizmos()`：当 isActive 时，根据 shapeType 绘制对应形状 — Sphere 用黄色 WireSphere，Capsule 用 WireSphere 起点终点加连线，Cone 用外围线段和圆弧

## 4. AttackEvent — 集成 AttackShapeDebugger

- [x] 4.1 在 `AttackEvent.OnStart` 中，通过 `owner.GetComponent<AttackShapeDebugger>()` 获取（若无则 AddComponent）Debugger，根据 attackData.shape 调用对应 Set 方法
- [x] 4.2 在 `AttackEvent.OnEnd` 中，调用 Debugger 的 `Clear()` 方法清除可视化
