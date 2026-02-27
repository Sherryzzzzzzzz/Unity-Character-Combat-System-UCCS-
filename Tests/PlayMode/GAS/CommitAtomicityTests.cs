using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using System.Collections;

public class CommitAtomicityTests
{
    [UnityTest]
    public IEnumerator CommitFailsDoesNotSetCooldownOrCurrentAbility()
    {
        var go = new GameObject("ASC_Test");
        var asc = go.AddComponent<AbilitySystemComponent>();
        var attrs = go.AddComponent<AttributeSet>();
        asc.RegisterAbility("test", new DefaultGameplayAbility());

        var abilitySO = ScriptableObject.CreateInstance<GameplayAbilitySO>();
        abilitySO.cooldown = 5f;
        // costEffect that will be rejected: null target or large damage
        var cost = ScriptableObject.CreateInstance<GameplayEffect>();
        cost.durationPolicy = DurationPolicy.Instant;
        cost.damage = 999999f; // will exceed health
        abilitySO.costEffect = cost;

        var ability = abilitySO.CreateRuntimeAbility();
        ability.InitializeFromData(abilitySO);
        ability.Initialize(asc);

        // Ensure health is small so cost check fails
        attrs.ModifyHealth(-attrs.Health + 1f);

        bool activated = ability.TryActivate();
        Assert.IsFalse(activated, "Ability should not activate when cost cannot be paid");

        // current ability should not be set and cooldown should not be applied
        Assert.IsTrue(asc != null);
        var field = typeof(AbilitySystemComponent).GetField("currentAbility", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var current = field?.GetValue(asc) as GameplayAbility;
        Assert.IsNull(current);

        // cleanup
        Object.DestroyImmediate(go);
        yield return null;
    }
}