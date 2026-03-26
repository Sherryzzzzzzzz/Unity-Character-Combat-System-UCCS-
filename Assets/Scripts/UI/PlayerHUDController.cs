using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Simple Player HUD health bar. Bind to player's AttributeSet.
/// Expected hierarchy: Foreground (Image), Background (Image), HealthText (Text)
/// </summary>
public class PlayerHUDController : HealthBarController
{
    public Text healthText;

    protected override void Awake()
    {
        base.Awake();
        if (healthText == null)
            healthText = transform.Find("HealthText")?.GetComponent<Text>();
    }

    protected override void UpdateVisuals(float normalizedHealth)
    {
        if (foregroundImage != null)
            foregroundImage.fillAmount = Mathf.Clamp01(normalizedHealth);
        if (healthText != null && boundAttributes != null)
            healthText.text = $"{Mathf.CeilToInt(boundAttributes.Health)}/{Mathf.CeilToInt(boundAttributes.HealthMax)}";
    }
}
