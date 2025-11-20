using UnityEngine;
using System.Collections.Generic;

public class MeleeWeapon : MonoBehaviour
{
    [Header("配置")]
    [Tooltip("设置可以被击中的层")]
    public LayerMask hittableLayers;

    private AttackEvent _currentAttackEvent;
    private List<Collider> _collidersHitThisSwing;

    private void Awake()
    {
        _collidersHitThisSwing = new List<Collider>();
        var col = GetComponent<Collider>();
        if (col != null) col.enabled = false;
    }

    public void Initialize(AttackEvent attackEvent)
    {
        this._currentAttackEvent = attackEvent;
        _collidersHitThisSwing.Clear();
    }

    public void Deinitialize()
    {
        this._currentAttackEvent = null;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_currentAttackEvent == null)
        {
            return;
        }
        if ((hittableLayers.value & (1 << other.gameObject.layer)) == 0) return;
        if (_collidersHitThisSwing.Contains(other)) return;

        var hurtBoxManager = other.GetComponentInParent<HurtBoxManager>();
        if (hurtBoxManager != null)
        {
            // 记录受击对象
            _currentAttackEvent.hitObject = other.gameObject;
            Debug.Log(other.name);

            // 受击点
            _currentAttackEvent.hitPoint = other.ClosestPoint(transform.position);

            hurtBoxManager.ProcessHit(_currentAttackEvent);

            _collidersHitThisSwing.Add(other);
        }
        else
        {
            Debug.Log("HurtBoxManager not found on " + other.name);
        }
    }
}