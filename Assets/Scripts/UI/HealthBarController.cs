using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Base controller for health bars. Bind an AttributeSet to receive updates.
/// This class handles smoothing and damage flash. Concrete subclasses provide UpdateVisuals implementation.
/// </summary>
public abstract class HealthBarController : MonoBehaviour
{
    public HealthBarStyleSO style;

    protected AttributeSet boundAttributes;
    protected float displayedHealth;
    protected Image foregroundImage;
    protected Image backgroundImage;

    private Coroutine flashCoroutine;

    protected virtual void Awake()
    {
        foregroundImage = transform.Find("Foreground")?.GetComponent<Image>();
        backgroundImage = transform.Find("Background")?.GetComponent<Image>();

        if (foregroundImage == null)
        {
            var images = GetComponentsInChildren<Image>(true);
            if (images.Length > 0)
                foregroundImage = images[0];
        }
    }

    protected virtual void Update()
    {
        if (boundAttributes == null) return;

        float maxHealth = boundAttributes.HealthMax;
        float target = boundAttributes.Health;
        float smoothSpeed = style != null ? style.smoothSpeed : 1000f;
        displayedHealth = Mathf.MoveTowards(displayedHealth, target, smoothSpeed * Time.deltaTime);
        UpdateVisuals(maxHealth > 0f ? displayedHealth / maxHealth : 0f);
    }

    public virtual void Bind(AttributeSet attrs)
    {
        if (boundAttributes != null) Unbind();
        boundAttributes = attrs;
        displayedHealth = attrs.Health;
        if (foregroundImage != null)
        {
            foregroundImage.type = Image.Type.Filled;
            foregroundImage.fillMethod = Image.FillMethod.Horizontal;
            foregroundImage.fillOrigin = 0;
        }
        attrs.OnAttributeChanged += OnAttributeChanged;
        UpdateVisuals(attrs.HealthMax > 0f ? displayedHealth / attrs.HealthMax : 0f);
    }

    public virtual void Unbind()
    {
        if (boundAttributes == null) return;
        boundAttributes.OnAttributeChanged -= OnAttributeChanged;
        boundAttributes = null;
    }

    protected virtual void OnDestroy()
    {
        Unbind();
    }

    protected virtual void OnAttributeChanged(GameplayAttribute attr, float oldVal, float newVal)
    {
        if (attr == GameplayAttribute.Health)
        {
            if (newVal < oldVal && style != null)
            {
                if (flashCoroutine != null) StopCoroutine(flashCoroutine);
                flashCoroutine = StartCoroutine(DamageFlash());
            }
        }
    }

    private System.Collections.IEnumerator DamageFlash()
    {
        if (foregroundImage == null || style == null) yield break;
        Color orig = foregroundImage.color;
        foregroundImage.color = style.damageFlashColor;
        yield return new WaitForSeconds(style.flashDuration);
        foregroundImage.color = orig;
        flashCoroutine = null;
    }

    protected abstract void UpdateVisuals(float normalizedHealth);
}
