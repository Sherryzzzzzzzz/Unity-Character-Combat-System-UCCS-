# 原创行为树系统 — 设计文档

## 概述

替换 Behavior Designer 插件，自研行为树系统，包含：
- **运行时** (`Assets/Scripts/AI/`)：基于 Tick 的 BT 执行引擎
- **编辑器** (`Assets/Editor/AI/`)：可视化节点图编辑器，支持拖拽连线
- **资产**：`BTreeAsset` ScriptableObject 存储树结构

---

## 1. 整体架构

```
┌──────────────────────────────────────────────────┐
│                    编辑器                         │
│  BTreeEditorWindow (UI Toolkit / GraphView)       │
│    ├── 右键菜单创建节点                            │
│    ├── 拖拽端口连线                                │
│    ├── 底部属性面板                                │
│    └── 保存到 BTreeAsset.asset                    │
├──────────────────────────────────────────────────┤
│                    资产                           │
│  BTreeAsset : ScriptableObject                    │
│    ├── 根节点（序列化的节点树）                     │
│    └── Blackboard 键值定义                        │
├──────────────────────────────────────────────────┤
│                    运行时                          │
│  BTreeRunner : MonoBehaviour                      │
│    ├── 引用 BTreeAsset                            │
│    ├── 每帧 Tick() 驱动整棵树                      │
│    └── Blackboard（运行时共享数据）                │
│                                                   │
│  BTNode（抽象基类）                                │
│    ├── BTComposite    组合节点                     │
│    │     ├── Sequence  顺序（与）                  │
│    │     └── Selector  选择（或）                  │
│    ├── BTDecorator    装饰节点                     │
│    │     ├── Inverter  取反                       │
│    │     ├── Repeater  重复                       │
│    │     ├── Wait      等待                       │
│    │     └── Succeeder 永远成功                   │
│    └── BTAction       动作/叶子节点                │
│          ├── MoveTo            随机移动            │
│          ├── PlaySkill         播放技能            │
│          ├── SetAnimationState 切换动画状态         │
│          └── WaitForCondition  条件等待            │
└──────────────────────────────────────────────────┘
```

## 2. 数据结构 — 行为树在内存和硬盘上如何存储

### 2.1 资产层（硬盘）

```csharp
// BTreeAsset.cs — ScriptableObject，存盘用
[CreateAssetMenu(menuName = "AI/BTree Asset")]
public class BTreeAsset : ScriptableObject
{
    [SerializeReference]  // ← 关键：让多态子类能被 Unity 序列化
    public BTNode rootNode;                 // 树的根节点

    public List<BlackboardEntry> blackboard; // 黑板键定义
}

// Blackboard 条目
[System.Serializable]
public class BlackboardEntry
{
    public string key;         // 键名，如 "player"、"targetPos"
    public BlackboardType type; // float / int / bool / Vector3 / GameObject / Transform
}
```

### 2.2 节点层（内存树结构）

```csharp
// BTNode.cs — 所有节点的抽象基类
public abstract class BTNode
{
    // ---- 硬盘数据 ----
    public string guid;           // 唯一 ID（编辑器中定位节点用）
    public Vector2 editorPosition; // 编辑器画布坐标

    // ---- 运行时数据 ----
    [System.NonSerialized]
    public BTNode parent;         // 父节点（运行时赋值）
    [System.NonSerialized]
    public NodeState state;       // Inactive / Running / Success / Failure
    [System.NonSerialized]
    protected BTreeRunner runner; // 所属的 Runner，用于访问黑板

    // ---- 生命周期 ----
    public virtual void OnEnter() { state = NodeState.Running; }
    public abstract NodeState OnTick();         // 每帧调用
    public virtual void OnExit()  { }
}

// BTComposite.cs — 有多个子节点
public abstract class BTComposite : BTNode
{
    [SerializeReference]
    public List<BTNode> children = new();  // ← 子节点列表

    protected int currentIndex; // 当前执行到第几个子节点
}

// BTDecorator.cs — 只有 1 个子节点
public abstract class BTDecorator : BTNode
{
    [SerializeReference]
    public BTNode child;  // ← 被装饰的唯一子节点
}

// BTAction.cs — 叶子节点，没有子节点
public abstract class BTAction : BTNode
{
    // 无 children，只有自己的业务逻辑
}
```

### 2.3 树的物理结构示意

```
BTreeAsset (ScriptableObject)
  │
  └── rootNode : BTComposite (Sequence)
        │
        ├── children[0] : BTDecorator (Condition "HP>50%")
        │     └── child : BTAction (PlaySkill "重击")
        │
        ├── children[1] : BTDecorator (Condition "距离<2m")
        │     └── child : BTAction (PlaySkill "横扫")
        │
        └── children[2] : BTAction (MoveTo)
              // 叶子节点，children = null
```

每个节点的 `guid` 用于编辑器唯一标识。`parent` 在运行时由 `BTreeRunner.Play()` 遍历树赋值。`[SerializeReference]` 让 Unity 序列化系统能正确存储 `BTComposite` / `BTDecorator` / `BTAction` 这些多态子类。

## 3. 节点类型详解

### 3.1 组合节点（控制流）

| 节点 | 图标 | 行为 |
|------|------|------|
| **Sequence** 顺序 | → | 从左到右依次执行子节点。任一返回失败则整体失败。全部成功则整体成功。 |
| **Selector** 选择 | ? | 从左到右依次尝试子节点。任一返回成功则整体成功。全部失败则整体失败。 |
| **RandomSelector** 随机 | 🎲 | 从子节点中随机选一个执行。可选加权随机（每个子节点设 weight）。 |
| **PrioritySelector** 优先 | ⇑ | 类似 Selector，但每帧重新从第一个子节点开始评估（用于持续条件判断）。 |

### 3.2 装饰节点（修饰单个子节点）

| 节点 | 图标 | 行为 |
|------|------|------|
| **Condition** 条件 | ◆ | **[BOSS 战核心]** — 若黑板条件满足则执行子节点，否则直接返回失败。支持：比较黑板值、判断距离、判断 HP 百分比、判断 GameplayTag。 |
| **Cooldown** 冷却 | ⏳ | **[BOSS 战核心]** — 执行完子节点后进入冷却，冷却期间返回失败。防止技能连发。 |
| **Inverter** 取反 | ¬ | 翻转子节点结果 |
| **Repeater** 重复 | ↻ | 重复执行子节点 N 次（或无限循环） |
| **Wait** 等待 | ⏱ | 等待指定秒数后执行子节点 |
| **Succeeder** 永真 | ✓ | 无论子节点返回什么，永远返回成功 |

### 3.3 动作节点（叶子 — 干活）

| 节点 | 用途 | 参数 |
|------|------|------|
| **MoveTo** 移动 | 移动到玩家周围的随机位置 | `模式`（圆形/左右平移/冲向玩家）、`半径`、`停止距离` |
| **PlaySkill** 技能 | 播放技能动画 | `技能资产`（SkillTimelineAsset） |
| **SetAnimationState** 动画 | 切换动画状态 | `状态`（Idle/Move） |
| **WaitForCondition** 等条件 | 等待 GameplayTag 条件满足 | `条件Tag`、`超时` |
| **SetBlackboard** 设黑板 | 修改黑板值 | `键`、`值`（用于标记阶段、计数器等） |

### 3.4 子树引用（模块化）

| 节点 | 用途 |
|------|------|
| **SubTree** 子树 | 引用另一个 `BTreeAsset`，作为当前树的一个节点展开。用于：攻击模式库、阶段行为组等可复用模块。 |

### 3.5 节点状态

每个节点 Tick 后返回三种状态之一：

| 状态 | 含义 |
|------|------|
| `Success` | 执行成功，父节点继续下一个 |
| `Failure` | 执行失败，父节点根据类型决定行为 |
| `Running` | 还在执行中，下一帧继续 Tick |

## 4. 能不能搭完整 BOSS 战？

### 4.1 典型 BOSS 战需求分析

| BOSS 行为 | 需要的节点 | 是否满足 |
|-----------|-----------|---------|
| **阶段切换**（100%→70%→40% 血量） | Condition 查黑板 "currentPhase" + PrioritySelector | ✅ Condition + SetBlackboard |
| **距离判断**（远=突进，近=近战，中=远程） | Condition "distance < 2m" | ✅ Condition 支持距离判断 |
| **技能池**（60%普攻 / 25%技能A / 15%技能B） | RandomSelector + 权重 | ✅ RandomSelector |
| **不会连续放同一个技能** | Cooldown 装饰器 | ✅ Cooldown |
| **受击时立刻反击** | PrioritySelector 首位置检测 "isHit" tag | ✅ WaitForCondition + PrioritySelector |
| **爆发后进虚弱期**（防御降低） | PlaySkill 爆发序列 → SetBlackboard "isExhausted=true" → Wait | ✅ |
| **血量低时狂暴**（攻速加快） | Condition "HP% < 30%" → SetBlackboard "moveSpeed=2x" | ✅ |
| **召唤小怪**（一次性触发） | Condition "HP% < 50%" + Cooldown(once) → PlaySkill "召唤" | ✅ |
| **连招**（3段不能被打断） | Sequence → PlaySkill×3 | ✅ |
| **模块化复用**（多个BOSS共用攻击模式） | SubTree | ✅ |

### 4.2 示例：一个完整的 BOSS 行为树

```
Root (PrioritySelector)           ← 每帧重新评估，高优先级行为先执行
│
├── [1] Sequence "受击反击"
│     ├── Condition: HP% < 30%    ← 仅低血量触发
│     ├── Cooldown: 8s
│     └── PlaySkill "狂暴强化"
│
├── [2] Condition "距离 < 2m"
│     └── RandomSelector "近战攻击池"
│           ├── (60%) Sequence → PlaySkill "三连斩"
│           ├── (25%) Sequence → PlaySkill "上挑"
│           └── (15%) Sequence → PlaySkill "投技"
│
├── [3] Condition "2m ≤ 距离 < 8m"
│     └── RandomSelector "中距离攻击池"
│           ├── (50%) Sequence → MoveTo(冲向玩家) → PlaySkill "突刺"
│           ├── (30%) SubTree → "远程AOE模式"
│           └── (20%) PlaySkill "跳劈"
│
└── [4] (兜底) Sequence "追击"
      ├── MoveTo(冲向玩家)
      └── Wait 0.5s
```

### 4.3 结论

**✅ 够用。** 加入 Condition、Cooldown、RandomSelector、PrioritySelector、SubTree 后，可以搭建完整的 BOSS 战行为树。层次清晰：PrioritySelector → Condition 分阶段/距离 → RandomSelector 攻击池 → Sequence 连招。模块化用 SubTree 复用。

## 3. Blackboard（黑板）

节点间共享数据的键值存储。

```
Blackboard:
  ┌─────────────────────┬──────────────┐
  │ 键                  │ 类型         │
  ├─────────────────────┼──────────────┤
  │ "player"            │ Transform    │
  │ "目标位置"           │ Vector3      │
  │ "上次攻击时间"        │ float        │
  │ "移动速度"           │ float        │
  │ "动画数据"           │ EnemyAnimData│
  └─────────────────────┴──────────────┘
```

- **编辑时**：在 BTreeAsset 的 Inspector 里定义键名 + 类型
- **运行时**：`blackboard.Get<Transform>("player")`、`blackboard.Set("目标位置", pos)`

## 4. 运行时执行

### BTreeRunner 组件

```csharp
public class BTreeRunner : MonoBehaviour
{
    public BTreeAsset treeAsset;     // 要运行的行为树
    public float tickInterval = 0f;  // 0 = 每帧Tick; >0 = 限频
    public bool runOnStart = true;   // Start时自动运行
    
    private Blackboard blackboard;   // 运行时黑板
    private BTNode rootInstance;     // 从 asset 克隆出来的运行实例
    
    void Start()  { if (runOnStart) Play(); }
    void Update() { Tick(); }
    
    public void Play();  // 实例化树，开始运行
    public void Pause(); // 暂停
    public void Stop();  // 中止所有节点
}
```

### Tick 流程

```
BTreeRunner.Tick()
  → rootInstance.Tick()
    → Sequence.Tick()
      → child[0].Tick()  // MoveTo: 移动中返回 Running
      → child[1].Tick()  // PlaySkill: 技能播放中返回 Running
      → child[2].Tick()  // Wait: 计时中返回 Running
```

## 5. 编辑器设计

### 窗口布局

```
┌─────────────────────────────────────────────────────────┐
│  行为树编辑器                          [保存] [调试]      │
├────────────┬────────────────────────────────────────────┤
│            │                                            │
│  节点面板   │      ┌──────┐                              │
│            │      │ Root │                              │
│ ┌────────┐ │      └──┬───┘                              │
│ │Sequence│ │         │                                  │
│ └────────┘ │     ┌───┴───┐                              │
│ ┌────────┐ │     │Selectr│                              │
│ │Selector│ │     └─┬───┬─┘      ← 编辑画布               │
│ └────────┘ │       │   │        （可缩放、平移）          │
│ ┌────────┐ │    ┌──┴┐ ┌┴──┐                             │
│ │ MoveTo │ │    │移动│ │技能│                            │
│ └────────┘ │    └───┘ └───┘                             │
│ ┌────────┐ │                                            │
│ │PlaySkil│ │                                            │
│ └────────┘ │                                            │
│ ┌────────┐ │                                            │
│ │  Wait  │ │                                            │
│ └────────┘ │                                            │
│            │                                            │
├────────────┴────────────────────────────────────────────┤
│  属性面板                                                 │
│  节点: MoveTo  模式: [圆形区域 ▼]  半径: 8  停止距离: 0.5  │
└─────────────────────────────────────────────────────────┘
```

### 功能清单

- **右键画布** → 弹出"添加节点"菜单，按类别分组
- **拖拽端口**（节点底部圆点）→ 连线到子节点
- **点击节点** → 底部面板显示该节点的属性
- **Delete 键** → 删除选中节点及其所有子节点
- **Ctrl+C/V** → 复制粘贴节点
- **滚轮** → 缩放画布
- **中键拖拽** → 平移画布
- **Ctrl+Z/Y** → 撤销/重做
- **小地图** → 画布右下角

## 6. 与现有代码的接入

### EnemyModel 改动

```diff
- public BehaviorTree tree;          // Behavior Designer 的
+ public BTreeRunner bTreeRunner;    // 我们的

// 受击时暂停行为树
- tree.DisableBehavior();
+ bTreeRunner.Pause();

// 受击结束恢复行为树
- tree.EnableBehavior();
+ bTreeRunner.Play();
```

### 现有自定义 Action 的迁移

`MoveTo.cs` 和 `PlaySkill.cs` 的核心逻辑**完全复用**，只改继承的基类：

```diff
- using BehaviorDesigner.Runtime;
- using BehaviorDesigner.Runtime.Tasks;
- public class MoveTo : Action { ... }

+ public class BTA_MoveTo : BTAction { ... }  // 逻辑不动
```

## 7. 文件结构

```
Assets/Scripts/AI/
  ├── Core/
  │   ├── BTNode.cs                // 抽象基类（Tick、OnEnter、OnExit、guid、parent 引用）
  │   ├── BTNodeState.cs           // 枚举：Inactive / Running / Success / Failure
  │   ├── BTComposite.cs           // Sequence + Selector + RandomSelector + PrioritySelector
  │   ├── BTDecorator.cs           // Condition + Cooldown + Inverter + Repeater + Wait + Succeeder
  │   ├── BTAction.cs              // 动作节点抽象基类
  │   ├── BTreeRunner.cs           // MonoBehaviour，挂在敌人上跑树
  │   ├── BTBlackboard.cs          // 键值对黑板 + BlackboardEntry
  │   └── BTConditionEvaluator.cs  // Condition 条件评估器（HP、距离、Tag、黑板值比较）
  ├── Actions/
  │   ├── BTA_MoveTo.cs            // 随机移动 / 冲向玩家
  │   ├── BTA_PlaySkill.cs         // 播放技能
  │   ├── BTA_SetAnimationState.cs // 切换动画状态
  │   ├── BTA_WaitForCondition.cs  // 条件等待（GameplayTag）
  │   └── BTA_SetBlackboard.cs     // 修改黑板值
  └── BTreeAsset.cs                // ScriptableObject 资产（rootNode + blackboard 定义）

Assets/Editor/AI/
  ├── BTreeEditorWindow.cs         // 编辑器主窗口
  ├── BTreeGraphView.cs            // GraphView 画布（右键菜单、连线、缩放）
  ├── BTreeGraphNode.cs            // 画布上的可视化节点（按类型着色）
  └── BTreeAssetEditor.cs          // 资产 Inspector + 黑板编辑器
```

## 8. 迁移步骤

## 8. 迁移步骤

1. **新建运行时 + 编辑器**（只加新文件，不动现有代码）
2. **创建 BTreeAsset**，把敌人现在的行为编辑进去
3. **换组件**：Enemy GameObject 上 `BehaviorTree` → `BTreeRunner`
4. **删除** `Assets/Scripts/BehaviorDesigner/MoveTo.cs` 和 `PlaySkill.cs`
5. **卸载** Behavior Designer 插件

## 9. 范围说明

| ✅ 做 | ❌ 不做 |
|-------|--------|
| Sequence、Selector、RandomSelector、PrioritySelector | Parallel 并行节点（BOSS 战不需要） |
| Condition、Cooldown、Inverter、Repeater、Wait、Succeeder | 可视化动画过渡编辑 |
| 黑板（float/int/bool/Vector3/GameObject/Transform） | 复杂类型（List、自定义 class） |
| SubTree 子树引用 | 子树编辑器内联展开 |
| UI Toolkit GraphView 可视化编辑 | 运行时调试高亮（可后续加） |
| Tick 单线程驱动 | 多线程 |
| 5 个动作 + 可自定义扩展 | 预置动作库 |
| 约 2000 行代码（含编辑器） | 过度工程 |

## 11. 示例：小怪 vs BOSS 行为树对比

### 小怪行为树（当前敌人）
```
Root (Sequence → Repeater 无限)
  ├── MoveTo (圆形区域, 半径=8)
  ├── PlaySkill (攻击_01)
  └── Wait (1.5s)
```
简单循环：走路→攻击→等待→重复。小怪用这套就够了。

### BOSS 行为树（完整版）
```
Root (PrioritySelector)                     ← 每帧重新评估
│
├── [优先级1] "低血量终结技"
│   ├── Condition: HP% ≤ 20%
│   ├── Cooldown: 60s
│   └── Sequence
│         ├── SetBlackboard: isEnraged = true
│         ├── PlaySkill "狂暴变身"
│         └── PlaySkill "全屏AOE"
│
├── [优先级2] "受击反击"
│   ├── Condition: hasTag("gotHit")
│   └── RandomSelector
│         ├── (70%) PlaySkill "受击后撤"
│         └── (30%) PlaySkill "受击反击"
│
├── [优先级3] "近距离攻击"
│   ├── Condition: distance < 2m
│   └── RandomSelector (权重)
│         ├── (50%) SubTree "三连斩模式"
│         ├── (25%) PlaySkill "震地"
│         └── (25%) PlaySkill "抓取技"
│
├── [优先级4] "中距离攻击"
│   ├── Condition: 2m ≤ distance < 8m
│   └── RandomSelector
│         ├── (40%) Sequence → MoveTo(冲向玩家) → PlaySkill "突刺"
│         ├── (35%) SubTree "远程AOE模式"
│         └── (25%) PlaySkill "跳劈"
│
└── [优先级5] "追击"（兜底）
      └── Sequence
            ├── MoveTo (冲向玩家)
            └── Wait 0.5s
```

层级清晰：优先判断血量/状态 → 距离分流 → 攻击池随机 → 连招组合。新加一个 BOSS 只需要换 `BTreeAsset`。
