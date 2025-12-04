using UnityEngine;
using System.Collections.Generic;

public class MeleeWeapon : MonoBehaviour
{
    [Header("配置")]
    [Tooltip("设置可以被击中的层 (你的伤害系统依赖这个)")]
    public LayerMask hittableLayers;

    // --- 静态 Layer 缓存 (仅用于拼刀) ---
    private static int WeaponLayer = -1;

    // --- 运行时数据 ---
    private AttackEvent _currentAttackEvent;
    private IClashable _ownerClashable;
    private List<Collider> _collidersHitThisSwing;
    private bool _hasClashedThisSwing = false;

    private void Awake()
    {
        _collidersHitThisSwing = new List<Collider>();
        _ownerClashable = GetComponentInParent<IClashable>();
        
        var col = GetComponent<Collider>();
        if (col != null) col.enabled = false;
        
        // 初始化 Weapon Layer，并确保自身在正确的 Layer 上
        if (WeaponLayer == -1)
        {
            WeaponLayer = LayerMask.NameToLayer("Weapon");
        }
        this.gameObject.layer = WeaponLayer;
    }

    public void Initialize(AttackEvent attackEvent)
    {
        this._currentAttackEvent = attackEvent;
        _collidersHitThisSwing.Clear();
        _hasClashedThisSwing = false;
    }

    public void Deinitialize()
    {
        this._currentAttackEvent = null;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_currentAttackEvent == null) return;
        
        // *** 核心修改 1: 在所有逻辑之前，优先进行拼刀检测 ***
        // 如果本次挥击已经拼过刀，则后续所有检测（包括伤害）都跳过
        if (_hasClashedThisSwing) return;

        // 检查对方是否也是一个 Weapon
        if (other.gameObject.layer == WeaponLayer)
        {
            var otherWeapon = other.GetComponent<MeleeWeapon>();
            // 确保对方武器也处于激活的攻击事件中，并且不是自己人
            if (otherWeapon != null && otherWeapon._currentAttackEvent != null && otherWeapon._ownerClashable != this._ownerClashable)
            {
                // 找到了！调用裁判
                ClashManager.Instance.ResolveClash(_ownerClashable, otherWeapon._ownerClashable);
                
                // 标记双方都已拼刀
                _hasClashedThisSwing = true;
                otherWeapon._hasClashedThisSwing = true;
                
                // 拼刀后，立即结束本轮检测，不执行后续的伤害逻辑
                return;
            }
        }
        
        if ((hittableLayers.value & (1 << other.gameObject.layer)) == 0) return;
        
        if (_collidersHitThisSwing.Contains(other)) return;

        var hurtBoxManager = other.GetComponentInParent<HurtBoxManager>();
        if (hurtBoxManager != null)
        {
            _currentAttackEvent.hitObject = other.gameObject;
            _currentAttackEvent.hitPoint = other.ClosestPoint(transform.position);
            hurtBoxManager.ProcessHit(_currentAttackEvent, this.transform.root.gameObject);

            _collidersHitThisSwing.Add(other);
        }
        else
        {
            Debug.Log("HurtBoxManager not found on " + other.name);
        }
    }
}