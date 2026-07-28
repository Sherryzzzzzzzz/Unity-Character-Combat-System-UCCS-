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

    private AbilitySystemComponent _ownerASC;

    /// <summary>拼刀宽限期 — 攻击结束后的短时间内仍可触发拼刀</summary>
    private float _clashGraceTime = -1f;
    private const float ClashGraceDuration = 0.35f;

    public void Init(AbilitySystemComponent ownerASC)
    {
        _ownerASC = ownerASC;
    }


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
        _clashGraceTime = -1f;
    }

    public void Deinitialize()
    {
        // ★ 攻击结束后仍保留宽限期用于拼刀
        if (_currentAttackEvent != null)
            _clashGraceTime = Time.time + ClashGraceDuration;
        this._currentAttackEvent = null;
    }

    /// <summary>是否在拼刀有效窗口内（攻击中或宽限期内）</summary>
    private bool IsInClashWindow =>
        _currentAttackEvent != null || Time.time < _clashGraceTime;

    private void OnTriggerEnter(Collider other)
    {
        if (_hasClashedThisSwing) return;

        // ★ 步骤1: 直接武器对武器碰撞 → 拼刀
        if (other.gameObject.layer == WeaponLayer)
        {
            var otherWeapon = other.GetComponent<MeleeWeapon>();
            if (otherWeapon != null && otherWeapon.IsInClashWindow && IsInClashWindow)
            {
                Debug.Log($"[Clash] ✅ 武器碰撞拼刀! {name} vs {other.name}");
                DoClash(otherWeapon._ownerClashable);
            }
            return; // 武器层碰撞不造成伤害
        }

        // 未在攻击窗口 → 不处理任何碰撞
        if (!IsInClashWindow) return;

        // ★ 步骤2: 命中可攻击层 → 先检查目标是否也在攻击中
        if ((hittableLayers.value & (1 << other.gameObject.layer)) != 0)
        {
            if (_collidersHitThisSwing.Contains(other)) return;

            // ★★★ 拼刀优先：目标身上有活跃武器 → 拼刀而非伤害 ★★★
            var targetRoot = other.transform.root;
            var targetWeapons = targetRoot.GetComponentsInChildren<MeleeWeapon>();
            foreach (var tw in targetWeapons)
            {
                if (tw != this && tw.IsInClashWindow)
                {
                    var otherClashable = tw._ownerClashable;
                    if (otherClashable != null && _ownerClashable != null)
                    {
                        Debug.Log($"[Clash] ✅ 双方攻击中, 转为拼刀! (via body) {name}→{other.name}");
                        DoClash(otherClashable);
                        tw._hasClashedThisSwing = true;
                        return;
                    }
                }
            }

            // 普通命中
            var hurtBoxManager = other.GetComponentInParent<HurtBoxManager>();
            if (hurtBoxManager != null)
            {
                _currentAttackEvent.hitObject = other.gameObject;
                _currentAttackEvent.hitPoint = other.ClosestPoint(transform.position);
                hurtBoxManager.ProcessHit(_currentAttackEvent, this.transform.root.gameObject, _ownerASC);

                // 攻击者反馈
                var attackerRoot = this.transform.root;
                var attackerHitStop = attackerRoot.GetComponent<HitStopController>();
                if (attackerHitStop != null && _currentAttackEvent?.attackData != null)
                    attackerHitStop.ApplyAttackerHitStop(_currentAttackEvent.attackData.forceType);

                if (CameraImpactEffects.Instance != null && _currentAttackEvent?.attackData != null)
                    CameraImpactEffects.Instance.ApplyFOVKick(_currentAttackEvent.attackData.forceType);

                _collidersHitThisSwing.Add(other);
            }
        }
    }

    private void DoClash(IClashable otherClashable)
    {
        if (ClashManager.Instance == null)
        {
            Debug.LogError("[Clash] ClashManager.Instance 为空!");
            return;
        }
        _hasClashedThisSwing = true;
        ClashManager.Instance.ResolveClash(_ownerClashable, otherClashable);
    }
}