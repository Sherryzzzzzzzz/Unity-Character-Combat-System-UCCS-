using UnityEngine;

/// <summary>
/// 行为树运行器接口 — 节点只依赖此接口而非具体 BTreeRunner 类。
/// 解耦目的：AI 核心节点（UCCS.BT 程序集）不依赖运行时装配类，
/// 单元测试可用轻量假实现驱动节点逻辑。
/// </summary>
public interface IBTRunner
{
    /// <summary>运行时黑板</summary>
    BTBlackboard Blackboard { get; }

    /// <summary>标签组件（条件节点用）</summary>
    TagComponent Tags { get; }

    /// <summary>运行器所在 Transform（距离/方向计算用）</summary>
    Transform transform { get; }

    /// <summary>获取组件（动作节点获取 EnemyModel/ISkillPlayer/IAttributeProvider 等）</summary>
    T GetComponent<T>();
}
