using UnityEngine;

public class ClashDetector : MonoBehaviour
{
    private IClashable _ownerClashable;
    private bool _isActive = false;
    private bool _hasClashedThisSwing = false;
    
    private static int WeaponLayer = -1;

    private void Awake()
    {
        _ownerClashable = GetComponentInParent<IClashable>();
        Debug.Log(_ownerClashable);
        // 默认禁用
        GetComponent<Collider>().enabled = false;
        
        if (WeaponLayer == -1)
        {
            WeaponLayer = LayerMask.NameToLayer("Weapon");
        }
    }

    /// <summary>
    /// 由 PlayerAttackComponent 或 EnemySkillComponent 在攻击开始时调用。
    /// </summary>
    public void Activate()
    {
        _isActive = true;
        GetComponent<Collider>().enabled = true;
    }

    /// <summary>
    /// 在攻击结束时调用。
    /// </summary>
    public void Deactivate()
    {
        _isActive = false;
        GetComponent<Collider>().enabled = false;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!_isActive) return;
        if (WeaponLayer == -1) WeaponLayer = LayerMask.NameToLayer("Weapon");
        // --- 核心修改：直接比较 Layer ---
        if (other.gameObject.layer == WeaponLayer)
        {
            
            var otherClashDetector = other.GetComponent<ClashDetector>();
            if (otherClashDetector == null) return; // 对方必须也是一个 ClashDetector
            var otherClashable = other.GetComponentInParent<IClashable>();
            if (otherClashable != null && _ownerClashable != null && otherClashable != _ownerClashable)
            {
                if (otherClashDetector._isActive)
                {
                    
                    ClashManager.Instance.ResolveClash(_ownerClashable, otherClashable);
                    _hasClashedThisSwing = true;
                    otherClashDetector._hasClashedThisSwing = true;
                }
            }
        }
    }
}