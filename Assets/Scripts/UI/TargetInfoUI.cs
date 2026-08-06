using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 目标信息 UI：显示锁定目标的名字和血条。
/// 通过 TargetingSystem 获取当前目标。
/// </summary>
public class TargetInfoUI : MonoBehaviour
{
    [Header("UI 引用")]
    public Text targetNameText;
    public Image targetHealthBar;
    public Text targetHealthText;
    public Image targetPoiseBar;
    public Text targetPoiseText;
    public GameObject containerRoot;

    [Header("配置")]
    public float hideDelay = 0.5f;  // 目标丢失后的隐藏延迟

    private TargetingSystem _targetingSystem;
    private AttributeSet _targetAttributes;
    private AbilitySystemComponent _targetASC;
    private Transform _lastTarget;
    private float _hideTimer;

    private void Start()
    {
        var player = PlayerController.Instance != null ? PlayerController.Instance.gameObject : null;
        if (player != null)
        {
            _targetingSystem = player.GetComponent<TargetingSystem>();
        }

        if (containerRoot != null)
            containerRoot.SetActive(false);
    }

    private void Update()
    {
        if (_targetingSystem == null) return;

        var currentTarget = _targetingSystem.HasTarget ? _targetingSystem.CurrentTarget : null;

        if (currentTarget != _lastTarget)
        {
            _lastTarget = currentTarget;

            // 解绑旧目标
            UnbindTarget();

            // 绑定新目标
            if (currentTarget != null)
            {
                BindTarget(currentTarget);
            }
        }

        // 如果无目标且在等待隐藏
        if (currentTarget == null && containerRoot != null && containerRoot.activeSelf)
        {
            _hideTimer += Time.deltaTime;
            if (_hideTimer >= hideDelay)
            {
                containerRoot.SetActive(false);
            }
        }

        // 更新目标血条
        UpdateTargetAttributes();
    }

    private void BindTarget(Transform target)
    {
        _targetAttributes = target.GetComponentInParent<AttributeSet>();
        _targetASC = target.GetComponentInParent<AbilitySystemComponent>();

        if (_targetAttributes != null)
            _targetAttributes.OnAttributeChanged += OnTargetAttributeChanged;

        _hideTimer = 0f;

        if (containerRoot != null)
            containerRoot.SetActive(true);

        // 目标名称
        if (targetNameText != null)
            targetNameText.text = _targetAttributes != null ? _targetAttributes.name : target.name;

        // 初始血条更新
        UpdateTargetAttributes();
    }

    private void UnbindTarget()
    {
        if (_targetAttributes != null)
            _targetAttributes.OnAttributeChanged -= OnTargetAttributeChanged;

        _targetAttributes = null;
        _targetASC = null;
    }

    private void UpdateTargetAttributes()
    {
        if (_targetAttributes == null) return;

        float health = _targetAttributes.Health;
        float maxHealth = _targetAttributes.HealthMax;
        float normalizedHealth = maxHealth > 0f ? Mathf.Clamp01(health / maxHealth) : 0f;

        if (targetHealthBar != null)
            targetHealthBar.fillAmount = normalizedHealth;

        if (targetHealthText != null)
            targetHealthText.text = $"{Mathf.CeilToInt(health)}/{Mathf.CeilToInt(maxHealth)}";

        float poise = _targetAttributes.Poise;
        float maxPoise = _targetAttributes.PoiseMax;
        float normalizedPoise = maxPoise > 0f ? Mathf.Clamp01(poise / maxPoise) : 0f;

        if (targetPoiseBar != null)
            targetPoiseBar.fillAmount = normalizedPoise;

        if (targetPoiseText != null)
            targetPoiseText.text = $"{Mathf.CeilToInt(poise)}/{Mathf.CeilToInt(maxPoise)}";
    }

    private void OnTargetAttributeChanged(GameplayAttribute attr, float oldVal, float newVal)
    {
        if (attr == GameplayAttribute.Health || attr == GameplayAttribute.HealthMax ||
            attr == GameplayAttribute.Poise || attr == GameplayAttribute.PoiseMax)
            UpdateTargetAttributes();
    }

    private void OnDestroy()
    {
        UnbindTarget();
    }
}
