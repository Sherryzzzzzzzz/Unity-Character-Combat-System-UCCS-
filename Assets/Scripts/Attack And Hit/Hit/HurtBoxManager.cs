using UnityEngine;

public class HurtBoxManager : MonoBehaviour
{
    public GameplayTagSO perfectParryTag;
    public GameplayTagSO normalParryTag;
    public GameplayTagSO guardingTag;
    public GameplayTagSO parrySuccessTag;

    private TagComponent _tagComponent;

    private DamageProcessor damageProcessor;
    private HitReactionController hitReactionController;

    public bool isInvincible = false;

    private void Awake()
    {
        _tagComponent = GetComponent<TagComponent>();
        damageProcessor = GetComponent<DamageProcessor>();
        hitReactionController = GetComponent<HitReactionController>();
    }

    public void ProcessHit(AttackEvent hit, GameObject attacker)
    {
        if (isInvincible)
            return;

        // 弹反
        if (_tagComponent.HasTag(perfectParryTag) ||
            _tagComponent.HasTag(normalParryTag))
        {
            var attackerTagComponent = attacker.GetComponent<TagComponent>();
            if (attackerTagComponent != null && parrySuccessTag != null)
                attackerTagComponent.AddTransientTag(parrySuccessTag);
            return;
        }

        // 格挡
        if (_tagComponent.HasTag(guardingTag))
        {
            return;
        }

        // 数值
        damageProcessor?.ApplyDamage(hit);

        // 反应
        hitReactionController?.PlayHit(hit);
    }
}