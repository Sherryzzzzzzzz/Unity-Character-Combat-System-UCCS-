using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// 推力类型
public enum AttackForceType
{
    None,
    Light,       // 不推
    Medium,  // 后退
    Heavy,     // 击飞
    Blow        // 强击退（更大力）
}

[System.Serializable]
public class AttackEvent : TimelineEventBase, ITimelineEventRuntime
{
    public string hitBoxName;

    [Header("基础伤害属性")]
    public float damage = 10f;
    public float poiseDamage = 20f;

    [Header("推力属性")]
    public float hitForce = 100f;     // 推力大小
    public float hitFrame = 5f;       // 哪一帧施加推力
    public Vector3 hitPosition;       // 自定义方向（可空）
    public AttackForceType forceType = AttackForceType.Light;

    // --- 运行时数据 ---
    private Transform _hitBoxTransform;
    private Vector3 _startPosition;
    private Vector3 _endPosition;
    public GameObject hitObject;
    public Vector3 hitPoint;

    public override TimelineEventType Type => TimelineEventType.Attack;
    public override string GetSummary() => $"Attack [{StartFrame}-{EndFrame}] Dmg:{damage} Force:{hitForce}";
    
    public void OnStart(GameObject owner)
    {
        // 让 Player 朝向最近敌人
        var model = owner.GetComponent<PlayerModel>();
        if (model?.nearestEnemy != null)
            owner.transform.LookAt(model.nearestEnemy);

        // 找 HitBox
        _hitBoxTransform = FindDeepChild(owner.transform, hitBoxName);
        if (_hitBoxTransform != null)
        {
            // 启用 collider
            var col = _hitBoxTransform.GetComponent<Collider>();
            if (col != null) col.enabled = true;

            _startPosition = _hitBoxTransform.position;

            // 使用 MeleeWeapon（只做命中检测）
            var weapon = _hitBoxTransform.GetComponent<MeleeWeapon>();
            if (weapon == null)
                weapon = _hitBoxTransform.gameObject.AddComponent<MeleeWeapon>();

            weapon.Initialize(this);
        }
        else
        {
            Debug.Log("AttackEvent: 未找到 HitBox '" + hitBoxName + "' 在 " + owner.name, owner);
        }
    }

    public void OnEnd(GameObject owner)
    {
        if (_hitBoxTransform != null)
        {
            _endPosition = _hitBoxTransform.position;

            var col = _hitBoxTransform.GetComponent<Collider>();
            if (col != null) col.enabled = false;

            var weapon = _hitBoxTransform.GetComponent<MeleeWeapon>();
            if (weapon != null)
                weapon.Deinitialize();
        }
    }

    public Vector3 GetForceDirection()
    {
        if (hitPosition != Vector3.zero)
            return hitPosition.normalized;

        Vector3 dir = (_endPosition - _startPosition);
        if (dir.sqrMagnitude < 0.01f && _hitBoxTransform != null)
            return _hitBoxTransform.root.forward;

        return dir.normalized;
    }
    
    public bool ShouldApplyForce(float currentFrame)
    {
        return Mathf.Abs(currentFrame - hitFrame) < 0.01f;
    }

    public override TimelineEventBase Clone()
    {
        var newEvent = new AttackEvent();
        newEvent.StartFrame = StartFrame;
        newEvent.EndFrame = EndFrame;

        newEvent.damage = damage;
        newEvent.poiseDamage = poiseDamage;

        newEvent.hitForce = hitForce;
        newEvent.hitFrame = hitFrame;
        newEvent.hitPosition = hitPosition;

        newEvent.forceType = forceType;
        newEvent.hitBoxName = hitBoxName;

        return newEvent;
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
