using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// 战斗 HUD 主控制器，管理所有 HUD 子元素的绑定和更新。
/// 挂载在 Screen Space Overlay Canvas 上。
/// </summary>
public class CombatHUD : SingletonPatternMonoBase<CombatHUD>
{
    [Header("血条")]
    public PlayerHUDController playerHealthBar;
    public PoiseBarUI poiseBar;

    [Header("技能槽")]
    public List<SkillSlotUI> skillSlots;

    [Header("目标信息")]
    public TargetInfoUI targetInfo;

    [Header("状态效果")]
    public StatusEffectListUI buffList;
    public StatusEffectListUI debuffList;

    [Header("锁定指示器")]
    public RectTransform lockOnIndicator;
    public Image lockOnIndicatorImage;

    [Header("伤害数字")]
    public DamageNumberManager damageNumberManager;

    [Header("连击")]
    public Text comboText;
    public Animator comboAnimator;

    [Header("Game Over")]
    public GameObject gameOverPanel;
    public Text gameOverTitle;
    public Button retryButton;
    public Button quitButton;

    private PlayerModel _playerModel;
    private AbilitySystemComponent _playerASC;
    private AttributeSet _playerAttributes;
    private TargetingSystem _targetingSystem;

    private int _comboCount;
    private float _comboResetTimer;
    private const float COMBO_RESET_TIME = 2.5f;

    private void Awake()
    {
        if (gameOverPanel != null)
            gameOverPanel.SetActive(false);
    }

    private void Start()
    {
        // 查找玩家引用
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            _playerModel = player.GetComponent<PlayerModel>();
            _playerASC = player.GetComponent<AbilitySystemComponent>();
            _playerAttributes = player.GetComponent<AttributeSet>();
            _targetingSystem = player.GetComponent<TargetingSystem>();

            BindToPlayer();
        }

        // Game Over 按钮
        if (retryButton != null)
            retryButton.onClick.AddListener(OnRetryClicked);
        if (quitButton != null)
            quitButton.onClick.AddListener(OnQuitClicked);

        // 初始化 GameOver 管理器引用
        if (GameOverManager.Instance != null)
            GameOverManager.Instance.SetGameOverPanel(gameOverPanel);
    }

    private void BindToPlayer()
    {
        // 血条
        if (playerHealthBar != null && _playerAttributes != null)
            playerHealthBar.Bind(_playerAttributes);

        // 架势条
        if (poiseBar != null && _playerAttributes != null)
            poiseBar.Bind(_playerAttributes);

        // 技能槽
        if (_playerASC != null)
        {
            BindSkillSlots();
        }

        // 受伤监听 (连击计数)
        if (_playerAttributes != null)
            _playerAttributes.OnAttributeChanged += OnPlayerAttributeChanged;

        // 死亡
        if (_playerAttributes != null)
            _playerAttributes.OnDeath += OnPlayerDeath;
    }

    private void BindSkillSlots()
    {
        if (skillSlots == null) return;

        // 通过反射或已知 ability 名称绑定技能槽
        // 这里使用常见的能力名称
        var abilityNames = new[] { "LightAttack", "HeavyAttack", "Dodge", "Guard" };
        for (int i = 0; i < skillSlots.Count && i < abilityNames.Length; i++)
        {
            if (skillSlots[i] != null)
                skillSlots[i].BindToPlayer(_playerASC, abilityNames[i]);
        }
    }

    private void Update()
    {
        // 更新锁定指示器位置
        UpdateLockOnIndicator();

        // 连击计时器
        if (_comboCount > 0)
        {
            _comboResetTimer -= Time.deltaTime;
            if (_comboResetTimer <= 0f)
            {
                ResetCombo();
            }
        }
    }

    private void UpdateLockOnIndicator()
    {
        if (_targetingSystem == null || !_targetingSystem.HasTarget ||
            lockOnIndicator == null || _targetingSystem.CurrentTarget == null)
        {
            if (lockOnIndicator != null && lockOnIndicator.gameObject.activeSelf)
                lockOnIndicator.gameObject.SetActive(false);
            return;
        }

        lockOnIndicator.gameObject.SetActive(true);
        Vector3 screenPos = Camera.main.WorldToScreenPoint(_targetingSystem.CurrentTarget.position);
        if (screenPos.z > 0)
        {
            lockOnIndicator.position = screenPos;
        }
        else
        {
            lockOnIndicator.gameObject.SetActive(false);
        }
    }

    private void OnPlayerAttributeChanged(GameplayAttribute attr, float oldVal, float newVal)
    {
        // 连击计数：玩家造成伤害时+1（通过 Health 变化检测不完美，但实用）
        // 更好的做法是通过 HurtBoxManager 事件，这里做简单版本
    }

    /// <summary>
    /// 由 HurtBoxManager 或攻击系统调用以增加连击
    /// </summary>
    public void IncrementCombo()
    {
        _comboCount++;
        _comboResetTimer = COMBO_RESET_TIME;
        UpdateComboDisplay();
    }

    private void UpdateComboDisplay()
    {
        if (comboText != null)
        {
            comboText.text = _comboCount > 1 ? $"x{_comboCount}" : "";
            comboText.gameObject.SetActive(_comboCount > 1);
        }
        if (comboAnimator != null && _comboCount > 1)
        {
            comboAnimator.SetTrigger("Combo");
        }
    }

    private void ResetCombo()
    {
        _comboCount = 0;
        _comboResetTimer = 0f;
        UpdateComboDisplay();
    }

    private void OnPlayerDeath()
    {
        if (gameOverPanel != null)
            gameOverPanel.SetActive(true);
        if (gameOverTitle != null)
            gameOverTitle.text = "You Died";
    }

    private void OnRetryClicked()
    {
        Time.timeScale = 1f;
        UnityEngine.SceneManagement.SceneManager.LoadScene(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
    }

    private void OnQuitClicked()
    {
        Time.timeScale = 1f;
        UnityEngine.SceneManagement.SceneManager.LoadScene(0);
    }

    private void OnDestroy()
    {
        if (_playerAttributes != null)
        {
            _playerAttributes.OnAttributeChanged -= OnPlayerAttributeChanged;
            _playerAttributes.OnDeath -= OnPlayerDeath;
        }

        if (GameOverManager.Instance != null)
            GameOverManager.Instance.SetGameOverPanel(null);
    }
}
