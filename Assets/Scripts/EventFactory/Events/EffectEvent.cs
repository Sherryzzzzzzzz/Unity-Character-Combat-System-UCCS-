using UnityEngine;

[System.Serializable]
public class HitBoxEvent : TimelineEventBase
{
    public GameObject hurtBoxPrefab; // 受击盒预制体或引用
    public bool invincible;          // 是否无敌状态

    public override TimelineEventType Type => TimelineEventType.HitBox;

    public override string GetSummary()
    {
        string hbName = hurtBoxPrefab != null ? hurtBoxPrefab.name : "None";
        return $"HitBox [{StartFrame}-{EndFrame}] hurtBox={hbName}, invincible={invincible}";
    }
}
