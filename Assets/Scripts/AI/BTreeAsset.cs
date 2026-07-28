using System.Collections.Generic;
using UnityEngine;

/// <summary>行为树资产 — ScriptableObject，存盘用</summary>
[CreateAssetMenu(menuName = "AI/BTree Asset", fileName = "NewBTree")]
public class BTreeAsset : ScriptableObject
{
    [Header("树结构")]
    [SerializeReference]
    public BTNode rootNode;

    [Header("黑板定义")]
    public List<BlackboardEntry> blackboard = new();

    // ================================================================

    /// <summary>快捷创建一棵简单行为树的静态工厂</summary>
    public static BTreeAsset CreateSimpleTree(string name,
        SkillTimelineAsset skill, float moveRadius, float waitAfterAttack)
    {
        var asset = CreateInstance<BTreeAsset>();
        asset.name = name;

        // 黑板
        asset.blackboard = new List<BlackboardEntry>
        {
            new() { key = "player", type = BlackboardType.Transform },
            new() { key = "moveRadius", type = BlackboardType.Float },
            new() { key = "waitTime", type = BlackboardType.Float },
        };

        // Root: Repeater(-1) 无限循环
        var repeater = new BTRepeater { repeatCount = -1 };
        var sequence = new BTSequence();

        // 子节点
        sequence.children = new List<BTNode>
        {
            new BTA_MoveTo { mode = BTA_MoveTo.MoveMode.Circle, radius = moveRadius },
            new BTA_PlaySkill { skillAsset = skill },
            new BTWait { duration = waitAfterAttack },
        };

        repeater.child = sequence;
        asset.rootNode = repeater;

        return asset;
    }

    /// <summary>快捷创建 BOSS 行为树</summary>
    public static BTreeAsset CreateBossTree(string name,
        SkillTimelineAsset normalAttack,
        SkillTimelineAsset heavyAttack,
        SkillTimelineAsset enrageAttack,
        SkillTimelineAsset aoeAttack)
    {
        var asset = CreateInstance<BTreeAsset>();
        asset.name = name;

        asset.blackboard = new List<BlackboardEntry>
        {
            new() { key = "player",       type = BlackboardType.Transform },
            new() { key = "isEnraged",    type = BlackboardType.Bool },
            new() { key = "heavyCD",      type = BlackboardType.Float },
        };

        var root = new BTPrioritySelector();

        // [优先级1] 狂暴状态 → 强化攻击
        var enrageSeq = new BTSequence();
        enrageSeq.children = new List<BTNode>
        {
            new BTCondition { type = BTCondition.ConditionType.BlackboardBool, blackboardKey = "isEnraged", expectedBool = true },
            new BTCondition { type = BTCondition.ConditionType.HPPercentage, hpCompare = BTCondition.CompareMode.LessOrEqual, hpThreshold = 0.4f },
            new BTWait { duration = 0.5f },
            enrageAttack != null ? new BTA_PlaySkill { skillAsset = enrageAttack } : null,
            new BTA_SetBlackboard { key = "isEnraged", boolValue = false },
            new BTWait { duration = 1f },
        };

        // [优先级2] 近战（距离<3m）
        var meleeBranch = new BTCondition { type = BTCondition.ConditionType.Distance, distanceCompare = BTCondition.CompareMode.LessThan, distanceValue = 3f };
        var meleeRandom = new BTRandomSelector();
        meleeRandom.weights = new List<float> { 50f, 25f, 25f };
        meleeRandom.children = new List<BTNode>
        {
            new BTSequence { children = new List<BTNode> { normalAttack != null ? new BTA_PlaySkill { skillAsset = normalAttack } : null, new BTWait { duration = 0.3f } } },
            new BTSequence { children = new List<BTNode> { heavyAttack != null ? new BTA_PlaySkill { skillAsset = heavyAttack } : null, new BTWait { duration = 0.5f } } },
            new BTA_MoveTo { mode = BTA_MoveTo.MoveMode.Strafe, radius = 4f },
        };
        meleeBranch.child = meleeRandom;

        // [优先级3] 中距离（3~10m）→ 突进或远程
        var midBranch = new BTCondition { type = BTCondition.ConditionType.Distance, distanceCompare = BTCondition.CompareMode.GreaterOrEqual, distanceValue = 3f };
        var midSeq = new BTSequence();
        midSeq.children = new List<BTNode>
        {
            new BTA_MoveTo { mode = BTA_MoveTo.MoveMode.Charge, radius = 1f },
            new BTWait { duration = 0.2f },
            aoeAttack != null ? new BTA_PlaySkill { skillAsset = aoeAttack } : null,
        };
        midBranch.child = midSeq;

        // [优先级4] 兜底 → 追击
        var fallback = new BTSequence();
        fallback.children = new List<BTNode>
        {
            new BTA_MoveTo { mode = BTA_MoveTo.MoveMode.Charge, radius = 1f },
            new BTWait { duration = 0.3f },
        };

        root.children = new List<BTNode> { enrageSeq, meleeBranch, midBranch, fallback };
        asset.rootNode = root;

        return asset;
    }
}
