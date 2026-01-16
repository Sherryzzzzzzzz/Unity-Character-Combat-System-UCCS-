// 文件名: TargetingSystem.cs (已修复UI坐标转换)
using UnityEngine;
using UnityEngine.UI; // 【新增】引入UI命名空间
using System.Collections.Generic;
using System.Linq;

public class TargetingSystem : MonoBehaviour
{
    public static TargetingSystem Instance;

    [Header("设置")]
    [SerializeField] private float _maxLockOnDistance = 50f;
    [SerializeField] private LayerMask _targetLayers;
    [SerializeField] private float _fieldOfView = 180f;

    [Header("UI")]
    [Tooltip("Canvas下的锁定图标Image Prefab")]
    [SerializeField] private Image _lockOnIconPrefab; // 【修改】类型改为 Image
    [Tooltip("用于承载UI图标的Canvas")]
    [SerializeField] private Canvas _uiCanvas; // 【新增】需要Canvas的引用

    private Image _lockOnIconInstance; // 【修改】类型改为 Image
    
    public Transform CurrentTarget { get; private set; }
    public bool HasTarget => CurrentTarget != null;
    
    private Camera _mainCamera;
    private List<Transform> _potentialTargets = new List<Transform>();

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        _mainCamera = Camera.main;
    }
    
    void Start()
    {
        // 确保有Prefab和Canvas
        if (_lockOnIconPrefab != null && _uiCanvas != null)
        {
            // 创建图标实例，并将其父对象设置为Canvas
            _lockOnIconInstance = Instantiate(_lockOnIconPrefab, _uiCanvas.transform);
            _lockOnIconInstance.gameObject.SetActive(false);
        }
        else
        {
            Debug.LogWarning("TargetingSystem: Lock-on Icon Prefab or UI Canvas is not assigned.", this);
        }
    }

    void LateUpdate()
    {
        if (HasTarget && _lockOnIconInstance != null)
        {
            // --- 核心修改：坐标转换 ---
            
            // 1. 获取目标在世界空间中的位置 (例如头顶)
            Vector3 worldPosition = CurrentTarget.position + Vector3.up * 1.5f;

            // 2. 将世界坐标转换为屏幕坐标
            Vector3 screenPosition = _mainCamera.WorldToScreenPoint(worldPosition);

            // 3. 判断目标是否在摄像机前方
            if (screenPosition.z > 0)
            {
                // 如果在前方，UI是可见的
                _lockOnIconInstance.gameObject.SetActive(true);
                // 将屏幕坐标直接赋值给UI元素的RectTransform.position
                _lockOnIconInstance.rectTransform.position = screenPosition;
            }
            else
            {
                // 如果目标在摄像机后方，则隐藏图标，防止它镜像出现在屏幕上
                _lockOnIconInstance.gameObject.SetActive(false);
            }

            // ... (自动取消锁定的逻辑保持不变)
            float distanceSqr = (transform.position - CurrentTarget.position).sqrMagnitude;
            if (distanceSqr > (_maxLockOnDistance * 1.2f) * (_maxLockOnDistance * 1.2f) || !CurrentTarget.gameObject.activeInHierarchy)
            {
                ClearTarget();
            }
        }
    }

    public bool ToggleLockOn(Transform playerTransform)
    {
        if (HasTarget)
        {
            ClearTarget();
        }
        else
        {
            FindAndSetTarget(playerTransform);
        }
        return HasTarget;
    }
    
    // FindAndSetTarget 方法保持不变
    private void FindAndSetTarget(Transform playerTransform)
    {
        // ... (代码完全不变)
        _potentialTargets.Clear();
        Collider[] colliders = Physics.OverlapSphere(playerTransform.position, _maxLockOnDistance, _targetLayers);
        foreach (var col in colliders)
        {
            Vector3 directionToTarget = col.transform.position - playerTransform.position;
            float angle = Vector3.Angle(playerTransform.forward, directionToTarget);
            if (angle < _fieldOfView / 2)
            {
                if (!Physics.Linecast(playerTransform.position + Vector3.up, col.transform.position + Vector3.up, out _, ~_targetLayers))
                {
                    _potentialTargets.Add(col.transform);
                }
            }
        }
        if (_potentialTargets.Count == 0) return;
        _potentialTargets = _potentialTargets.OrderBy(t => Vector3.Distance(playerTransform.position, t.position)).ToList();
        SetTarget(_potentialTargets[0]);
    }

    private void SetTarget(Transform newTarget)
    {
        CurrentTarget = newTarget;
        // SetActive的逻辑现在由LateUpdate处理，这里不需要了
    }
    
    private void ClearTarget()
    {
        CurrentTarget = null;
        if (_lockOnIconInstance != null)
        {
            // 隐藏实例
            _lockOnIconInstance.gameObject.SetActive(false);
        }
    }
}