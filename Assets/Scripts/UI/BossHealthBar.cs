using UnityEngine;
using UnityEngine.UI;

/// <summary>BOSS 血条 — 屏幕底边居中，自动绑定敌人</summary>
public class BossHealthBar : MonoBehaviour
{
    public GameObject rootPanel;
    public Text bossNameText;
    public Image healthBarFill;
    public float barSmoothSpeed = 2f;

    [Header("Runtime Debug")]
    [SerializeField] private AttributeSet boundBossAttributes;
    [SerializeField] private float currentHealth;
    [SerializeField] private float maxHealth;
    [SerializeField] private float healthFill;
    [SerializeField] private bool isBound;

    private AttributeSet _bossAttr;
    private float _displayHP = 1f;

    void Start()
    {
        // 自动创建缺失的 UI 元素
        if (rootPanel == null) rootPanel = gameObject;
        if (healthBarFill == null || bossNameText == null) AutoCreate();

        StartCoroutine(AutoBindRoutine());
    }

    System.Collections.IEnumerator AutoBindRoutine()
    {
        for (int i = 0; i < 60; i++)
        {
            if (_bossAttr != null && _bossAttr.HealthMax > 0f) yield break;

            var attr = FindBossAttributes();
            if (attr != null && attr.HealthMax > 0f)
            {
                BindBoss(attr, "BOSS");
                yield break;
            }
            yield return new WaitForSeconds(0.05f);
        }
    }

    private AttributeSet FindBossAttributes()
    {
        var enemy = GameObject.Find("Charater/Enemy");
        if (enemy == null) enemy = GameObject.Find("Enemy");
        if (enemy != null)
        {
            var attr = enemy.GetComponent<AttributeSet>();
            if (attr == null) attr = enemy.GetComponentInChildren<AttributeSet>();
            if (attr == null) attr = enemy.GetComponentInParent<AttributeSet>();
            if (attr != null) return attr;
        }

        var allAttributes = FindObjectsOfType<AttributeSet>();
        foreach (var attr in allAttributes)
        {
            if (attr == null || attr == AttributeSet.PlayerAttributes) continue;
            if (attr.GetComponent<PlayerModel>() != null || attr.GetComponentInParent<PlayerModel>() != null ||
                attr.GetComponentInChildren<PlayerModel>() != null) continue;
            if (attr.HealthMax > 0f) return attr;
        }
        return null;
    }

    void AutoCreate()
    {
        var canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
        {
            var cgo = new GameObject("GameCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = cgo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            cgo.GetComponent<CanvasScaler>().referenceResolution = new Vector2(1920, 1080);
            transform.SetParent(cgo.transform, false);
        }

        var rt = GetComponent<RectTransform>();
        if (rt == null) { var r = gameObject.AddComponent<RectTransform>(); rt = r; }
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0);
        rt.pivot = new Vector2(0.5f, 0);
        rt.anchoredPosition = new Vector2(0, 50);
        rt.sizeDelta = new Vector2(500, 45);

        if (transform.Find("BG") == null)
        {
            var bg = new GameObject("BG", typeof(Image));
            bg.transform.SetParent(transform, false);
            bg.GetComponent<RectTransform>().Stretch();
            bg.GetComponent<Image>().color = new Color(0, 0, 0, 0.6f);
        }

        if (bossNameText == null)
        {
            var nt = new GameObject("Name", typeof(Text));
            nt.transform.SetParent(transform, false);
            var nr = nt.GetComponent<RectTransform>();
            nr.Stretch(); nr.offsetMin = new Vector2(10, 22); nr.offsetMax = new Vector2(-10, 0);
            bossNameText = nt.GetComponent<Text>();
            bossNameText.text = "BOSS";
            bossNameText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            bossNameText.fontSize = 20;
            bossNameText.color = new Color(0.9f, 0.8f, 0.4f);
            bossNameText.alignment = TextAnchor.MiddleCenter;
        }

        if (healthBarFill == null)
        {
            var fb = new GameObject("Fill", typeof(Image));
            fb.transform.SetParent(transform, false);
            var fr = fb.GetComponent<RectTransform>();
            fr.Stretch(); fr.offsetMin = new Vector2(10, 3); fr.offsetMax = new Vector2(-10, -18);
            healthBarFill = fb.GetComponent<Image>();
            healthBarFill.color = new Color(0.65f, 0.15f, 0.08f);
            healthBarFill.type = Image.Type.Filled;
            healthBarFill.fillMethod = Image.FillMethod.Horizontal;
            healthBarFill.fillAmount = 1f;
        }

        rootPanel = gameObject;
        rootPanel.SetActive(true);
    }

    public void BindBoss(AttributeSet attr, string name = null)
    {
        _bossAttr = attr;
        boundBossAttributes = attr;
        isBound = attr != null;
        if (bossNameText != null && !string.IsNullOrEmpty(name))
            bossNameText.text = name;
        if (rootPanel != null) rootPanel.SetActive(true);
        _displayHP = 1f;
    }

    void Update()
    {
        if (_bossAttr == null) return;

        float maxHP = _bossAttr.HealthMax;
        float curHP = _bossAttr.Health;
        if (maxHP <= 0f) maxHP = 1f;

        float targetFill = Mathf.Clamp01(curHP / maxHP);
        _displayHP = Mathf.MoveTowards(_displayHP, targetFill, Time.deltaTime * barSmoothSpeed);

        currentHealth = curHP;
        maxHealth = maxHP;
        healthFill = _displayHP;
        boundBossAttributes = _bossAttr;
        isBound = true;

        ApplyBarFill(_displayHP);

        if (curHP <= 0f && _displayHP < 0.01f && rootPanel != null)
            rootPanel.SetActive(false);
    }

    private void ApplyBarFill(float value)
    {
        if (healthBarFill == null) return;

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

public static class BossBarEx { public static void Stretch(this RectTransform r) { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero; } }