using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 敌人头顶血条 — 世界空间跟随敌人移动
/// 挂在敌人身上有 Canvas 的子物体上
/// </summary>
public class EnemyHealthBar : MonoBehaviour
{
    [Header("UI 组件")]
    public Image healthBarFill;
    public GameObject barRoot;

    [Header("偏移")]
    public Vector3 worldOffset = new Vector3(0, 2.2f, 0);

    [Header("显隐")]
    [Tooltip("满血时隐藏，受伤后显示")]
    public bool hideAtFullHealth = true;
    [Tooltip("受伤后显示多久再隐藏")]
    public float showDurationAfterHit = 3f;

    private AttributeSet _attributes;
    private Transform _followTarget;
    private Camera _cam;
    private float _showTimer;

    void Start()
    {
        _attributes = GetComponentInParent<AttributeSet>();
        _followTarget = transform.parent;
        _cam = Camera.main;

        if (barRoot != null && hideAtFullHealth)
            barRoot.SetActive(false);
    }

    void Update()
    {
        if (_attributes == null || _cam == null || _followTarget == null) return;

        // 血条位置跟随
        Vector3 worldPos = _followTarget.position + worldOffset;
        Vector3 screenPos = _cam.WorldToScreenPoint(worldPos);

        if (screenPos.z > 0)
        {
            transform.position = screenPos;
            if (barRoot != null) barRoot.SetActive(true);
        }
        else
        {
            if (barRoot != null) barRoot.SetActive(false);
            return;
        }

        // 更新血量
        float maxHP = _attributes.HealthMax;
        float curHP = _attributes.Health;
        if (maxHP <= 0) maxHP = 1;
        float pct = curHP / maxHP;

        if (healthBarFill != null)
            ApplyBarFill(pct);

        // 显隐逻辑
        if (hideAtFullHealth)
        {
            if (pct >= 1f)
                _showTimer -= Time.deltaTime;
            else
                _showTimer = showDurationAfterHit;

            if (barRoot != null)
                barRoot.SetActive(_showTimer > 0f);
        }

        // 死了隐藏
        if (curHP <= 0 && barRoot != null)
            barRoot.SetActive(false);
    }

    private void ApplyBarFill(float value)
    {
        value = Mathf.Clamp01(value);
        healthBarFill.type = Image.Type.Simple;
        healthBarFill.fillAmount = 1f;

        var rectTransform = healthBarFill.rectTransform;
        rectTransform.anchorMin = new Vector2(0f, rectTransform.anchorMin.y);
        rectTransform.anchorMax = new Vector2(value, rectTransform.anchorMax.y);
        rectTransform.offsetMin = new Vector2(0f, rectTransform.offsetMin.y);
        rectTransform.offsetMax = new Vector2(0f, rectTransform.offsetMax.y);
        rectTransform.localScale = new Vector3(1f, rectTransform.localScale.y, rectTransform.localScale.z);
    }
}
