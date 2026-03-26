using UnityEngine;

[CreateAssetMenu(menuName = "UI/HealthBarStyle")]
public class HealthBarStyleSO : ScriptableObject
{
    public Color foregroundColor = Color.red;
    public Color backgroundColor = new Color(0.2f, 0.0f, 0.0f, 0.8f);
    public Color damageFlashColor = Color.white;
    public float smoothSpeed = 6f;
    public float flashDuration = 0.15f;
    public float fadeDistance = 30f; // world-space distance where world bars begin to fade
}
