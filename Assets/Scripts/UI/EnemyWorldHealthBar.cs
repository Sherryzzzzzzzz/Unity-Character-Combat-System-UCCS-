using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// World-to-screen enemy health bar. Call Bind(AttributeSet, Transform target) to attach to an enemy.
/// Uses Camera.main to compute screen position each frame.
/// </summary>
public class EnemyWorldHealthBar : HealthBarController
{
    public Transform followTarget; // usually the enemy's head or root
    public float yOffset = 2f;
    public float minShowDistance = 2f;
    public float maxShowDistance = 30f;

    private RectTransform rectTransform;
    private Canvas parentCanvas;

    private Camera _cachedCam;
    private CanvasGroup _cachedCG;

    protected override void Awake()
    {
        base.Awake();
        rectTransform = GetComponent<RectTransform>();
        parentCanvas = GetComponentInParent<Canvas>();
        _cachedCam = Camera.main;
        _cachedCG = GetComponent<CanvasGroup>();
    }

    protected override void Update()
    {
        base.Update();
        if (followTarget == null || _cachedCam == null) return;

        Vector3 worldPos = followTarget.position + Vector3.up * yOffset;
        Vector3 screenPos = _cachedCam.WorldToScreenPoint(worldPos);

        bool isInFront = screenPos.z > 0f;
        float distance = Vector3.Distance(_cachedCam.transform.position, followTarget.position);

        gameObject.SetActive(isInFront && distance <= maxShowDistance);

        if (isInFront && parentCanvas != null)
        {
            rectTransform.position = screenPos;
            float alpha = 1f - Mathf.InverseLerp(maxShowDistance, maxShowDistance * 1.5f, distance);
            if (_cachedCG != null) _cachedCG.alpha = alpha;
        }
    }

    public void Bind(AttributeSet attrs, Transform follow)
    {
        Bind(attrs);
        followTarget = follow;
    }

    protected override void UpdateVisuals(float normalizedHealth)
    {
        if (foregroundImage != null)
            foregroundImage.fillAmount = Mathf.Clamp01(normalizedHealth);
    }
}
