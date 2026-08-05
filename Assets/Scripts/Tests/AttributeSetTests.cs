using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// AttributeSet EditMode 单元测试。
/// EditMode 下 MonoBehaviour 的 Awake/Start 不会自动执行，
/// 因此通过公开 API RegisterAttribute(...) 初始化属性字典
/// （与 Awake 中 EnsureAttribute + 回调注册逻辑一致），
/// 覆盖 Health 死亡事件、Poise 破防事件、Stamina 消耗与钳制。
/// </summary>
[TestFixture]
public class AttributeSetTests
{
    private readonly List<GameObject> _trackedGameObjects = new List<GameObject>();

    [TearDown]
    public void TearDown()
    {
        foreach (var go in _trackedGameObjects)
            if (go != null) Object.DestroyImmediate(go);
        _trackedGameObjects.Clear();
    }

    /// <summary>
    /// 用公开 API 建立与 Awake 等效的属性字典（含回调 + 非负钳制）。
    /// 避免调用 Awake 中的 CompareTag("Player")（该 Tag 未在 TagManager.asset 注册）。
    /// </summary>
    private AttributeSet CreateAttributeSet(float healthMax = 100f, float poiseMax = 60f, float staminaMax = 100f)
    {
        var go = new GameObject("AttributeSetTestGO");
        _trackedGameObjects.Add(go);
        var set = go.AddComponent<AttributeSet>();

        set.RegisterAttribute(GameplayAttribute.HealthMax, healthMax);
        set.RegisterAttribute(GameplayAttribute.PoiseMax, poiseMax);
        set.RegisterAttribute(GameplayAttribute.StaminaMax, staminaMax);
        set.RegisterAttribute(GameplayAttribute.Health, healthMax);
        set.RegisterAttribute(GameplayAttribute.Poise, poiseMax);
        set.RegisterAttribute(GameplayAttribute.Stamina, staminaMax);
        return set;
    }

    // ==================== Health / OnDeath ====================

    [Test]
    public void HealthModify_TriggersOnDeath()
    {
        var set = CreateAttributeSet();
        bool died = false;
        set.OnDeath += () => died = true;

        set.ModifyHealth(-200f);
        Assert.IsTrue(died);
        Assert.IsTrue(set.IsDead);
        Assert.AreEqual(0f, set.Health, 0.0001f);
    }

    [Test]
    public void HealthModify_ClampsToMax()
    {
        var set = CreateAttributeSet();
        set.ModifyHealth(1000f);
        Assert.AreEqual(100f, set.Health, 0.0001f);
        Assert.IsFalse(set.IsDead);
    }

    [Test]
    public void HealthModify_NeverDropsBelowZero()
    {
        var set = CreateAttributeSet();
        set.ModifyHealth(-150f); // 伤害超过当前生命（100）→ 钳制到 0
        Assert.AreEqual(0f, set.Health, 0.0001f);
    }

    [Test]
    public void DeadAttributeSet_IgnoresFurtherModification()
    {
        var set = CreateAttributeSet();
        set.ModifyHealth(-200f);
        Assert.IsTrue(set.IsDead);

        set.ModifyHealth(1000f); // 死亡后所有修改被忽略
        Assert.AreEqual(0f, set.Health, 0.0001f);
    }

    // ==================== Poise / OnPoiseBreak ====================

    [Test]
    public void PoiseModify_TriggersOnPoiseBreak()
    {
        var set = CreateAttributeSet();
        bool broke = false;
        set.OnPoiseBreak += () => broke = true;

        set.ModifyPoise(-200f);
        Assert.IsTrue(broke);
        Assert.IsTrue(set.IsBroken);
        Assert.AreEqual(0f, set.Poise, 0.0001f);
    }

    [Test]
    public void ResetPoise_RestoresAndFiresRecover()
    {
        var set = CreateAttributeSet();
        set.ModifyPoise(-200f);
        Assert.IsTrue(set.IsBroken);

        bool recovered = false;
        set.OnPoiseRecover += () => recovered = true;
        set.ResetPoise();

        Assert.IsFalse(set.IsBroken);
        Assert.AreEqual(60f, set.Poise, 0.0001f);
        Assert.IsTrue(recovered);
    }

    // ==================== Stamina ====================

    [Test]
    public void TryConsumeStamina_ConsumesWhenEnough()
    {
        var set = CreateAttributeSet();
        Assert.IsTrue(set.TryConsumeStamina(30f));
        Assert.AreEqual(70f, set.Stamina, 0.0001f);
    }

    [Test]
    public void TryConsumeStamina_FailsWhenInsufficient()
    {
        var set = CreateAttributeSet();
        Assert.IsFalse(set.TryConsumeStamina(200f));
        Assert.AreEqual(100f, set.Stamina, 0.0001f);
    }

    [Test]
    public void TryConsumeStamina_NonPositive_ReturnsTrueWithoutChange()
    {
        var set = CreateAttributeSet();
        Assert.IsTrue(set.TryConsumeStamina(0f));
        Assert.IsTrue(set.TryConsumeStamina(-5f));
        Assert.AreEqual(100f, set.Stamina, 0.0001f);
    }

    [Test]
    public void ModifyStamina_ClampsToZero()
    {
        var set = CreateAttributeSet();
        set.ModifyStamina(-150f);
        Assert.AreEqual(0f, set.Stamina, 0.0001f);
    }

    // ==================== 事件与属性访问 ====================

    [Test]
    public void OnAttributeChanged_FiresWithOldAndNewValues()
    {
        var set = CreateAttributeSet();
        GameplayAttribute? changed = null;
        float oldVal = 0f, newVal = 0f;
        set.OnAttributeChanged += (attr, o, n) => { changed = attr; oldVal = o; newVal = n; };

        set.ModifyHealth(-10f);
        Assert.AreEqual(GameplayAttribute.Health, changed);
        Assert.AreEqual(100f, oldVal, 0.0001f);
        Assert.AreEqual(90f, newVal, 0.0001f);
    }

    [Test]
    public void HealthProperty_SetAndGet()
    {
        var set = CreateAttributeSet();
        set.Health = 42f;
        Assert.AreEqual(42f, set.Health, 0.0001f);
    }

    [Test]
    public void GetAttributeValue_ReturnsRegisteredValue()
    {
        var set = CreateAttributeSet();
        Assert.IsNotNull(set.GetAttributeValue(GameplayAttribute.Health));
        Assert.IsTrue(set.HasAttribute(GameplayAttribute.Health));
        Assert.IsFalse(set.HasAttribute(GameplayAttribute.AttackPower));
    }

    [Test]
    public void RegisterAttribute_Existing_ReturnsSameInstance()
    {
        var set = CreateAttributeSet();
        var first = set.RegisterAttribute(GameplayAttribute.Health, 1f);
        var second = set.RegisterAttribute(GameplayAttribute.Health, 2f);
        Assert.AreSame(first, second);
        Assert.AreEqual(100f, set.Health, 0.0001f); // 已存在属性不被覆盖
    }
}
