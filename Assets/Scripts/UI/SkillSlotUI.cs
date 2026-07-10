using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// 技能冷却槽 UI。显示技能图标、冷却遮罩、倒计时文本和充能数量。
/// 通过 AbilitySystemComponent 绑定到具体的 GameplayAbility。
/// </summary>
public class SkillSlotUI : MonoBehaviour
{
    [Header("UI 引用")]
    public Image iconImage;
    public Image cooldownOverlay;  // 径向填充遮罩
    public Text cooldownText;
    public GameObject chargeCounterRoot;
    public Text chargeCounterText;
    public Image costInsufficientOverlay; // 资源不足遮罩

    [Header("样式")]
    public Color normalColor = Color.white;
    public Color cooldownColor = new Color(0.3f, 0.3f, 0.3f, 0.7f);
    public Color insufficientCostColor = new Color(0.5f, 0.2f, 0.2f, 0.5f);

    private AbilitySystemComponent _asc;
    private string _abilityName;

    private bool _isInitialized;

    public void BindToPlayer(AbilitySystemComponent asc, string abilityName)
    {
        _asc = asc;
        _abilityName = abilityName;
        _isInitialized = true;

        if (chargeCounterRoot != null)
            chargeCounterRoot.SetActive(false);
        if (costInsufficientOverlay != null)
            costInsufficientOverlay.gameObject.SetActive(false);
    }

    private void Update()
    {
        if (!_isInitialized || _asc == null) return;

        // 通过反射获取能力冷却信息
        var ability = GetAbilityInstance();
        if (ability == null)
        {
            // 能力未注册或不可用
            if (cooldownOverlay != null) cooldownOverlay.fillAmount = 1f;
            if (iconImage != null) iconImage.color = cooldownColor;
            return;
        }

        var cdInfo = ability.GetCooldownInfo();

        // 更新遮罩
        if (cooldownOverlay != null)
        {
            if (cdInfo.IsOnCooldown && cdInfo.TotalDuration > 0f)
            {
                cooldownOverlay.fillAmount = cdInfo.RemainingTime / cdInfo.TotalDuration;
                cooldownOverlay.gameObject.SetActive(true);
            }
            else
            {
                cooldownOverlay.fillAmount = 0f;
                cooldownOverlay.gameObject.SetActive(false);
            }
        }

        // 更新倒计时文本
        if (cooldownText != null)
        {
            if (cdInfo.IsOnCooldown && cdInfo.RemainingTime > 0f)
            {
                cooldownText.text = cdInfo.RemainingTime > 1f ?
                    $"{cdInfo.RemainingTime:F0}" :
                    $"{cdInfo.RemainingTime:F1}";
                cooldownText.gameObject.SetActive(true);
            }
            else
            {
                cooldownText.gameObject.SetActive(false);
            }
        }

        // 充能数量
        if (cdInfo.IsChargeBased && chargeCounterRoot != null)
        {
            chargeCounterRoot.SetActive(true);
            if (chargeCounterText != null)
                chargeCounterText.text = $"{cdInfo.RemainingCharges}/{cdInfo.MaxCharges}";
        }
        else if (chargeCounterRoot != null)
        {
            chargeCounterRoot.SetActive(false);
        }

        // 资源不足提示
        if (costInsufficientOverlay != null)
        {
            bool canAfford = ability.CheckCost();
            costInsufficientOverlay.gameObject.SetActive(!canAfford && !cdInfo.IsOnCooldown);
            if (!canAfford && !cdInfo.IsOnCooldown)
                costInsufficientOverlay.color = insufficientCostColor;
        }

        // 图标颜色
        if (iconImage != null)
        {
            iconImage.color = cdInfo.IsOnCooldown ? cooldownColor : normalColor;
        }
    }

    private GameplayAbility GetAbilityInstance()
    {
        if (_asc == null || string.IsNullOrEmpty(_abilityName)) return null;

        // 通过反射访问 private abilities 字典
        var abilitiesField = typeof(AbilitySystemComponent).GetField("abilities",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (abilitiesField == null) return null;

        var abilities = abilitiesField.GetValue(_asc) as System.Collections.Generic.Dictionary<string, GameplayAbility>;
        if (abilities == null) return null;

        abilities.TryGetValue(_abilityName, out var ability);
        return ability;
    }
}
