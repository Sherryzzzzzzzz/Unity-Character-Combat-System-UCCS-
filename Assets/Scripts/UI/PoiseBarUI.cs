using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 架势条 (Poise/Stance Bar) UI。显示当前架势值，归零时闪烁警告。
/// </summary>
public class PoiseBarUI : HealthBarController
{
    [Header("Poise 特有配置")]
    public Image poiseForeground;
    public Color poiseNormalColor = new Color(0.8f, 0.6f, 0.2f);
    public Color poiseBreakFlashColor = Color.red;
    public float flashInterval = 0.2f;

    private float _poiseMax;
    private bool _wasBroken;
    private Coroutine _flashCoroutine;

    protected override void Awake()
    {
        base.Awake();
        if (poiseForeground == null)
            poiseForeground = foregroundImage;
    }

    public override void Bind(AttributeSet attrs)
    {
        base.Bind(attrs);
        _poiseMax = attrs.PoiseMax;
        _wasBroken = false;

        // 监听 Poise 事件
        attrs.OnPoiseBreak += OnPoiseBreak;
        attrs.OnPoiseRecover += OnPoiseRecover;

        if (poiseForeground != null)
            poiseForeground.color = poiseNormalColor;
    }

    public override void Unbind()
    {
        if (boundAttributes != null)
        {
            boundAttributes.OnPoiseBreak -= OnPoiseBreak;
            boundAttributes.OnPoiseRecover -= OnPoiseRecover;
        }
        base.Unbind();
    }

    private void OnPoiseBreak()
    {
        _wasBroken = true;
        if (_flashCoroutine != null) StopCoroutine(_flashCoroutine);
        _flashCoroutine = StartCoroutine(PoiseBreakFlash());
    }

    private void OnPoiseRecover()
    {
        _wasBroken = false;
        if (_flashCoroutine != null)
        {
            StopCoroutine(_flashCoroutine);
            _flashCoroutine = null;
        }
        if (poiseForeground != null)
            poiseForeground.color = poiseNormalColor;
    }

    private System.Collections.IEnumerator PoiseBreakFlash()
    {
        while (_wasBroken)
        {
            if (poiseForeground != null)
            {
                poiseForeground.color = poiseForeground.color == poiseNormalColor ?
                    poiseBreakFlashColor : poiseNormalColor;
            }
            yield return new WaitForSeconds(flashInterval);
        }
    }

    protected override void UpdateVisuals(float normalizedHealth)
    {
        // 重写为 Poise 专用逻辑
        if (boundAttributes == null) return;
        float poise = boundAttributes.Poise;
        float maxPoise = boundAttributes.PoiseMax;
        float normalizedPoise = maxPoise > 0f ? Mathf.Clamp01(poise / maxPoise) : 0f;

        if (poiseForeground != null)
            poiseForeground.fillAmount = normalizedPoise;
    }

    protected override void OnAttributeChanged(GameplayAttribute attr, float oldVal, float newVal)
    {
        // Poise 不需要 damage flash，由专用 flash 处理
    }
}
