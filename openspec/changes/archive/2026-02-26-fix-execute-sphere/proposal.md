## Why

AttackEvent 中 `ExecuteSphere` 方法在运行时不生效 — 使用 `Physics.OverlapSphere` 检测目标时未指定 LayerMask，导致可能被无关碰撞体干扰或遗漏目标。同时缺乏运行时可视化，无法直观调试球形检测的中心位置和半径是否正确。需要修复检测逻辑并添加运行时 Gizmos 绘制以辅助调试。

## What Changes

- 修复 `ExecuteSphere` — 为 `Physics.OverlapSphere` 添加 LayerMask 参数过滤目标层，并添加 Debug.Log 输出检测结果
- 修复 `ExecuteCone` 和 `ExecuteCapsule` — 同样添加 LayerMask 支持保持一致
- 为 AttackEvent 的所有形状检测添加运行时 Gizmos 可视化（Gizmos.DrawWireSphere 等）
- 在 AttackData 中添加可配置的 `hitLayerMask` 字段

## Capabilities

### New Capabilities
- `attack-shape-debug`: 运行时攻击形状可视化调试系统（Gizmos 绘制 Sphere/Capsule/Cone）

### Modified Capabilities
- `unified-damage-pipeline`: ExecuteSphere/ExecuteCapsule/ExecuteCone 需要添加 LayerMask 过滤参数

## Impact

- **AttackEvent.cs**: ExecuteSphere/ExecuteCapsule/ExecuteCone 方法签名变更，添加 LayerMask 参数
- **AttackData.cs**: 新增 hitLayerMask 字段
- **运行时调试**: 新增 AttackShapeDebugger MonoBehaviour 或在 AttackEvent 中通过 Gizmos 回调绘制形状
