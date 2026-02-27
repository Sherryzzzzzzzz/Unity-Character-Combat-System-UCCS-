## ADDED Requirements

### Requirement: 运行时攻击形状 Gizmos 可视化
系统 MUST 提供 `AttackShapeDebugger` MonoBehaviour，在 OnDrawGizmos 中绘制当前攻击检测形状。AttackEvent 在 OnStart 时 SHALL 将形状参数写入 owner 的 AttackShapeDebugger，在 OnEnd 时 SHALL 清除。

#### Scenario: Sphere 形状运行时可视化
- **WHEN** AttackEvent 以 Sphere 形状执行攻击
- **THEN** owner 的 Scene 视图中 SHALL 显示半透明黄色 WireSphere，中心和半径与检测参数一致

#### Scenario: Capsule 形状运行时可视化
- **WHEN** AttackEvent 以 Capsule 形状执行攻击
- **THEN** Scene 视图中 SHALL 显示 WireSphere 在起点和终点，以及连接线段

#### Scenario: Cone 形状运行时可视化
- **WHEN** AttackEvent 以 Cone 形状执行攻击
- **THEN** Scene 视图中 SHALL 显示锥形的外围线段和圆弧

#### Scenario: 攻击结束后清除可视化
- **WHEN** AttackEvent.OnEnd 被调用
- **THEN** AttackShapeDebugger SHALL 停止绘制（清除缓存参数）

### Requirement: 调试日志输出
AttackEvent 在执行形状检测时 MUST 输出调试日志，包含检测中心坐标、半径、命中碰撞体数量。

#### Scenario: ExecuteSphere 输出检测信息
- **WHEN** ExecuteSphere 执行完成
- **THEN** SHALL 输出 Debug.Log 包含 center、radius 和 hits.Length
