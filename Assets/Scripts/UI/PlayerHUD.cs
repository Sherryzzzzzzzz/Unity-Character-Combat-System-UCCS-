using UnityEngine;
using UnityEngine.UI;

public class PlayerHUD : MonoBehaviour
{
    [Header("血条 — 红色（即时）")]
    public Image hpRedBar;
    [Header("血条 — 黄色（延迟扣血）")]
    public Image hpYellowBar;
    public float hpYellowDelay = 0.5f;
    public float hpYellowSpeed = 1.5f;

    [Header("体力条 — 绿色")]
    public Image staminaGreenBar;
    public float staminaCostDodge = 20f;

    [Header("Runtime Debug")]
    [SerializeField] private AttributeSet boundAttributes;
    [SerializeField] private float currentHealth;
    [SerializeField] private float maxHealth;
    [SerializeField] private float currentStamina;
    [SerializeField] private float maxStamina;
    [SerializeField] private float healthFill;
    [SerializeField] private float yellowHealthFill;
    [SerializeField] private float staminaFill;
    [SerializeField] private bool isBound;

    private float _yellowHP = 1f;
    private float _displayStamina = 1f;
    private float _hpYellowTimer;
    private bool _initDone;
    private AttributeSet _playerAttributes;

    void Start()
    {
        if (hpRedBar == null || hpYellowBar == null || staminaGreenBar == null)
            AutoCreateBars();

        StartCoroutine(InitRoutine());
    }

    System.Collections.IEnumerator InitRoutine()
    {
        for (int i = 0; i < 60; i++)
        {
            var attr = GetPlayerAttributes();

            if (attr != null && attr.HealthMax > 0f)
            {
                _yellowHP = attr.Health / attr.HealthMax;
                _initDone = true;
                Debug.Log($"[PlayerHUD] Bound: HP={attr.Health}/{attr.HealthMax} Stamina={attr.Stamina}/{attr.StaminaMax}");
                yield break;
            }
            yield return new WaitForSeconds(0.05f);
        }
        Debug.LogWarning("[PlayerHUD] Failed to find PlayerAttributes after 3s");
    }

    void AutoCreateBars()
    {
        // Ensure we have a Canvas parent
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
        {
            var canvasGo = new GameObject("GameCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGo.GetComponent<CanvasScaler>().referenceResolution = new Vector2(1920, 1080);
            transform.SetParent(canvasGo.transform, false);
        }

        var rt = GetComponent<RectTransform>();
        if (rt == null) { var r = gameObject.AddComponent<RectTransform>(); rt = r; }
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0, 1);
        rt.anchoredPosition = new Vector2(45, -35);
        rt.sizeDelta = new Vector2(300, 60);

        // Background
        if (transform.Find("BG") == null)
        {
            var bg = new GameObject("BG", typeof(Image));
            bg.transform.SetParent(transform, false);
            bg.GetComponent<RectTransform>().Stretch();
            bg.GetComponent<Image>().color = new Color(0, 0, 0, 0.45f);
        }

        // HP Bars
        if (hpRedBar == null || hpYellowBar == null)
        {
            var hpRow = EnsureRow("HPRow", 0, 0.6f, 1, 1);
            if (hpYellowBar == null) hpYellowBar = MakeBar(hpRow, "YellowBar", new Color(0.85f, 0.7f, 0.15f));
            if (hpRedBar   == null) hpRedBar   = MakeBar(hpRow, "RedBar",    new Color(0.75f, 0.1f, 0.08f));
        }

        // Stamina Bar
        if (staminaGreenBar == null)
        {
            var stRow = EnsureRow("StaminaRow", 0, 0.05f, 1, 0.55f);
            staminaGreenBar = MakeBar(stRow, "GreenBar", new Color(0.15f, 0.7f, 0.25f));
        }

        Debug.Log("[PlayerHUD] Auto-created missing bar references");
    }

    Transform EnsureRow(string name, float x1, float y1, float x2, float y2)
    {
        var t = transform.Find(name);
        if (t != null) return t;
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(transform, false);
        var r = go.GetComponent<RectTransform>();
        r.anchorMin = new Vector2(x1, y1); r.anchorMax = new Vector2(x2, y2);
        r.offsetMin = new Vector2(10, 2); r.offsetMax = new Vector2(-10, -2);
        return go.transform;
    }

    Image MakeBar(Transform row, string name, Color c)
    {
        var go = new GameObject(name, typeof(Image));
        go.transform.SetParent(row, false);
        go.GetComponent<RectTransform>().Stretch();
        var img = go.GetComponent<Image>();
        img.color = c;
        img.type = Image.Type.Filled;
        img.fillMethod = Image.FillMethod.Horizontal;
        img.fillOrigin = 0;
        img.fillAmount = 1f;
        return img;
    }

    void Update()
    {
        var attr = GetPlayerAttributes();
        if (attr == null || attr.HealthMax <= 0f)
        {
            _initDone = false;
            boundAttributes = null;
            currentHealth = 0f;
            maxHealth = 0f;
            currentStamina = 0f;
            maxStamina = 0f;
            healthFill = 0f;
            yellowHealthFill = 0f;
            staminaFill = 0f;
            isBound = false;
            return;
        }

        if (!_initDone)
        {
            _yellowHP = attr.Health / attr.HealthMax;
            _displayStamina = attr.StaminaMax > 0f ? attr.Stamina / attr.StaminaMax : 0f;
            _initDone = true;
        }

        float maxHP = attr.HealthMax;
        float curHP = attr.Health;
        float hpPct = Mathf.Clamp01(curHP / maxHP);

        boundAttributes = attr;
        currentHealth = curHP;
        maxHealth = maxHP;
        if (hpPct < healthFill)
            _hpYellowTimer = hpYellowDelay;

        healthFill = hpPct;
        isBound = true;

        ApplyBarFill(hpRedBar, hpPct);

        if (_hpYellowTimer > 0f)
        {
            _hpYellowTimer -= Time.deltaTime;
        }
        else if (_yellowHP > hpPct)
        {
            _yellowHP = Mathf.MoveTowards(_yellowHP, hpPct, Time.deltaTime * hpYellowSpeed);
        }
        else
        {
            _yellowHP = hpPct;
        }

        yellowHealthFill = _yellowHP;
        ApplyBarFill(hpYellowBar, _yellowHP);

        float maxStaminaValue = attr.StaminaMax;
        float curStamina = attr.Stamina;
        float staminaPct = Mathf.Clamp01(curStamina / Mathf.Max(maxStaminaValue, 1f));

        currentStamina = curStamina;
        maxStamina = maxStaminaValue;
        staminaFill = staminaPct;

        _displayStamina = Mathf.Lerp(_displayStamina, staminaPct, Time.deltaTime * 8f);
        ApplyBarFill(staminaGreenBar, _displayStamina);
    }

    private void ApplyBarFill(Image bar, float value)
    {
        if (bar == null) return;

        value = Mathf.Clamp01(value);
        bar.type = Image.Type.Simple;
        bar.fillAmount = 1f;

        var rectTransform = bar.rectTransform;
        rectTransform.anchorMin = new Vector2(0f, rectTransform.anchorMin.y);
        rectTransform.anchorMax = new Vector2(value, rectTransform.anchorMax.y);
        rectTransform.offsetMin = new Vector2(0f, rectTransform.offsetMin.y);
        rectTransform.offsetMax = new Vector2(0f, rectTransform.offsetMax.y);
        rectTransform.localScale = new Vector3(1f, rectTransform.localScale.y, rectTransform.localScale.z);
    }

    private AttributeSet GetPlayerAttributes()
    {
        if (_playerAttributes != null) return _playerAttributes;

        var attr = AttributeSet.PlayerAttributes;
        if (attr != null)
        {
            _playerAttributes = attr;
            return _playerAttributes;
        }

        var playerModel = FindObjectOfType<PlayerModel>();
        if (playerModel != null)
        {
            _playerAttributes = playerModel.GetComponent<AttributeSet>();
            if (_playerAttributes == null)
                _playerAttributes = playerModel.GetComponentInParent<AttributeSet>();
            if (_playerAttributes == null)
                _playerAttributes = playerModel.GetComponentInChildren<AttributeSet>();
            if (_playerAttributes != null) return _playerAttributes;
        }

        var playerGo = GameObject.FindGameObjectWithTag("Player");
        if (playerGo == null) return null;

        _playerAttributes = playerGo.GetComponent<AttributeSet>();
        if (_playerAttributes == null)
            _playerAttributes = playerGo.GetComponentInParent<AttributeSet>();
        if (_playerAttributes == null)
            _playerAttributes = playerGo.GetComponentInChildren<AttributeSet>();
        return _playerAttributes;
    }

    public bool ConsumeDodgeStamina()
    {
        var attr = GetPlayerAttributes();
        return attr != null && attr.TryConsumeStamina(staminaCostDodge);
    }
}

