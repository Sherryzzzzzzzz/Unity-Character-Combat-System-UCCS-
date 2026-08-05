using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// AttributeValue 纯逻辑单元测试（EditMode）：
/// 覆盖 Default / MostPositive / MostNegative 聚合模式、
/// Additive / Multiplicative / Override 修改器、StackCount 感知、
/// Dirty 重算、OnPreAttributeChange 钳制与 OnValueChanged 回调。
/// </summary>
[TestFixture]
public class AttributeValueTests
{
    private readonly List<Object> _trackedObjects = new List<Object>();

    [TearDown]
    public void TearDown()
    {
        foreach (var obj in _trackedObjects)
        {
            if (obj != null) Object.DestroyImmediate(obj);
        }
        _trackedObjects.Clear();
    }

    /// <summary>
    /// 创建一个带 Source（ActiveGameplayEffect）的堆叠效果，用于 StackCount 感知测试。
    /// </summary>
    private ActiveGameplayEffect CreateStackableEffect(int maxStacks)
    {
        var ge = ScriptableObject.CreateInstance<GameplayEffect>();
        _trackedObjects.Add(ge);
        ge.maxStacks = maxStacks;
        return new ActiveGameplayEffect(ge, null);
    }

    // ==================== Default 模式 ====================

    [Test]
    public void DefaultMode_NoModifiers_ReturnsBaseValue()
    {
        var attr = new AttributeValue(100f);
        Assert.AreEqual(100f, attr.GetCurrentValue(), 0.0001f);
    }

    [Test]
    public void DefaultMode_AggregatesAdditiveAndMultiplicative()
    {
        var attr = new AttributeValue(100f);
        attr.AddModifier(new AttributeModifier(ModifierType.Additive, 10f));
        attr.AddModifier(new AttributeModifier(ModifierType.Multiplicative, 0.5f));
        // (Base + ΣAdd) × (1 + ΣMult) = (100 + 10) × 1.5 = 165
        Assert.AreEqual(165f, attr.GetCurrentValue(), 0.0001f);
    }

    [Test]
    public void DefaultMode_AccumulatesMultipleMultiplicatives()
    {
        var attr = new AttributeValue(100f);
        attr.AddModifier(new AttributeModifier(ModifierType.Multiplicative, 0.5f));
        attr.AddModifier(new AttributeModifier(ModifierType.Multiplicative, 0.25f));
        // 100 × (1 + 0.5 + 0.25) = 175
        Assert.AreEqual(175f, attr.GetCurrentValue(), 0.0001f);
    }

    [Test]
    public void DefaultMode_OverrideTakesLastValue()
    {
        var attr = new AttributeValue(100f);
        attr.AddModifier(new AttributeModifier(ModifierType.Override, 200f));
        attr.AddModifier(new AttributeModifier(ModifierType.Override, 300f));
        Assert.AreEqual(300f, attr.GetCurrentValue(), 0.0001f);
    }

    [Test]
    public void DefaultMode_OverrideIgnoresOtherModifiers()
    {
        var attr = new AttributeValue(100f);
        attr.AddModifier(new AttributeModifier(ModifierType.Additive, 50f));
        attr.AddModifier(new AttributeModifier(ModifierType.Multiplicative, 0.5f));
        attr.AddModifier(new AttributeModifier(ModifierType.Override, 250f));
        Assert.AreEqual(250f, attr.GetCurrentValue(), 0.0001f);
    }

    // ==================== MostPositive / MostNegative 模式 ====================

    [Test]
    public void MostPositiveMode_TakesLargestPositiveAdditiveOnly()
    {
        var attr = new AttributeValue(100f) { Mode = AggregatorMode.MostPositive };
        attr.AddModifier(new AttributeModifier(ModifierType.Additive, 5f));
        attr.AddModifier(new AttributeModifier(ModifierType.Additive, 20f));
        attr.AddModifier(new AttributeModifier(ModifierType.Additive, -10f));
        Assert.AreEqual(120f, attr.GetCurrentValue(), 0.0001f); // 100 + 20
    }

    [Test]
    public void MostPositiveMode_IgnoresNegativeOnlyAdditives()
    {
        var attr = new AttributeValue(100f) { Mode = AggregatorMode.MostPositive };
        attr.AddModifier(new AttributeModifier(ModifierType.Additive, -10f));
        // 只有负向 additive 时取 Max(0, best) = 0
        Assert.AreEqual(100f, attr.GetCurrentValue(), 0.0001f);
    }

    [Test]
    public void MostPositiveMode_StillAppliesMultiplicative()
    {
        var attr = new AttributeValue(100f) { Mode = AggregatorMode.MostPositive };
        attr.AddModifier(new AttributeModifier(ModifierType.Additive, 20f));
        attr.AddModifier(new AttributeModifier(ModifierType.Multiplicative, 0.5f));
        Assert.AreEqual(180f, attr.GetCurrentValue(), 0.0001f); // (100 + 20) × 1.5
    }

    [Test]
    public void MostNegativeMode_TakesMostNegativeAdditiveOnly()
    {
        var attr = new AttributeValue(100f) { Mode = AggregatorMode.MostNegative };
        attr.AddModifier(new AttributeModifier(ModifierType.Additive, -5f));
        attr.AddModifier(new AttributeModifier(ModifierType.Additive, -20f));
        attr.AddModifier(new AttributeModifier(ModifierType.Additive, 10f));
        Assert.AreEqual(80f, attr.GetCurrentValue(), 0.0001f); // 100 - 20
    }

    [Test]
    public void MostNegativeMode_IgnoresPositiveOnlyAdditives()
    {
        var attr = new AttributeValue(100f) { Mode = AggregatorMode.MostNegative };
        attr.AddModifier(new AttributeModifier(ModifierType.Additive, 10f));
        // 只有正向 additive 时取 Min(0, best) = 0
        Assert.AreEqual(100f, attr.GetCurrentValue(), 0.0001f);
    }

    // ==================== StackCount 感知 ====================

    [Test]
    public void StackCount_DoublesAdditiveMagnitude()
    {
        var attr = new AttributeValue(100f);
        var effect = CreateStackableEffect(5);
        attr.AddModifier(new AttributeModifier(ModifierType.Additive, 10f, effect));
        Assert.AreEqual(110f, attr.GetCurrentValue(), 0.0001f); // stacks = 1 → 10 × 1

        effect.AddStack();
        Assert.AreEqual(2, effect.CurrentStacks);
        attr.SetDirty();
        Assert.AreEqual(120f, attr.GetCurrentValue(), 0.0001f); // stacks = 2 → 10 × 2
    }

    [Test]
    public void StackCount_MultipliesMultiplicativeMagnitude()
    {
        var attr = new AttributeValue(100f);
        var effect = CreateStackableEffect(3);
        effect.AddStack(); // stacks = 2
        attr.AddModifier(new AttributeModifier(ModifierType.Multiplicative, 0.1f, effect));
        Assert.AreEqual(120f, attr.GetCurrentValue(), 0.0001f); // 100 × (1 + 0.1×2)
    }

    // ==================== Dirty 重算 ====================

    [Test]
    public void DirtyRecalc_RequiresExplicitSetDirty()
    {
        var attr = new AttributeValue(100f);
        var effect = CreateStackableEffect(5);
        attr.AddModifier(new AttributeModifier(ModifierType.Additive, 10f, effect));
        Assert.AreEqual(110f, attr.GetCurrentValue(), 0.0001f);

        effect.AddStack(); // 外部 StackCount 变化，但未 SetDirty
        Assert.AreEqual(110f, attr.GetCurrentValue(), 0.0001f); // 缓存值不变

        attr.SetDirty();
        Assert.AreEqual(120f, attr.GetCurrentValue(), 0.0001f); // 重算后生效
    }

    [Test]
    public void RemoveModifier_RestoresOriginalValue()
    {
        var attr = new AttributeValue(100f);
        var mod = new AttributeModifier(ModifierType.Additive, 10f);
        attr.AddModifier(mod);
        Assert.AreEqual(110f, attr.GetCurrentValue(), 0.0001f);
        attr.RemoveModifier(mod);
        Assert.AreEqual(100f, attr.GetCurrentValue(), 0.0001f);
    }

    [Test]
    public void GetModifiers_ReturnsAllAddedModifiers()
    {
        var attr = new AttributeValue(100f);
        attr.AddModifier(new AttributeModifier(ModifierType.Additive, 10f));
        attr.AddModifier(new AttributeModifier(ModifierType.Multiplicative, 0.1f));
        Assert.AreEqual(2, attr.GetModifiers().Count);
    }

    // ==================== 钳制（OnPreAttributeChange） ====================

    [Test]
    public void OnPreAttributeChange_ClampsResult()
    {
        var attr = new AttributeValue(100f);
        attr.OnPreAttributeChange = v => Mathf.Clamp(v, 10f, 90f);

        attr.BaseValue = 100f; // 聚合结果 100 → 钳到 90
        Assert.AreEqual(90f, attr.GetCurrentValue(), 0.0001f);

        attr.BaseValue = 0f;   // 聚合结果 0 → 钳到 10
        Assert.AreEqual(10f, attr.GetCurrentValue(), 0.0001f);
    }

    [Test]
    public void OnPreAttributeChange_ClampsToNonNegative()
    {
        var attr = new AttributeValue(100f);
        attr.OnPreAttributeChange = v => Mathf.Max(0f, v);
        attr.BaseValue = -50f;
        Assert.AreEqual(0f, attr.GetCurrentValue(), 0.0001f);
    }

    // ==================== 回调 ====================

    [Test]
    public void OnValueChanged_FiresWhenValueChanges()
    {
        var attr = new AttributeValue(100f);
        float? oldValue = null, newValue = null;
        attr.OnValueChanged = (o, n) => { oldValue = o; newValue = n; };

        attr.AddModifier(new AttributeModifier(ModifierType.Additive, 10f));
        Assert.AreEqual(100f, oldValue.Value, 0.0001f);
        Assert.AreEqual(110f, newValue.Value, 0.0001f);
    }

    [Test]
    public void OnValueChanged_NotFiredWhenValueUnchanged()
    {
        var attr = new AttributeValue(100f);
        int fireCount = 0;
        attr.OnValueChanged = (o, n) => fireCount++;

        attr.AddModifier(new AttributeModifier(ModifierType.Additive, 0f)); // 值不变
        Assert.AreEqual(0, fireCount);
    }

    [Test]
    public void BaseValueSetter_FiresOnValueChanged()
    {
        var attr = new AttributeValue(100f);
        int fireCount = 0;
        attr.OnValueChanged = (o, n) => fireCount++;

        attr.BaseValue = 150f;
        Assert.AreEqual(1, fireCount);
        Assert.AreEqual(150f, attr.GetCurrentValue(), 0.0001f);
    }
}
