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
        // Find expected child images
        foregroundImage = transform.Find("Foreground")?.GetComponent<Image>();
        backgroundImage = transform.Find("Background")?.GetComponent<Image>();
    }

    protected virtual void Update()
    {
        if (boundAttributes == null) return;
        float target = boundAttributes.Health;
        // smooth interpolation
        displayedHealth = Mathf.MoveTowards(displayedHealth, target, style.smoothSpeed * Time.deltaTime);
        UpdateVisuals(displayedHealth / boundAttributes.HealthMax);
    }

    public virtual void Bind(AttributeSet attrs)
    {
        if (boundAttributes != null) Unbind();
        boundAttributes = attrs;
        displayedHealth = attrs.Health;
        attrs.OnAttributeChanged += OnAttributeChanged;
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
            // Trigger damage flash if health decreased
            if (newVal < oldVal)
            {
                if (flashCoroutine != null) StopCoroutine(flashCoroutine);
                flashCoroutine = StartCoroutine(DamageFlash());
            }
        }
    }

    private System.Collections.IEnumerator DamageFlash()
    {
        if (foregroundImage == null) yield break;
        Color orig = foregroundImage.color;
        foregroundImage.color = style.damageFlashColor;
        yield return new WaitForSeconds(style.flashDuration);
        foregroundImage.color = orig;
        flashCoroutine = null;
    }

    protected abstract void UpdateVisuals(float normalizedHealth);
}
