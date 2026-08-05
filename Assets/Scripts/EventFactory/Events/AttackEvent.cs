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

public enum AttackShape
{
    WeaponCollider,
    Sphere,
    Capsule,
    Cone
}


[System.Serializable]
public class AttackEvent : TimelineEventBase, ITimelineEventRuntime
{
    public string hitBoxName;

    [Header("Attack Data Asset")]
    public AttackData attackData;

    [Header("Target System 模式")]
    [Tooltip("启用后使用 SearchParameters/TargetData 替代直接 Physics 调用")]
    public bool useTargetSystem = false;

    // --- 运行时数据 ---
    private Transform _hitBoxTransform;
    private Vector3 _startPosition;
    private Vector3 _endPosition;
    
    public bool useLocalOffset;
    public Vector3 localOffset;
    public Vector3 localForward = Vector3.forward;

    public GameObject hitObject;

    /// <summary>本次挥击是否已触发拼刀（防止一次挥击多次拼刀/拼刀后仍伤害）</summary>
    private bool _clashResolvedThisSwing = false;
    public Vector3 hitPoint;
    public GameObject attackerRoot; // ★ P3 运行时：攻击者根节点，供受击方计算稳定击退方向

    public override TimelineEventType Type => TimelineEventType.Attack;

    public override string GetSummary()
    {
        string effectName = attackData != null && attackData.effect != null
            ? attackData.effect.name
            : "None";

        return $"Attack [{StartFrame}-{EndFrame}] Effect:{effectName}";
    }

    public void OnStart(GameObject owner)
    {
        _clashResolvedThisSwing = false;

        var model = owner.GetComponent<PlayerModel>();
        if (model?.nearestEnemy != null)
            owner.transform.LookAt(model.nearestEnemy);

        _hitBoxTransform = FindDeepChild(owner.transform, hitBoxName);

        if (_hitBoxTransform != null)
        {
            var col = _hitBoxTransform.GetComponent<Collider>();
            if (col != null) col.enabled = true;

            _startPosition = _hitBoxTransform.position;

            var weapon = _hitBoxTransform.GetComponent<MeleeWeapon>();
            if (weapon == null)
                weapon = _hitBoxTransform.gameObject.AddComponent<MeleeWeapon>();

            weapon.Initialize(this);

            var ownerASC = owner.GetComponent<AbilitySystemComponent>();
            if (ownerASC != null)
                weapon.Init(ownerASC);

            // ★ 激活剑气拖尾
            var slashTrail = _hitBoxTransform.GetComponent<SlashTrailEffect>();
            if (slashTrail == null)
                slashTrail = _hitBoxTransform.gameObject.AddComponent<SlashTrailEffect>();
            if (attackData != null)
                slashTrail.Activate(attackData.forceType);
        }
        else
        {
            Debug.LogWarning($"AttackEvent: 未找到 HitBox '{hitBoxName}'");
        }

        // 判定说明：当前场景受击盒位于 Default 层，MeleeWeapon 触发路径的 hittableLayers(层6/层3)
        // 不会命中它们——伤害实际由下方形状 Overlap 判定承担（攻击方自身/无ASC对象会被过滤）。
        // 因此这里必须无条件执行 ExecuteAttack，不能按“存在武器碰撞体”门控。
        ExecuteAttack(owner);

        // 设置运行时 Gizmos 可视化
        if (attackData != null && attackData.shape != AttackShape.WeaponCollider)
        {
            var debugger = owner.GetComponent<AttackShapeDebugger>();
            if (debugger == null)
                debugger = owner.AddComponent<AttackShapeDebugger>();

            GetAttackBasis(owner, out var debugCenter, out var debugForward);
            switch (attackData.shape)
            {
                case AttackShape.Sphere:
                    debugger.SetSphere(debugCenter, attackData.radius);
                    break;
                case AttackShape.Capsule:
                    debugger.SetCapsule(debugCenter, debugForward, attackData.radius, attackData.length);
                    break;
                case AttackShape.Cone:
                    debugger.SetCone(debugCenter, debugForward, attackData.length, attackData.angle);
                    break;
            }
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

            // ★ 停用剑气拖尾
            var slashTrail = _hitBoxTransform.GetComponent<SlashTrailEffect>();
            if (slashTrail != null)
                slashTrail.Deactivate();
        }

        // 清除运行时 Gizmos 可视化
        var debugger = owner.GetComponent<AttackShapeDebugger>();
        if (debugger != null)
            debugger.Clear();
    }

    public Vector3 GetForceDirection()
    {
        if (attackData != null && attackData.hitPosition != Vector3.zero)
            return attackData.hitPosition.normalized;

        Vector3 dir = (_endPosition - _startPosition);
        if (dir.sqrMagnitude < 0.01f && _hitBoxTransform != null)
            return _hitBoxTransform.root.forward;

        return dir.normalized;
    }

    public bool ShouldApplyForce(float currentFrame)
    {
        if (attackData == null) return false;
        return Mathf.Abs(currentFrame - attackData.hitFrame) < 0.01f;
    }

    public override TimelineEventBase Clone()
    {
        var newEvent = new AttackEvent();
        newEvent.StartFrame = StartFrame;
        newEvent.EndFrame = EndFrame;
        newEvent.hitBoxName = hitBoxName;
        newEvent.attackData = attackData;
        newEvent.useLocalOffset = useLocalOffset;
        newEvent.localOffset = localOffset;
        newEvent.localForward = localForward;
        newEvent.useTargetSystem = useTargetSystem;

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
    
    private void ExecuteAttack(GameObject owner)
    {
        if (attackData == null || attackData.effect == null)
            return;

        var ownerASC = owner.GetComponent<AbilitySystemComponent>();
        if (ownerASC == null)
            return;

        GetAttackBasis(owner, out var center, out var forward);

        if (useTargetSystem)
        {
            ExecuteWithTargetSystem(ownerASC, center, forward);
            return;
        }

        switch (attackData.shape)
        {
            case AttackShape.Sphere:
                ExecuteSphere(ownerASC, center, attackData.radius);
                break;

            case AttackShape.Capsule:
                ExecuteCapsule(ownerASC, center, forward);
                break;

            case AttackShape.Cone:
                ExecuteCone(ownerASC, center, forward);
                break;
        }
    }

    private void ExecuteWithTargetSystem(AbilitySystemComponent ownerASC, Vector3 center, Vector3 forward)
    {
        var sp = new SearchParameters { TargetLayer = attackData.hitLayerMask, ExcludeSelf = true };

        switch (attackData.shape)
        {
            case AttackShape.Sphere:
                sp.Shape = SearchShape.Circle;
                sp.Radius = attackData.radius;
                break;
            case AttackShape.Capsule:
                sp.Shape = SearchShape.Rectangle;
                sp.Length = attackData.length;
                sp.Width = attackData.radius * 2f;
                break;
            case AttackShape.Cone:
                sp.Shape = SearchShape.Sector;
                sp.Radius = attackData.length;
                sp.Angle = attackData.angle;
                break;
            default:
                return;
        }

        var data = new TargetData { Origin = center, Direction = forward, Range = sp.Radius };
        Collider[] hits = null;

        switch (sp.Shape)
        {
            case SearchShape.Circle:
                hits = Physics.OverlapSphere(center, sp.Radius, sp.TargetLayer);
                break;
            case SearchShape.Sector:
                hits = Physics.OverlapSphere(center, sp.Radius, sp.TargetLayer);
                break;
            case SearchShape.Rectangle:
                var boxCenter = center + forward * (sp.Length / 2f);
                var boxSize = new Vector3(sp.Width, 2f, sp.Length);
                hits = Physics.OverlapBox(boxCenter, boxSize / 2f, Quaternion.LookRotation(forward), sp.TargetLayer);
                break;
        }

        if (hits == null) return;

        foreach (var col in hits)
        {
            var targetASC = col.GetComponentInParent<AbilitySystemComponent>();
            if (targetASC == null || targetASC == ownerASC) continue;

            if (sp.Shape == SearchShape.Sector)
            {
                Vector3 dir = (col.transform.position - center).normalized;
                if (Vector3.Angle(forward, dir) > sp.Angle * 0.5f) continue;
            }

            data.TargetActors.Add(targetASC);
        }

        // 通过 TargetData 应用效果
        ownerASC.ApplyEffectToTargets(attackData.effect, data);
    }


    /// <summary>
    /// ★ 拼刀判定（时间窗口制，鬼泣式）：攻击形状命中目标时，若目标也在攻击中 → 转拼刀而非伤害。
    /// 相比武器物理碰撞路径（ClashDetector），此判定覆盖双方攻击动画重叠的整个窗口，触发率大幅提升。
    /// 闪避/格挡状态无 AttackEvent（GetClashLevel 返回 0），不会误判。
    /// </summary>
    private bool TryResolveClash(GameObject owner, Collider col)
    {
        if (_clashResolvedThisSwing) return true; // 本次挥击已拼刀，跳过伤害
        if (ClashManager.Instance == null) return false;

        var ownerClashable = owner.GetComponent<IClashable>();
        var targetClashable = col.GetComponentInParent<IClashable>();
        if (ownerClashable == null || targetClashable == null || targetClashable == ownerClashable)
            return false;

        // 目标正在攻击中（攻击技能中返回 forceType>0；闪避/格挡无 AttackEvent 返回 0）
        if (targetClashable.GetClashLevel() <= 0)
            return false;

        _clashResolvedThisSwing = true;
        ClashManager.Instance.ResolveClash(ownerClashable, targetClashable);
        return true;
    }

    private void ExecuteSphere(AbilitySystemComponent ownerASC, Vector3 center, float radius)
    {
        var hits = Physics.OverlapSphere(center, radius, attackData.hitLayerMask);

        foreach (var col in hits)
        {
            var targetASC = col.GetComponentInParent<AbilitySystemComponent>();
            if (targetASC == null || targetASC == ownerASC)
                continue;

            // ★ 拼刀优先：目标在攻击中 → 转拼刀而非伤害
            if (TryResolveClash(ownerASC.gameObject, col)) continue;

            // 路由到 HurtBoxManager.ProcessHit() — 走格挡/弹反/伤害完整管道
            this.attackerRoot = ownerASC.gameObject;
            var hbm = col.GetComponentInParent<HurtBoxManager>();
            if (hbm != null)
                hbm.ProcessHit(this, ownerASC.gameObject, ownerASC);
        }
    }

    private void ExecuteCone(AbilitySystemComponent ownerASC, Vector3 center, Vector3 forward)
    {
        var hits = Physics.OverlapSphere(center, attackData.length, attackData.hitLayerMask);

        foreach (var col in hits)
        {
            var targetASC = col.GetComponentInParent<AbilitySystemComponent>();
            if (targetASC == null || targetASC == ownerASC)
                continue;

            Vector3 dir = (col.transform.position - center).normalized;
            float angle = Vector3.Angle(forward, dir);

            if (angle <= attackData.angle * 0.5f)
            {
                // ★ 拼刀优先：目标在攻击中 → 转拼刀而非伤害
                if (TryResolveClash(ownerASC.gameObject, col)) continue;

                this.attackerRoot = ownerASC.gameObject;
                var hbm = col.GetComponentInParent<HurtBoxManager>();
                if (hbm != null)
                    hbm.ProcessHit(this, ownerASC.gameObject, ownerASC);
            }
        }
    }

    private void ExecuteCapsule(
        AbilitySystemComponent ownerASC,
        Vector3 center,
        Vector3 forward)
    {
        float length = attackData.length;
        float radius = attackData.radius;

        // 胶囊起点终点
        Vector3 point1 = center;
        Vector3 point2 = center + forward * length;

        var hits = Physics.OverlapCapsule(
            point1,
            point2,
            radius,
            attackData.hitLayerMask
        );

        foreach (var col in hits)
        {
            var targetASC = col.GetComponentInParent<AbilitySystemComponent>();
            if (targetASC == null || targetASC == ownerASC)
                continue;

            // ★ 拼刀优先：目标在攻击中 → 转拼刀而非伤害
            if (TryResolveClash(ownerASC.gameObject, col)) continue;

            // ★ 形状判定路径：写入攻击者根节点，供受击方计算稳定击退方向
            this.attackerRoot = ownerASC.gameObject;
            var hbm = col.GetComponentInParent<HurtBoxManager>();
            if (hbm != null)
                hbm.ProcessHit(this, ownerASC.gameObject, ownerASC);
        }
    }

    public void GetAttackBasis(
        GameObject owner,
        out Vector3 center,
        out Vector3 forward)
    {
        if (owner == null)
        {
            center = Vector3.zero;
            forward = Vector3.forward;
            return;
        }

        Transform t = owner.transform;

        if (useLocalOffset)
        {
            // 局部偏移转世界空间
            center = t.position + t.rotation * localOffset;
            forward = t.rotation * localForward;
        }
        else
        {
            center = t.position;
            forward = t.forward;
        }

        if (forward.sqrMagnitude < 0.0001f)
            forward = Vector3.forward;

        forward.Normalize();
    }



}

