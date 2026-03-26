using UnityEngine;

/// <summary>
/// 浮动文字 Cue — 显示伤害/治疗数字
/// </summary>
public class FloatingTextCue : MonoBehaviour, IGameplayCue
{
    [Tooltip("浮动文字预制体（需包含 TextMesh 或 TextMeshPro 组件）")]
    public GameObject floatingTextPrefab;

    [Tooltip("文字偏移")]
    public Vector3 offset = new Vector3(0, 1.5f, 0);

    [Tooltip("自动销毁延迟")]
    public float destroyDelay = 1.5f;

    [Tooltip("文字颜色")]
    public Color textColor = Color.red;

    public void OnExecute(GameObject target, GameplayEffectSpec spec)
    {
        if (floatingTextPrefab == null) return;

        var instance = Instantiate(floatingTextPrefab, target.transform.position + offset, Quaternion.identity);

        // 尝试设置文字内容
        float damage = 0f;
        if (spec != null && spec.EffectData != null)
        {
            damage = spec.EffectData.damage * spec.EffectData.damageMultiplier;
        }

        // 尝试 TextMesh
        var textMesh = instance.GetComponent<TextMesh>();
        if (textMesh != null)
        {
            textMesh.text = Mathf.RoundToInt(damage).ToString();
            textMesh.color = textColor;
        }

        if (destroyDelay > 0f)
            Destroy(instance, destroyDelay);
    }

    public void OnAdd(GameObject target, GameplayEffectSpec spec)
    {
        // 浮动文字通常只用于 Instant 效果
    }

    public void OnRemove(GameObject target)
    {
        // 无需清理
    }
}
