using UnityEngine;

/// <summary>
/// Binds an enemy's AttributeSet to a pooled EnemyWorldHealthBar only when the enemy is visible / within distance / not occluded.
///
/// Behavior:
/// - Periodically checks visibility (Camera viewport + distance + optional occlusion raycast).
/// - When visible and within range, obtains a bar from HealthBarPool and binds it.
/// - When not visible (out of range, off-screen, or occluded), returns the bar to the pool.
/// - Returns the bar on disable/destroy.
///
/// Usage:
/// - Add this to enemy prefabs (requires AttributeSet on the same GameObject).
/// - Ensure a HealthBarPool exists in the scene and its prefab points to a valid EnemyWorldHealthBar prefab.
/// </summary>
[DisallowMultipleComponent]
public class EnemyHealthBinder : MonoBehaviour
{
    [Tooltip("Optional transform to follow (e.g., head). If null, will follow this.transform)")]
    public Transform followTransform;

    [Tooltip("How often (seconds) to check visibility")]
    public float checkInterval = 0.2f;

    [Tooltip("Minimum distance to show a world bar")]
    public float minShowDistance = 1f;

    [Tooltip("Maximum distance to show a world bar")]
    public float maxShowDistance = 30f;

    [Tooltip("Layer mask used for occlusion raycasts. Set to everything that can occlude (default: Everything)")]
    public LayerMask occlusionMask = ~0;

    private AttributeSet attrs;
    private HealthBarPool pool;
    private EnemyWorldHealthBar bar;
    private float checkTimer = 0f;
    private Camera mainCam;

    private void Awake()
    {
        attrs = GetComponentInParent<AttributeSet>();
        if (attrs == null)
            attrs = GetComponentInChildren<AttributeSet>();
        if (followTransform == null) followTransform = attrs != null ? attrs.transform : transform;
        pool = FindObjectOfType<HealthBarPool>();
        mainCam = Camera.main;
        if (attrs == null)
            Debug.LogWarning("EnemyHealthBinder: No AttributeSet found on this enemy hierarchy.", this);
        if (pool == null)
            Debug.LogWarning($"EnemyHealthBinder: No HealthBarPool found in scene. Add one under your UI Canvas.", this);
    }

    private void OnEnable()
    {
        // immediate check on enable
        checkTimer = 0f;
    }

    private void OnDisable()
    {
        ReleaseBar();
    }

    private void OnDestroy()
    {
        ReleaseBar();
    }

    private void Update()
    {
        if (pool == null || attrs == null || mainCam == null) return;

        checkTimer -= Time.deltaTime;
        if (checkTimer > 0f) return;
        checkTimer = checkInterval;

        bool visible = IsVisibleToCamera();

        if (visible)
        {
            if (bar == null)
            {
                bar = pool.Get();
                if (bar != null)
                {
                    bar.Bind(attrs, followTransform);
                }
            }
        }
        else
        {
            if (bar != null)
            {
                pool.Return(bar);
                bar = null;
            }
        }
    }

    private bool IsVisibleToCamera()
    {
        if (followTransform == null || mainCam == null) return false;

        Vector3 worldPos = followTransform.position;
        Vector3 screenPos = mainCam.WorldToViewportPoint(worldPos);

        // check in front of camera and within viewport
        if (screenPos.z <= 0f) return false;
        if (screenPos.x < 0f || screenPos.x > 1f || screenPos.y < 0f || screenPos.y > 1f) return false;

        float distance = Vector3.Distance(mainCam.transform.position, worldPos);
        if (distance < minShowDistance || distance > maxShowDistance) return false;

        // occlusion: raycast from camera to target and ensure we don't hit something else first
        Ray ray = new Ray(mainCam.transform.position, worldPos - mainCam.transform.position);
        float rayDist = distance - 0.1f; // small offset to avoid hitting target collider
        if (rayDist <= 0f) return false;

        if (Physics.Raycast(ray, out RaycastHit hit, rayDist, occlusionMask))
        {
            // if the ray hit something before reaching the target, it's occluded
            // allow hit if it belongs to the same root transform as followTransform
            if (!IsPartOfTransform(hit.collider.transform, followTransform))
                return false;
        }

        return true;
    }

    private bool IsPartOfTransform(Transform candidate, Transform targetRoot)
    {
        if (candidate == null || targetRoot == null) return false;
        Transform t = candidate;
        while (t != null)
        {
            if (t == targetRoot) return true;
            t = t.parent;
        }
        return false;
    }

    private void ReleaseBar()
    {
        if (pool != null && bar != null)
        {
            pool.Return(bar);
            bar = null;
        }
    }
}
