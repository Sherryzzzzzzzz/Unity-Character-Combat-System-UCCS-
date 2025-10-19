using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[System.Serializable]
public class AttackEvent : TimelineEventBase, ITimelineEventRuntime
{
    public string hitBoxName;
    public float damage = 10f;

    public override TimelineEventType Type => TimelineEventType.Attack;

    public override string GetSummary()
    {
        return $"Attack [{StartFrame}-{EndFrame}] Damage={damage}, HitBox={hitBoxName}";
    }

    public void OnStart(GameObject owner)
    {
        var hitBox = FindDeepChild(owner.transform, hitBoxName)?.GetComponent<Collider>();
        if (hitBox != null) hitBox.enabled = true;
    }

    public void OnEnd(GameObject owner)
    {
        var hitBox = FindDeepChild(owner.transform, hitBoxName)?.GetComponent<Collider>();
        if (hitBox != null) hitBox.enabled = false;
    }

    private Transform FindDeepChild(Transform parent, string name)
    {
        foreach (Transform child in parent)
        {
            if (child.name == name) return child;
            var result = FindDeepChild(child, name);
            if (result != null) return result;
        }
        return null;
    }
}
