using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// 浮动伤害数字管理器。使用对象池管理 TextMeshPro 或 Text 组件。
/// 支持不同伤害类型（普通、暴击、格挡、治疗）的颜色配置。
/// </summary>
public class DamageNumberManager : MonoBehaviour
{
    [Header("预制体")]
    public GameObject damageTextPrefab;   // 使用 Text 或 TextMeshPro
    public RectTransform spawnParent;     // Canvas 下的容器

    [Header("对象池")]
    public int poolSize = 20;

    [Header("动画参数")]
    public float floatDistance = 80f;
    public float duration = 1.2f;
    public float randomSpreadX = 30f;
    public AnimationCurve alphaCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);
    public AnimationCurve scaleCurve = AnimationCurve.EaseInOut(0f, 0.5f, 0.2f, 1f);

    [Header("颜色")]
    public Color normalDamageColor = Color.white;
    public Color criticalDamageColor = new Color(1f, 0.8f, 0f);
    public Color blockDamageColor = new Color(0.6f, 0.8f, 1f);
    public Color healColor = Color.green;

    private Queue<DamageNumberInstance> _pool;
    private List<DamageNumberInstance> _activeInstances;

    private void Awake()
    {
        _pool = new Queue<DamageNumberInstance>(poolSize);
        _activeInstances = new List<DamageNumberInstance>(poolSize);

        // 预创建对象
        for (int i = 0; i < poolSize; i++)
        {
            var instance = CreateInstance();
            instance.gameObject.SetActive(false);
            _pool.Enqueue(instance);
        }
    }

    private DamageNumberInstance CreateInstance()
    {
        GameObject go;
        if (damageTextPrefab != null)
        {
            go = Instantiate(damageTextPrefab, spawnParent != null ? spawnParent : transform);
        }
        else
        {
            // 没有预制体时自动创建默认 Text
            go = new GameObject("DamageText", typeof(Text), typeof(Outline));
            var text = go.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 32;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.raycastTarget = false;
            var outline = go.GetComponent<Outline>();
            outline.effectColor = Color.black;
            outline.effectDistance = new Vector2(1, -1);
            if (spawnParent != null)
                go.transform.SetParent(spawnParent, false);
            else
                go.transform.SetParent(transform, false);
        }

        var instance = go.GetComponent<DamageNumberInstance>();
        if (instance == null)
            instance = go.AddComponent<DamageNumberInstance>();

        return instance;
    }

    /// <summary>
    /// 在世界坐标位置生成伤害数字
    /// </summary>
    public void SpawnDamage(float amount, Vector3 worldPosition, DamageNumberType type = DamageNumberType.Normal)
    {
        Camera cam = Camera.main;
        if (cam == null) return;

        Vector3 screenPos = cam.WorldToScreenPoint(worldPosition);
        if (screenPos.z <= 0) return; // 在屏幕后方

        // 添加随机偏移
        screenPos.x += Random.Range(-randomSpreadX, randomSpreadX);
        screenPos.y += Random.Range(0, 20f);

        var instance = GetPooledInstance();
        instance.transform.position = screenPos;

        string text = type switch
        {
            DamageNumberType.Normal => $"{amount:F0}",
            DamageNumberType.Critical => $"<size=120%>{amount:F0}!</size>",
            DamageNumberType.Block => $"({amount:F0})",
            DamageNumberType.Heal => $"+{amount:F0}",
            _ => $"{amount:F0}"
        };

        Color color = type switch
        {
            DamageNumberType.Normal => normalDamageColor,
            DamageNumberType.Critical => criticalDamageColor,
            DamageNumberType.Block => blockDamageColor,
            DamageNumberType.Heal => healColor,
            _ => normalDamageColor
        };

        instance.Play(text, color, floatDistance, duration, alphaCurve, scaleCurve);
        _activeInstances.Add(instance);
        StartCoroutine(ReturnAfterDelay(instance, duration));
    }

    private DamageNumberInstance GetPooledInstance()
    {
        if (_pool.Count > 0)
        {
            var instance = _pool.Dequeue();
            instance.gameObject.SetActive(true);
            return instance;
        }
        // 池耗尽，动态扩展
        var newInstance = CreateInstance();
        newInstance.gameObject.SetActive(true);
        return newInstance;
    }

    private System.Collections.IEnumerator ReturnAfterDelay(DamageNumberInstance instance, float delay)
    {
        yield return new WaitForSeconds(delay);
        ReturnToPool(instance);
    }

    private void ReturnToPool(DamageNumberInstance instance)
    {
        if (instance == null) return;
        _activeInstances.Remove(instance);
        instance.gameObject.SetActive(false);
        _pool.Enqueue(instance);
    }
}

public enum DamageNumberType
{
    Normal,
    Critical,
    Block,
    Heal
}

/// <summary>
/// 单个伤害数字的 MonoBehaviour，处理动画
/// </summary>
public class DamageNumberInstance : MonoBehaviour
{
    private UnityEngine.UI.Text _text;
    private RectTransform _rectTransform;
    private CanvasGroup _canvasGroup;

    private Vector2 _startPos;
    private float _duration;
    private float _floatDistance;
    private AnimationCurve _alphaCurve;
    private AnimationCurve _scaleCurve;
    private float _elapsed;

    private void Awake()
    {
        _text = GetComponent<UnityEngine.UI.Text>();
        _rectTransform = GetComponent<RectTransform>();
        _canvasGroup = GetComponent<CanvasGroup>();
        if (_canvasGroup == null)
            _canvasGroup = gameObject.AddComponent<CanvasGroup>();
    }

    public void Play(string text, Color color, float floatDistance, float duration,
        AnimationCurve alphaCurve, AnimationCurve scaleCurve)
    {
        if (_text != null)
        {
            _text.text = text;
            _text.color = color;
        }
        _floatDistance = floatDistance;
        _duration = duration;
        _alphaCurve = alphaCurve;
        _scaleCurve = scaleCurve;
        _elapsed = 0f;
        _startPos = _rectTransform != null ? _rectTransform.anchoredPosition : Vector2.zero;
    }

    private void Update()
    {
        _elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(_elapsed / _duration);

        // 向上浮动
        if (_rectTransform != null)
        {
            _rectTransform.anchoredPosition = _startPos + Vector2.up * (_floatDistance * t);
        }

        // Alpha 变化
        if (_canvasGroup != null && _alphaCurve != null)
            _canvasGroup.alpha = _alphaCurve.Evaluate(t);

        // 缩放变化
        if (_scaleCurve != null)
            transform.localScale = Vector3.one * _scaleCurve.Evaluate(t);
    }
}
