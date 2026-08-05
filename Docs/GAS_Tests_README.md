# GAS 单元测试（EditMode）说明

## 概述

本测试覆盖 GAS（Gameplay Ability System）核心纯逻辑，全部为 **EditMode** 测试：
无需进入 Play 模式、无需场景、无帧级依赖（`Update`/`LateUpdate` 均不触发）。

测试程序集：**UCCS.GASTests**（`Assets/Scripts/Tests/`）

被测文件（只读，未修改）：

| 被测类 | 源文件 | 覆盖点 |
|---|---|---|
| `AttributeValue` | `Assets/Scripts/GASSystem/AttributeValue.cs` | Default/MostPositive/MostNegative 聚合公式、Additive/Multiplicative/Override、StackCount 感知、Dirty 重算、OnPreAttributeChange 钳制、OnValueChanged |
| `GameplayTagSO` | `Assets/Scripts/ScriptsObject/GameplayTagSO.cs` | 层级 `parentTag`、`HasChild` 祖先链匹配、`GetFullPath` 路径拼接 |
| `TagComponent` | `Assets/Scripts/GASSystem/TagComponent.cs` | AddTag/RemoveTag 引用计数、ConsumeTag、AddTransientTag 单帧标签、HasTagOrChild 层级匹配、OnTagAdded |
| `AttributeSet` | `Assets/Scripts/GASSystem/AttributeSet.cs` | Health 死亡事件、Poise 破防/恢复、Stamina 消耗与钳制、属性注册、OnAttributeChanged |

## 如何运行（Unity Test Runner）

1. 用 Unity 6000.1.9f1 打开工程（`com.unity.test-framework` 1.5.1 已在 `Packages/manifest.json` 中）。
2. 菜单 **Window > General > Test Runner** 打开测试运行器。
   > 打开 Test Runner 会自动定义 `UNITY_INCLUDE_TESTS`，测试程序集随之编译。
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

## 程序集配置说明（UCCS.GASTests.asmdef）

- **Editor 平台**（`includePlatforms: ["Editor"]`），勾选 **Test Assemblies** 等效配置：
  - `references`: `UnityEngine.TestRunner`、`UnityEditor.TestRunner`
  - `precompiledReferences`: `nunit.framework.dll`（Unity 官方以 `overrideReferences: true` + 该预编译引用识别测试程序集）
- **未设置** `No Engine References`——测试需要 UnityEngine API（`GameObject`、`AddComponent`、`ScriptableObject.CreateInstance`、`Mathf`）。
- 被测类位于预定义程序集 **Assembly-CSharp**（`Assets/Scripts/` 下无 asmdef）。
  自定义 asmdef 程序集默认即可引用预定义程序集，无需额外 `references`；设置 `overrideReferences` 只会接管预编译 DLL 引用，**不会**切断对 Assembly-CSharp 的引用。

## 实现要点 / 注意事项

1. **AttributeSet 的初始化方式**：EditMode 下 MonoBehaviour 的 `Awake`/`Start` 不会自动执行。
   测试使用公开 API `RegisterAttribute(...)` 建立属性字典（与 `Awake` 逻辑一致：
   注册 `OnValueChanged`/`OnPreAttributeChange` 回调 + 非负钳制）。
   这样既避免依赖生命周期，也避免触发 `Awake` 中的 `CompareTag("Player")`
   （`Player` 未在 `ProjectSettings/TagManager.asset` 注册，调用会输出报错日志）。

2. **GameplayTagSO.HasChild 语义**：实现沿 `otherTag.parentTag` 链向上查找 `this`
   （即“`this` 是否是 `otherTag` 的祖先”）。因此在**无循环引用**时 `HasChild(自身)` 返回
   `false`；`HasChild(子标签)` 返回 `true`；`HasChild(无关标签)` 返回 `false`。
   测试按实现的实际行为断言（`HasChild_Self_ReturnsFalseWithoutCycle`），
   未构造 `parentTag` 自环等病态结构（会令 `GetFullPath` 死循环）。

3. **StackCount 测试**：通过 `ScriptableObject.CreateInstance<GameplayEffect>()` +
   `new ActiveGameplayEffect(ge, null)` + `AddStack()` 构造带 `Source` 的修改器，
   验证 `effectiveValue = value × Source.CurrentStacks`。

4. **未覆盖（依赖过重/超出纯逻辑范围）**：
   - `TagComponent` 的 Buff 系统（`ApplyBuff`/`RemoveBuff`）依赖 `BuffSO` 资产与帧级 `Update` Tick，未覆盖；
   - `Update`/`LateUpdate` 中的缓存标签过期清理、瞬态标签清空逻辑属帧级行为，未覆盖；
   - `AttributeSet` 的 `HandleStaminaRecovery`/`HandlePoiseRecovery`（自然恢复）依赖 `Time.deltaTime` 帧循环，未覆盖；
   - `AbilitySystemComponent` 整体未覆盖（MonoBehaviour + 大量外部依赖，非纯逻辑）。
   这些均不影响 EditMode 纯逻辑测试的完整性。
