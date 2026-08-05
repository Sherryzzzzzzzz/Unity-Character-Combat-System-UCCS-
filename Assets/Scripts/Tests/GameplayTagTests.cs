using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// GameplayTagSO（层级标签）+ TagComponent（标签组件）EditMode 单元测试。
/// TagComponent 的引用计数 / 消耗 / 瞬态标签 / 层级匹配均为纯字典逻辑，
/// 不依赖 Update/LateUpdate，可在 EditMode 直接验证。
/// </summary>
[TestFixture]
public class GameplayTagTests
{
    private readonly List<Object> _trackedObjects = new List<Object>();
    private readonly List<GameObject> _trackedGameObjects = new List<GameObject>();

    [TearDown]
    public void TearDown()
    {
        foreach (var obj in _trackedObjects)
            if (obj != null) Object.DestroyImmediate(obj);
        _trackedObjects.Clear();
        foreach (var go in _trackedGameObjects)
            if (go != null) Object.DestroyImmediate(go);
        _trackedGameObjects.Clear();
    }

    private GameplayTagSO CreateTag(string name)
    {
        var tag = ScriptableObject.CreateInstance<GameplayTagSO>();
        tag.name = name;
        _trackedObjects.Add(tag);
        return tag;
    }

    private TagComponent CreateTagComponent()
    {
        var go = new GameObject("TagComponentTestGO");
        _trackedGameObjects.Add(go);
        return go.AddComponent<TagComponent>();
    }

    // ==================== GameplayTagSO：GetFullPath ====================

    [Test]
    public void GetFullPath_TopLevelTag_ReturnsOwnName()
    {
        var parent = CreateTag("Parent");
        Assert.AreEqual("Parent", parent.GetFullPath());
    }

    [Test]
    public void GetFullPath_ReturnsParentChildPath()
    {
        var parent = CreateTag("Parent");
        var child = CreateTag("Child");
        child.parentTag = parent;
        Assert.AreEqual("Parent.Child", child.GetFullPath());
    }

    [Test]
    public void GetFullPath_MultiLevelHierarchy()
    {
        var a = CreateTag("A");
        var b = CreateTag("B");
        var c = CreateTag("C");
        b.parentTag = a;
        c.parentTag = b;
        Assert.AreEqual("A.B.C", c.GetFullPath());
    }

    // ==================== GameplayTagSO：HasChild ====================

    [Test]
    public void HasChild_DirectChild_ReturnsTrue()
    {
        var parent = CreateTag("Parent");
        var child = CreateTag("Child");
        child.parentTag = parent;
        Assert.IsTrue(parent.HasChild(child));
    }

    [Test]
    public void HasChild_IndirectGrandchild_ReturnsTrue()
    {
        var grandparent = CreateTag("Grandparent");
        var parent = CreateTag("Parent");
        var child = CreateTag("Child");
        parent.parentTag = grandparent;
        child.parentTag = parent;
        Assert.IsTrue(grandparent.HasChild(child));
    }

    [Test]
    public void HasChild_Self_ReturnsFalseWithoutCycle()
    {
        var parent = CreateTag("Parent");
        // 实现仅沿 otherTag.parentTag 向上查找 this，
        // 因此在无循环引用时 HasChild(自身) 返回 false。
        Assert.IsFalse(parent.HasChild(parent));
    }

    [Test]
    public void HasChild_Unrelated_ReturnsFalse()
    {
        var a = CreateTag("A");
        var b = CreateTag("B");
        Assert.IsFalse(a.HasChild(b));
    }

    [Test]
    public void HasChild_Null_ReturnsFalse()
    {
        var a = CreateTag("A");
        Assert.IsFalse(a.HasChild(null));
    }

    // ==================== TagComponent：引用计数 ====================

    [Test]
    public void AddTag_Twice_RefCountIsTwo()
    {
        var comp = CreateTagComponent();
        var tag = CreateTag("TagA");
        comp.AddTag(tag);
        comp.AddTag(tag);
        Assert.AreEqual(2, comp.GetTagCount(tag));
        Assert.IsTrue(comp.HasTag(tag));
    }

    [Test]
    public void RemoveTag_Once_RefCountDecrementsToOne()
    {
        var comp = CreateTagComponent();
        var tag = CreateTag("TagA");
        comp.AddTag(tag);
        comp.AddTag(tag);
        comp.RemoveTag(tag);
        Assert.AreEqual(1, comp.GetTagCount(tag));
        Assert.IsTrue(comp.HasTag(tag));
    }

    [Test]
    public void RemoveTag_All_RemovesTagEntirely()
    {
        var comp = CreateTagComponent();
        var tag = CreateTag("TagA");
        comp.AddTag(tag);
        comp.RemoveTag(tag);
        Assert.AreEqual(0, comp.GetTagCount(tag));
        Assert.IsFalse(comp.HasTag(tag));
    }

    [Test]
    public void RemoveTag_WhenAbsent_DoesNotThrow()
    {
        var comp = CreateTagComponent();
        var tag = CreateTag("TagA");
        Assert.DoesNotThrow(() => comp.RemoveTag(tag));
    }

    // ==================== TagComponent：ConsumeTag ====================

    [Test]
    public void ConsumeTag_ConsumesPermanentTag()
    {
        var comp = CreateTagComponent();
        var tag = CreateTag("TagA");
        comp.AddTag(tag);
        Assert.IsTrue(comp.ConsumeTag(tag));
        Assert.IsFalse(comp.HasTag(tag));
        Assert.IsFalse(comp.ConsumeTag(tag)); // 第二次消耗失败
    }

    [Test]
    public void ConsumeTag_DecrementsRefCountOnly()
    {
        var comp = CreateTagComponent();
        var tag = CreateTag("TagA");
        comp.AddTag(tag);
        comp.AddTag(tag);
        Assert.IsTrue(comp.ConsumeTag(tag));
        Assert.AreEqual(1, comp.GetTagCount(tag));
        Assert.IsTrue(comp.HasTag(tag));
    }

    [Test]
    public void ConsumeTag_ConsumesTransientTag()
    {
        var comp = CreateTagComponent();
        var tag = CreateTag("TagA");
        comp.AddTransientTag(tag);
        Assert.IsTrue(comp.HasTag(tag));
        Assert.IsTrue(comp.ConsumeTag(tag));
        Assert.IsFalse(comp.HasTag(tag));
    }

    [Test]
    public void ConsumeTag_WhenAbsent_ReturnsFalse()
    {
        var comp = CreateTagComponent();
        var tag = CreateTag("TagA");
        Assert.IsFalse(comp.ConsumeTag(tag));
    }

    // ==================== TagComponent：瞬态标签 ====================

    [Test]
    public void AddTransientTag_HasTagImmediately()
    {
        var comp = CreateTagComponent();
        var tag = CreateTag("TagA");
        comp.AddTransientTag(tag);
        Assert.IsTrue(comp.HasTag(tag));
        Assert.AreEqual(1, comp.GetTagCount(tag));
    }

    // ==================== TagComponent：层级匹配 ====================

    [Test]
    public void HasTagOrChild_PermanentChildTag_MatchesParent()
    {
        var comp = CreateTagComponent();
        var parent = CreateTag("Parent");
        var child = CreateTag("Child");
        child.parentTag = parent;

        comp.AddTag(child);
        Assert.IsTrue(comp.HasTagOrChild(parent)); // 拥有子标签 → 父标签匹配
        Assert.IsTrue(comp.HasTagOrChild(child));  // 自身也匹配
    }

    [Test]
    public void HasTagOrChild_TransientChildTag_MatchesParent()
    {
        var comp = CreateTagComponent();
        var parent = CreateTag("Parent");
        var child = CreateTag("Child");
        child.parentTag = parent;

        comp.AddTransientTag(child);
        Assert.IsTrue(comp.HasTagOrChild(parent));
    }

    [Test]
    public void HasTagOrChild_UnrelatedTag_ReturnsFalse()
    {
        var comp = CreateTagComponent();
        var owned = CreateTag("Owned");
        var unrelated = CreateTag("Unrelated");
        comp.AddTag(owned);
        Assert.IsFalse(comp.HasTagOrChild(unrelated));
    }

    // ==================== TagComponent：回调与空安全 ====================

    [Test]
    public void OnTagAdded_FiresOnlyOnFirstAdd()
    {
        var comp = CreateTagComponent();
        var tag = CreateTag("TagA");
        int fireCount = 0;
        comp.OnTagAdded += t => fireCount++;

        comp.AddTag(tag);
        comp.AddTag(tag); // 引用计数 +1，不重复触发
        Assert.AreEqual(1, fireCount);
    }

    [Test]
    public void NullTag_IsIgnoredSafely()
    {
        var comp = CreateTagComponent();
        Assert.IsFalse(comp.HasTag(null));
        Assert.AreEqual(0, comp.GetTagCount(null));
        Assert.IsFalse(comp.ConsumeTag(null));
        Assert.IsFalse(comp.HasTagOrChild(null));
        Assert.DoesNotThrow(() => comp.AddTag(null));
        Assert.DoesNotThrow(() => comp.RemoveTag(null));
        Assert.DoesNotThrow(() => comp.AddTransientTag(null));
    }
}
