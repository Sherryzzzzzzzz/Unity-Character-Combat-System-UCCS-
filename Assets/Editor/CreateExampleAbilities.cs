#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

// Editor utility to create example GameplayAbilitySO and GameplayEffect assets
// Usage: Window -> GAS Example -> Create Example Assets
public class CreateExampleAbilities : EditorWindow
{
    [MenuItem("GAS Example/Create Example Abilities")]
    public static void ShowWindow()
    {
        GetWindow<CreateExampleAbilities>("Create GAS Example Assets");
    }

    private void OnGUI()
    {
        GUILayout.Label("Create example GameplayAbilitySO and GameplayEffect assets", EditorStyles.boldLabel);
        if (GUILayout.Button("Create example assets"))
        {
            CreateAssets();
        }
    }

    private static void EnsureFolder(string path)
    {
        if (!AssetDatabase.IsValidFolder(path))
        {
            var parent = System.IO.Path.GetDirectoryName(path.TrimEnd('/'));
            var name = System.IO.Path.GetFileName(path.TrimEnd('/'));
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
                EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }
    }

    public static void CreateAssets()
    {
        EnsureFolder("Assets/Data/Abilities");

        // Instant damage effect
        var instEffect = ScriptableObject.CreateInstance<GameplayEffect>();
        instEffect.name = "InstantDamageEffect";
        instEffect.damage = 15f;
        instEffect.durationPolicy = DurationPolicy.Instant;
        AssetDatabase.CreateAsset(instEffect, "Assets/Data/Abilities/InstantDamageEffect.asset");

        // DOT effect
        var dotEffect = ScriptableObject.CreateInstance<GameplayEffect>();
        dotEffect.name = "DotDamageEffect";
        dotEffect.damage = 5f;
        dotEffect.durationPolicy = DurationPolicy.Duration;
        dotEffect.duration = 3f;
        dotEffect.period = 1f;
        AssetDatabase.CreateAsset(dotEffect, "Assets/Data/Abilities/DotDamageEffect.asset");

        // Buff effect (increase attack power via modifier)
        var buffEffect = ScriptableObject.CreateInstance<GameplayEffect>();
        buffEffect.name = "BuffAttackEffect";
        buffEffect.durationPolicy = DurationPolicy.Duration;
        buffEffect.duration = 10f;
        // Create a simple static modifier if EffectAttributeModifier exists
        var modListField = typeof(GameplayEffect).GetField("modifiers");
        if (modListField != null)
        {
            var list = (System.Collections.IList)modListField.GetValue(buffEffect);
            // Try to construct EffectAttributeModifier via reflection if type exists
            var modType = typeof(GameplayEffect).Assembly.GetType("EffectAttributeModifier");
            if (modType != null)
            {
                var mod = System.Activator.CreateInstance(modType);
                var attrField = modType.GetField("attribute");
                var valField = modType.GetField("value");
                if (attrField != null) attrField.SetValue(mod, GameplayAttribute.AttackPower);
                if (valField != null) valField.SetValue(mod, 5f);
                list.Add(mod);
            }
        }
        AssetDatabase.CreateAsset(buffEffect, "Assets/Data/Abilities/BuffAttackEffect.asset");

        // GameplayAbilitySO: instant-damage
        var instantSO = ScriptableObject.CreateInstance<GameplayAbilitySO>();
        instantSO.name = "InstantDamageAbility";
        instantSO.abilityName = "InstantDamage";
        instantSO.cooldown = 1f;
        instantSO.effectsToApply = new System.Collections.Generic.List<GameplayEffect> { instEffect };
        AssetDatabase.CreateAsset(instantSO, "Assets/Data/Abilities/InstantDamageAbility.asset");

        // GameplayAbilitySO: dot-damage
        var dotSO = ScriptableObject.CreateInstance<GameplayAbilitySO>();
        dotSO.name = "DotDamageAbility";
        dotSO.abilityName = "DotDamage";
        dotSO.cooldown = 2f;
        dotSO.effectsToApply = new System.Collections.Generic.List<GameplayEffect> { dotEffect };
        AssetDatabase.CreateAsset(dotSO, "Assets/Data/Abilities/DotDamageAbility.asset");

        // GameplayAbilitySO: buff-attack
        var buffSO = ScriptableObject.CreateInstance<GameplayAbilitySO>();
        buffSO.name = "BuffAttackAbility";
        buffSO.abilityName = "BuffAttack";
        buffSO.cooldown = 5f;
        buffSO.effectsToApply = new System.Collections.Generic.List<GameplayEffect> { buffEffect };
        AssetDatabase.CreateAsset(buffSO, "Assets/Data/Abilities/BuffAttackAbility.asset");

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("Created example GAS assets in Assets/Data/Abilities/");
    }
}
#endif