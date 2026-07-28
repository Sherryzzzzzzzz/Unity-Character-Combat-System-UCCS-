# 性能优化指南

## 当前状态

| 指标 | 数值 | 评级 |
|------|------|------|
| 场景 GameObject 总数 | 780 | 🔴 过多 |
| 每帧 Update 调用 | 22 个 | 🟡 尚可 |
| 每帧 LateUpdate 调用 | 8 个 | 🟡 尚可 |
| 阴影投射 Renderer | 8/8 (100%) | 🔴 全部开阴影 |
| 方向光阴影类型 | Soft | 🔴 最贵 |
| BTreeRunner Tick | 0.15s (~7Hz) | 🟢 已优化 |

---

## 1. CPU 优化

### 1.1 删除每帧 Debug.Log

**问题**：`PlayerController.cs:237` — `Debug.Log(isGround)` 每帧写日志，这是最严重的性能杀手。

```csharp
// ❌ 之前
Debug.Log(isGround);

// ✅ 之后
// 完全删除
```

> Debug.Log 会触发字符串格式化 + Unity Console 刷新 + Editor 重绘，单条就能吃掉 2-5ms。

---

### 1.2 缓存 GetComponent 调用

**问题**：EnemyModel 和 PlayerModel 的 `Update()` 里每帧调 `GetComponent<HurtBoxManager>()`。

```csharp
// ❌ 之前 (EnemyModel.Update)
isHitting = GetComponent<HurtBoxManager>().isHitting;

// ✅ 之后 (Awake 里缓存，Update 里直接用)
private HurtBoxManager _hbm;
void Awake() { _hbm = GetComponent<HurtBoxManager>(); }
void Update() { isHitting = _hbm.isHitting; }
```

PlayerModel 同理 — `GetComponent<HurtBoxManager>()` 也每帧在调。

---

### 1.3 降频 Physics 查询

**问题**：`PlayerModel.DetectNearestEnemy()` 每帧跑 `Physics.OverlapSphere`，频率太高。

```csharp
// ✅ 改为每 0.3 秒检测一次
private float _detectTimer;
void Update()
{
    _detectTimer += Time.deltaTime;
    if (_detectTimer >= 0.3f)
    {
        _detectTimer = 0f;
        DetectNearestEnemy();
    }
}
```

---

### 1.4 合并 MonoManager 的 Update 委托

**问题**：`PlayerStateBase` 在 Enter 时 `MonoManager.Instance.AddUpdateAction(Update)`，Exit 时 Remove。每个状态切换都涉及委托操作。多个状态同时激活会导致多次回调。

**优化**：改为状态机自驱动 — StateMachine 只有一个 Update，内部路由到当前状态。

---

### 1.5 减少 Animancer Layer 数量

**问题**：`PlayerSkillComponent` 和 `Parryable` 都动态创建 Layer（最多 4 层）。每层都有独立的 Animator 状态评估。

**优化**：Layer 2（受击）和 Layer 3（弹反）可共享，减少为 3 层。

---

## 2. GPU / 渲染优化

### 2.1 方向光阴影

**问题**：Directional Light 使用 Soft Shadows（最贵的阴影类型），8 个 Renderer 全部投射阴影。

```diff
- Shadow Type: Soft Shadows
+ Shadow Type: Hard Shadows  (或 None，如果不需要)

- Shadow Distance: 150 (默认)
+ Shadow Distance: 30
```

> Soft Shadows 每帧需要多次采样，开销是 Hard Shadows 的 4-8 倍。

---

### 2.2 无限网格 Shader

**问题**：`InfiniteGrid.shader` 的 Fragment Shader 包含 `GetMainLight()`、`SampleSH()`、三角函数和多个 `smoothstep`。

**优化**：
- 去掉 `SampleSH()` 调用（用常量替代环境光）
- 去掉 `GetMainLight()` shadowAttenuation 采样
- 合并 `smoothstep` 计算

预估节省：0.5-1ms per frame（大面积覆盖屏幕时）。

---

### 2.3 场景裁剪

**问题**：780 个 GameObject 即使不在视野内也在场景中。

**优化**：
- 给远处建筑开 Occlusion Culling
- 不必要的装饰物关掉阴影投射 (`ShadowCastingMode.Off`)
- 合并静态网格（Static Batching）

---

## 3. 内存 / GC 优化

| 问题 | 位置 | 修复 |
|------|------|------|
| 每帧 `new List` | BTreeRunner Update | 复用列表 |
| 每帧 `new Dictionary` | Timeline events | 改为对象池 |
| `GetComponent` 返回 null 时装箱 | 多处 | 缓存引用 |
| `Debug.Log` 字符串拼接 | PlayerController:237 | 删除 |

---

## 4. 优先级排序（从高到低）

| 优先级 | 优化项 | 预估提升 | 实现难度 |
|--------|--------|---------|---------|
| 🔴 P0 | 删除 Debug.Log(isGround) | +5-10 FPS | 1 行改动 |
| 🔴 P0 | 缓存 GetComponent | +2-3 FPS | 5 行改动 |
| 🟡 P1 | 降频 DetectNearestEnemy | +2-3 FPS | 5 行改动 |
| 🟡 P1 | 阴影改 Hard + 降距离 | +5-15 FPS | Inspector 改 |
| 🟢 P2 | 简化 InfiniteGrid shader | +1-2 FPS | 10 行改动 |
| 🟢 P2 | 合并 MonoManager Update | +1-2 FPS | 结构改动 |
| 🟢 P3 | 场景 Occlusion Culling | +3-8 FPS | Editor 操作 |
| 🟢 P3 | 减少 Animancer Layer | +1 FPS | Inspector 改 |

---

## 5. 快速见效（P0 + P1）

```
1. 删除 PlayerController.cs 第 237 行 Debug.Log(isGround);
2. EnemyModel.Awake() 里缓存 _hbm = GetComponent<HurtBoxManager>();
3. PlayerModel.Awake() 里缓存 _hbm = GetComponent<HurtBoxManager>();
4. PlayerModel 加 _detectTimer 降频 DetectNearestEnemy()
5. Inspector: Directional Light → Shadows: Hard, Shadow Distance: 30
```

这 5 项改动加起来约 10 行代码 + 2 个 Inspector 调整，预计提升 **15-30 FPS**。
