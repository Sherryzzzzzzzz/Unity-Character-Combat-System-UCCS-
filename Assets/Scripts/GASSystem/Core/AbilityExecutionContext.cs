using System.Collections.Generic;

/// <summary>
/// 技能执行上下文 — 统一传递施法者/目标/等级等信息
/// </summary>
public class AbilityExecutionContext
{
    /// <summary>
    /// 施法者 ASC
    /// </summary>
    public AbilitySystemComponent Caster { get; set; }

    /// <summary>
    /// 主目标 ASC
    /// </summary>
    public AbilitySystemComponent MainTarget { get; set; }

    /// <summary>
    /// 所有目标 ASC 列表
    /// </summary>
    public List<AbilitySystemComponent> Targets { get; set; } = new List<AbilitySystemComponent>();

    /// <summary>
    /// 目标搜索数据
    /// </summary>
    public TargetData TargetData { get; set; }

    /// <summary>
    /// 技能等级
    /// </summary>
    public int AbilityLevel { get; set; } = 1;

    /// <summary>
    /// 堆叠层数
    /// </summary>
    public int StackCount { get; set; } = 1;

    /// <summary>
    /// 来源技能
    /// </summary>
    public GameplayAbility SourceAbility { get; set; }

    /// <summary>
    /// 自定义数据
    /// </summary>
    public Dictionary<string, object> CustomData { get; set; } = new Dictionary<string, object>();
}
