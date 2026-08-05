# GAS 单元测试（EditMode）说明

## 概述

本测试覆盖 GAS（Gameplay Ability System）核心纯逻辑，全部为 **EditMode** 测试：
无需进入 Play 模式、无需场景、无帧级依赖（`Update`/`LateUpdate` 均不触发）。

测试程序集：**UCCS.GASTests**（`Assets/Scripts/Tests/`）
被测程序集：**UCCS.GASCore**（`Assets/Scripts/GASCore/`）

被测文件：

| 被测类 | 源文件 | 覆盖点 |
|---|---|---|
| `AttributeValue` | `Assets/Scripts/GASCore/AttributeValue.cs` | Default/MostPositive/MostNegative 聚合公式、Additive/Multiplicative/Override、StackCount 感知、Dirty 重算、OnPreAttributeChange 钳制、OnValueChanged |
| `GameplayTagSO` | `Assets/Scripts/GASCore/GameplayTagSO.cs` | 层级 `parentTag`、`HasChild` 祖先链匹配、`GetFullPath` 路径拼接 |
| `TagComponent` | `Assets/Scripts/GASCore/TagComponent.cs` | AddTag/RemoveTag 引用计数、ConsumeTag、AddTransientTag 单帧标签、HasTagOrChild 层级匹配、OnTagAdded |
| `AttributeSet` | `Assets/Scripts/GASCore/AttributeSet.cs` | Health 死亡事件、Poise 破防/恢复、Stamina 消耗与钳制、属性注册、OnAttributeChanged |

## 如何运行（Unity Test Runner）

1. 用 Unity 6000.1.9f1 打开工程（`com.unity.test-framework` 1.5.1 已在 `Packages/manifest.json` 中）。
2. 菜单 **Window > General > Test Runner** 打开测试运行器。
3. 选择 **EditMode** 选项卡。
4. 点击 **Run All** 运行全部 EditMode 测试；
   或在左侧列表展开 **UCCS.GASTests** 程序集，单独运行某个测试文件 / 单条用例。
5. 命令行方式（CI）：
   ```
   Unity.exe -projectPath . -runTests -testPlatform EditMode -testResults results.xml
   ```

## 测试文件与用例数

| 文件 | 用例数 | 说明 |
|---|---|---|
| `AttributeValueTests.cs` | 20 | 属性聚合核心逻辑 |
| `GameplayTagTests.cs` | 22 | GameplayTagSO 层级（8）+ TagComponent（14） |
| `AttributeSetTests.cs` | 14 | 属性集事件 / 消耗 / 钳制 |
| **合计** | **56** | |

## 程序集结构（重要）

```
┌─────────────────────────────────────────────────┐
│ UCCS.GASCore.asmdef (autoReferenced: true)      │  ← 被测代码（纯逻辑）
│   AttributeValue / AttributeModifier /          │
│   AttributeSet / TagComponent /                 │
│   GameplayTagSO / BuffSO /                      │
│   UCCS.IAttributeProvider / UCCS.IPlayerMarker /│
│   UCCS.IStackCountSource                        │
└──────────────────────┬──────────────────────────┘
                       │ autoReferenced（Assembly-CSharp 自动引用）
┌──────────────────────▼──────────────────────────┐
│ Assembly-CSharp（预定义程序集，其余全部脚本）     │
└──────────────────────┬──────────────────────────┘
                       │ references: ["UCCS.GASCore"]
┌──────────────────────▼──────────────────────────┐
│ UCCS.GASTests.asmdef (TestAssemblies, Editor)   │
│   3 个测试文件（56 用例）                        │
└─────────────────────────────────────────────────┘
```

### 为什么被测代码要独立 asmdef？

**Unity 6 (6000.x) 中，asmdef 程序集无法引用预定义程序集 `Assembly-CSharp`**
（实测编译参数 rsp 中无 Assembly-CSharp 引用；`references` 显式写也会被忽略）。
因此测试程序集若要访问 GAS 类，这些类必须位于**自己的 asmdef 程序集**中。
`UCCS.GASCore` 即为此创建：`autoReferenced: true` 使 Assembly-CSharp 自动引用它，
原有代码无需任何改动即可继续使用这些类型。

### 解耦设计（保证 GASCore 不反向依赖 Assembly-CSharp）

| 原依赖 | 解耦方式 |
|---|---|
| `AttributeModifier.Source: ActiveGameplayEffect` | 改为接口 `UCCS.IStackCountSource`（`ActiveGameplayEffect` 实现） |
| `AttributeSet : UCCS.IAttributeProvider` | 接口移入 GASCore（`Interfaces.cs` 保留其余接口） |
| `AttributeSet.Awake` 检测 `PlayerModel` | 改为 `UCCS.IPlayerMarker`（`PlayerModel` 实现该接口） |

## 实现要点 / 注意事项

1. **AttributeSet 的初始化方式**：EditMode 下 MonoBehaviour 的 `Awake`/`Start` 不会自动执行。
   测试使用公开 API `RegisterAttribute(...)` 建立属性字典（与 `Awake` 逻辑一致）。
2. **GameplayTagSO.HasChild 语义**：实现沿 `otherTag.parentTag` 链向上查找 `this`
   （即"`this` 是否是 `otherTag` 的祖先"）。无循环引用时 `HasChild(自身)` 返回 `false`。
3. **StackCount 测试**：使用 `FakeStackSource : UCCS.IStackCountSource` 轻量假实现
   （初始层数 + `AddStack()`），不再依赖 `ActiveGameplayEffect`/`GameplayEffect`。
4. **未覆盖（依赖过重/超出纯逻辑范围）**：
   - `TagComponent` 的 Buff 系统（`ApplyBuff`/`RemoveBuff`）依赖 `BuffSO` 资产与帧级 `Update` Tick；
   - `Update`/`LateUpdate` 中的缓存标签过期清理、瞬态标签清空逻辑属帧级行为；
   - `AttributeSet` 的 `HandleStaminaRecovery`/`HandlePoiseRecovery`（自然恢复）依赖 `Time.deltaTime` 帧循环；
   - `AbilitySystemComponent` 整体未覆盖（MonoBehaviour + 大量外部依赖，非纯逻辑）。

## 疑难排查

### CS0246: 找不到 AttributeSet / GameplayTagSO / TagComponent 等类型

确认 `UCCS.GASTests.asmdef` 的 `references` 包含 `"UCCS.GASCore"`，且
`UCCS.GASCore.asmdef` 的 `autoReferenced` 为 `true`。若 GASCore 被移动/改名，
同步更新 references。

### 修改 asmdef 后 Unity 不重新编译

脚本编译报错时 Unity 会挂起编译，后续文件变化不自动触发重编译。
**切回 Unity 编辑器窗口**（聚焦）即会重新编译；或点击 Console 错误条上的 Clear 后重试。
