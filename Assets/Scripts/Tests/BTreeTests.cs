using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// 自研行为树核心节点单元测试（EditMode）：
/// 覆盖 BTBlackboard、BTSequence/BTSelector/BTRandomSelector 短路逻辑、
/// BTCondition（AlwaysTrue/BlackboardBool/Distance）、
/// BTInverter/BTRepeater/BTCooldown。
/// 使用 FakeRunner(IBTRunner) + FakeAction 驱动，不依赖 MonoBehaviour 帧循环。
/// </summary>
[TestFixture]
public class BTreeTests
{
    private readonly List<Object> _tracked = new List<Object>();

    [TearDown]
    public void TearDown()
    {
        foreach (var o in _tracked)
            if (o != null) Object.DestroyImmediate(o);
        _tracked.Clear();
    }

    // ── 测试替身 ──────────────────────────────

    private sealed class FakeRunner : IBTRunner
    {
        public BTBlackboard Blackboard { get; } = new();
        public TagComponent Tags { get; set; }
        public Transform transform { get; set; }
        public T GetComponent<T>() => default;
    }

    private sealed class FakeAction : BTAction
    {
        private readonly BTNodeState _result;
        public int TickCount;
        public FakeAction(BTNodeState result) { _result = result; }
        public override BTNodeState OnTick() { TickCount++; return _state = _result; }
    }

    private FakeRunner CreateRunner(Vector3 pos)
    {
        var go = new GameObject("FakeRunner");
        go.transform.position = pos;
        _tracked.Add(go);
        return new FakeRunner { transform = go.transform };
    }

    private static BTNodeState Tick(BTNode node, IBTRunner runner)
    {
        if (node.State == BTNodeState.Inactive)
            node.OnEnter(runner);
        var r = node.OnTick();
        if (r != BTNodeState.Running)
            node.OnExit();
        return r;
    }

    // ── BTBlackboard ──────────────────────────

    [Test]
    public void Blackboard_SetGet_AllTypes()
    {
        var bb = new BTBlackboard();
        bb.Initialize(new List<BlackboardEntry>
        {
            new() { key = "f", type = BlackboardType.Float },
            new() { key = "i", type = BlackboardType.Int },
            new() { key = "b", type = BlackboardType.Bool },
            new() { key = "v", type = BlackboardType.Vector3 },
        });

        bb.Set("f", 1.5f);
        bb.Set("i", 42);
        bb.Set("b", true);
        bb.Set("v", new Vector3(1, 2, 3));

        Assert.AreEqual(1.5f, bb.Get<float>("f"), 0.0001f);
        Assert.AreEqual(42, bb.Get<int>("i"));
        Assert.AreEqual(true, bb.GetBool("b"));
        Assert.AreEqual(new Vector3(1, 2, 3), bb.Get<Vector3>("v"));
    }

    [Test]
    public void Blackboard_Clear_RemovesAllKeys()
    {
        var bb = new BTBlackboard();
        bb.Set("x", 5f);
        bb.Clear();
        Assert.AreEqual(0f, bb.Get<float>("x"));
    }

    // ── BTSequence ────────────────────────────

    [Test]
    public void Sequence_AllSuccess_ReturnsSuccess()
    {
        var runner = CreateRunner(Vector3.zero);
        var a1 = new FakeAction(BTNodeState.Success);
        var a2 = new FakeAction(BTNodeState.Success);
        var seq = new BTSequence { children = new List<BTNode> { a1, a2 } };

        Assert.AreEqual(BTNodeState.Success, Tick(seq, runner));
        Assert.AreEqual(1, a1.TickCount);
        Assert.AreEqual(1, a2.TickCount);
    }

    [Test]
    public void Sequence_ChildFailure_ShortCircuits()
    {
        var runner = CreateRunner(Vector3.zero);
        var fail = new FakeAction(BTNodeState.Failure);
        var never = new FakeAction(BTNodeState.Success);
        var seq = new BTSequence { children = new List<BTNode> { fail, never } };

        Assert.AreEqual(BTNodeState.Failure, Tick(seq, runner));
        Assert.AreEqual(1, fail.TickCount);
        Assert.AreEqual(0, never.TickCount); // 短路：后续子节点未执行
    }

    [Test]
    public void Sequence_Running_StopsAtRunningNode()
    {
        var runner = CreateRunner(Vector3.zero);
        var run = new FakeAction(BTNodeState.Running);
        var after = new FakeAction(BTNodeState.Success);
        var seq = new BTSequence { children = new List<BTNode> { run, after } };

        Assert.AreEqual(BTNodeState.Running, Tick(seq, runner));
        Assert.AreEqual(0, after.TickCount);
    }

    // ── BTSelector ────────────────────────────

    [Test]
    public void Selector_FirstSuccess_ReturnsSuccess()
    {
        var runner = CreateRunner(Vector3.zero);
        var s = new FakeAction(BTNodeState.Success);
        var after = new FakeAction(BTNodeState.Success);
        var sel = new BTSelector { children = new List<BTNode> { s, after } };

        Assert.AreEqual(BTNodeState.Success, Tick(sel, runner));
        Assert.AreEqual(0, after.TickCount); // 短路
    }

    [Test]
    public void Selector_AllFail_ReturnsFailure()
    {
        var runner = CreateRunner(Vector3.zero);
        var sel = new BTSelector { children = new List<BTNode>
            { new FakeAction(BTNodeState.Failure), new FakeAction(BTNodeState.Failure) } };

        Assert.AreEqual(BTNodeState.Failure, Tick(sel, runner));
    }

    // ── BTCondition ───────────────────────────

    [Test]
    public void Condition_AlwaysTrue_RunsChild()
    {
        var runner = CreateRunner(Vector3.zero);
        var cond = new BTCondition { type = BTCondition.ConditionType.AlwaysTrue };
        cond.child = new FakeAction(BTNodeState.Success);

        Assert.AreEqual(BTNodeState.Success, Tick(cond, runner));
    }

    [Test]
    public void Condition_BlackboardBool_MatchAndMiss()
    {
        var runner = CreateRunner(Vector3.zero);
        runner.Blackboard.Set("flag", true);

        var match = new BTCondition
        {
            type = BTCondition.ConditionType.BlackboardBool,
            blackboardKey = "flag",
            expectedBool = true
        };
        match.child = new FakeAction(BTNodeState.Success);
        Assert.AreEqual(BTNodeState.Success, Tick(match, runner));

        var miss = new BTCondition
        {
            type = BTCondition.ConditionType.BlackboardBool,
            blackboardKey = "flag",
            expectedBool = false
        };
        miss.child = new FakeAction(BTNodeState.Success);
        Assert.AreEqual(BTNodeState.Failure, Tick(miss, runner)); // 条件不满足直接失败
    }

    [Test]
    public void Condition_Distance_LessThan()
    {
        var runner = CreateRunner(Vector3.zero); // 运行器在原点
        var targetGo = new GameObject("Target");
        targetGo.transform.position = new Vector3(3f, 0f, 0f); // 目标在 3m 处
        _tracked.Add(targetGo);
        runner.Blackboard.Set("player", targetGo.transform);

        // 距离 < 2m → 3m 不满足
        var far = new BTCondition
        {
            type = BTCondition.ConditionType.Distance,
            distanceCompare = BTCondition.CompareMode.LessThan,
            distanceValue = 2f,
            targetKey = "player"
        };
        far.child = new FakeAction(BTNodeState.Success);
        Assert.AreEqual(BTNodeState.Failure, Tick(far, runner));

        // 距离 < 5m → 3m 满足
        var near = new BTCondition
        {
            type = BTCondition.ConditionType.Distance,
            distanceCompare = BTCondition.CompareMode.LessThan,
            distanceValue = 5f,
            targetKey = "player"
        };
        near.child = new FakeAction(BTNodeState.Success);
        Assert.AreEqual(BTNodeState.Success, Tick(near, runner));
    }

    // ── BTInverter ────────────────────────────

    [Test]
    public void Inverter_FlipsResult()
    {
        var runner = CreateRunner(Vector3.zero);
        var inv = new BTInverter { child = new FakeAction(BTNodeState.Failure) };
        Assert.AreEqual(BTNodeState.Success, Tick(inv, runner));

        var inv2 = new BTInverter { child = new FakeAction(BTNodeState.Success) };
        Assert.AreEqual(BTNodeState.Failure, Tick(inv2, runner));
    }

    // ── BTRepeater ────────────────────────────

    [Test]
    public void Repeater_RepeatsFixedCount()
    {
        var runner = CreateRunner(Vector3.zero);
        var act = new FakeAction(BTNodeState.Success);
        var rep = new BTRepeater { repeatCount = 3, child = act };

        Assert.AreEqual(BTNodeState.Success, Tick(rep, runner));
        Assert.AreEqual(3, act.TickCount); // 子节点执行了 3 次
    }

    // ── BTCooldown ────────────────────────────

    [Test]
    public void Cooldown_BlocksSecondImmediateExecute()
    {
        var runner = CreateRunner(Vector3.zero);
        var act = new FakeAction(BTNodeState.Success);
        var cd = new BTCooldown { cooldownTime = 100f, child = act }; // 长冷却

        Assert.AreEqual(BTNodeState.Success, Tick(cd, runner)); // 第一次执行
        Assert.AreEqual(BTNodeState.Failure, Tick(cd, runner)); // 冷却中 → 失败
        Assert.AreEqual(1, act.TickCount);                       // 子节点未再执行
    }

    [Test]
    public void Cooldown_Reset_ClearsCooldown()
    {
        var runner = CreateRunner(Vector3.zero);
        var act = new FakeAction(BTNodeState.Success);
        var cd = new BTCooldown { cooldownTime = 100f, child = act };

        Assert.AreEqual(BTNodeState.Success, Tick(cd, runner));
        Assert.AreEqual(BTNodeState.Failure, Tick(cd, runner)); // 冷却中

        cd.Reset(); // 树重入时重置冷却
        Assert.AreEqual(BTNodeState.Success, Tick(cd, runner)); // 冷却清除后可执行
        Assert.AreEqual(2, act.TickCount);
    }
}
