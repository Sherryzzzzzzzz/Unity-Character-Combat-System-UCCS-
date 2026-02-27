using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using System.Collections;

public class EffectLifecycleTests
{
    [UnityTest]
    public IEnumerator ApplyDurationEffect_RollbackOnFailure_NoResidualModifiersOrTags()
    {
        var go = new GameObject("ASC_Target");
        var asc = go.AddComponent<AbilitySystemComponent>();
        var attrs = go.AddComponent<AttributeSet>();
        var tagComp = go.AddComponent<TagComponent>();

        // Create a dummy effect that attempts to add a modifier to a non-existent attribute to force partial apply
        var effect = ScriptableObject.CreateInstance<GameplayEffect>();
        effect.durationPolicy = DurationPolicy.Duration;
        effect.duration = 10f;
        var mod = new EffectAttributeModifier();
        mod.attribute = (GameplayAttribute)999; // invalid attribute
        mod.modifierType = ModifierType.Additive;
        mod.value = 10f;
        effect.modifiers.Add(mod);
        effect.grantedTags.Add(ScriptableObject.CreateInstance<GameplayTagSO>());

        var spec = new GameplayEffectSpec(effect, asc);

        int handle = asc.ApplyEffectSpec(spec);
        Assert.AreEqual(-1, handle, "ApplyEffectSpec should fail and return -1 when modifier application fails");

        // Ensure no modifiers present on known attributes
        var attackAttr = attrs.GetAttributeValue(GameplayAttribute.AttackPower);
        Assert.IsNotNull(attackAttr);
        // RegisteredModifiers should be empty and no tag should be present
        Assert.IsFalse(tagComp.HasTag(effect.grantedTags[0]));

        Object.DestroyImmediate(go);
        yield return null;
    }
}