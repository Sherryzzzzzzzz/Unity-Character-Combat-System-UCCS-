using UnityEngine;

public enum BuffStackingType { None, Refresh, Stack } // 不可叠加, 刷新时长, 叠加层数

[CreateAssetMenu(fileName = "Buff_", menuName = "Gameplay/Buff")]
public class BuffSO : ScriptableObject
{
    [Header("核心信息")]
    public string buffName = "New Buff";
    [TextArea] public string description;
    
    [Header("标签")]
    [Tooltip("此 Buff 激活时，授予给角色的 Gameplay Tag")]
    public GameplayTagSO gameplayTag; // 例如 "State.Parrying", "Buff.AttackUp"

    [Header("效果配置")]
    public float duration = 5f; // 持续时间，0代表永久
    // 在这里添加你的 Buff 需要影响的属性
    // public float attackMultiplier = 1.2f; // 攻击力倍率
    // public float defenseAddition = 50f;  // 防御力加成
    // public float healthPerSecond = -5f;  // 每秒生命变化（中毒效果）
    
    [Header("叠加逻辑")]
    public BuffStackingType stackingType = BuffStackingType.Refresh;
    public int maxStacks = 5; // 如果是叠加类型
}

public class Buff
{
    public BuffSO Data { get; } // 对模板数据的引用
    public GameObject Instigator { get; } // 施加者
    public GameObject Target { get; }     // 承受者

    public float TimeRemaining { get; private set; }
    public int CurrentStacks { get; private set; }
    public bool IsFinished => TimeRemaining <= 0;

    public Buff(BuffSO data, GameObject instigator, GameObject target)
    {
        Data = data;
        Instigator = instigator;
        Target = target;
        TimeRemaining = data.duration;
        CurrentStacks = 1;
    }

    public void Tick(float deltaTime)
    {
        if (Data.duration > 0)
        {
            TimeRemaining -= deltaTime;
        }
    }

    public void Refresh()
    {
        TimeRemaining = Data.duration;
    }

    public void AddStack()
    {
        if (CurrentStacks < Data.maxStacks)
        {
            CurrentStacks++;
        }
        Refresh(); // 通常叠加也会刷新时长
    }
}