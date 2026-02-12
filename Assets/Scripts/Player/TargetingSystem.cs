using UnityEngine;
using UnityEngine.UI;
using System.Linq;

public class TargetingSystem : MonoBehaviour
{
    [Header("设置")]
    [SerializeField] private float _maxLockOnDistance = 50f;
    [SerializeField] private LayerMask _targetLayers;

    [Header("UI")]
    [SerializeField] private Image _lockOnIconPrefab;
    [SerializeField] private Canvas _uiCanvas;

    private Image _lockOnIconInstance;
    private Camera _mainCamera;
    
    public Transform CurrentTarget { get; private set; }
    public bool HasTarget => CurrentTarget != null;

    void Awake()
    {
        _mainCamera = Camera.main;
    }
    
    void Start()
    {
        if (_lockOnIconPrefab != null && _uiCanvas != null)
        {
            _lockOnIconInstance = Instantiate(_lockOnIconPrefab, _uiCanvas.transform);
            _lockOnIconInstance.gameObject.SetActive(false);
        }
    }
    
    void LateUpdate()
    {
        if (HasTarget)
        {
            if (CurrentTarget.gameObject.activeInHierarchy && Vector3.Distance(transform.position, CurrentTarget.position) <= _maxLockOnDistance)
            {
                if (_lockOnIconInstance != null)
                {
                    _lockOnIconInstance.gameObject.SetActive(true);
                    _lockOnIconInstance.rectTransform.position = _mainCamera.WorldToScreenPoint(CurrentTarget.position + Vector3.up * 0.7f);
                }
            }
            else
            {
                ClearTarget(); // 如果目标失效，自动清除
            }
        }
    }
    
    public void ToggleLockOn()
    {
        if (HasTarget)
        {
            ClearTarget();
        }
        else
        {
            FindAndSetNearestTarget();
        }
    }
    
    private void FindAndSetNearestTarget()
    {
        Collider[] colliders = Physics.OverlapSphere(transform.position, _maxLockOnDistance, _targetLayers);

        if (colliders.Length == 0)
        {
            Debug.Log("TargetingSystem: No targets found in range.");
            return;
        }

        // 使用 Linq 找到最近的那个碰撞体
        Transform nearestTarget = colliders.OrderBy(col => Vector3.Distance(transform.position, col.transform.position))
                                          .FirstOrDefault()?
                                          .transform;
        
        if (nearestTarget != null)
        {
            SetTarget(nearestTarget);
        }
    }

    private void SetTarget(Transform newTarget)
    {
        CurrentTarget = newTarget;
        Debug.Log($"<color=green>TARGET SET: {CurrentTarget.name}</color>");
    }
    
    private void ClearTarget()
    {
        CurrentTarget = null;
        if (_lockOnIconInstance != null)
        {
            _lockOnIconInstance.gameObject.SetActive(false);
        }
        Debug.Log("<color=yellow>TARGET CLEARED.</color>");
    }
}