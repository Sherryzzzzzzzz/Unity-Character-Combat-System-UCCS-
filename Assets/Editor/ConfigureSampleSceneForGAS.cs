#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Editor utility to configure Assets/Scenes/SampleScene.unity for GAS demo
// It will open the scene, find a PlayerModel or create an AbilitySystemComponent on a suitable actor,
// and assign the example GameplayAbilitySO assets created by CreateExampleAbilities.
public static class ConfigureSampleSceneForGAS
{
    [MenuItem("GAS Example/Configure Sample Scene")]
    public static void ConfigureSampleScene()
    {
        string scenePath = "Assets/Scenes/SampleScene.unity";
        if (!System.IO.File.Exists(scenePath))
        {
            Debug.LogError($"SampleScene not found at {scenePath}");
            return;
        }

        var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        Debug.Log($"Opened scene: {scene.path}");

        // Try to find a PlayerModel in the scene
        PlayerModel playerModel = Object.FindObjectOfType<PlayerModel>();
        GameObject targetGO = null;
        if (playerModel != null)
        {
            targetGO = playerModel.gameObject;
            Debug.Log($"Found PlayerModel on GameObject: {targetGO.name}");
        }
        else
        {
            // fallback: try to find a GameObject named "Player"
            var go = GameObject.Find("Player");
            if (go != null) { targetGO = go; Debug.Log("Found GameObject named 'Player'"); }
        }

        if (targetGO == null)
        {
            Debug.LogWarning("No PlayerModel or GameObject named 'Player' found in SampleScene. Creating a new GameObject 'DemoPlayer'.");
            targetGO = new GameObject("DemoPlayer");
            // place at origin
            targetGO.transform.position = Vector3.zero;
            // mark scene dirty so it gets saved later
            EditorSceneManager.MarkSceneDirty(scene);
        }

        // Ensure AbilitySystemComponent exists on target
        var asc = targetGO.GetComponent<AbilitySystemComponent>();
        if (asc == null)
        {
            asc = targetGO.AddComponent<AbilitySystemComponent>();
            Debug.Log("Added AbilitySystemComponent to " + targetGO.name);
        }

        // Load example assets
        var instantSo = AssetDatabase.LoadAssetAtPath<GameplayAbilitySO>("Assets/Data/Abilities/InstantDamageAbility.asset");
        var dotSo = AssetDatabase.LoadAssetAtPath<GameplayAbilitySO>("Assets/Data/Abilities/DotDamageAbility.asset");
        var buffSo = AssetDatabase.LoadAssetAtPath<GameplayAbilitySO>("Assets/Data/Abilities/BuffAttackAbility.asset");

        var list = new System.Collections.Generic.List<GameplayAbilitySO>();
        if (instantSo != null) list.Add(instantSo); else Debug.LogWarning("InstantDamageAbility asset not found.");
        if (dotSo != null) list.Add(dotSo); else Debug.LogWarning("DotDamageAbility asset not found.");
        if (buffSo != null) list.Add(buffSo); else Debug.LogWarning("BuffAttackAbility asset not found.");

        if (list.Count == 0)
        {
            Debug.LogError("No example abilities found to assign. Run 'GAS Example/Create Example Abilities' first.");
            return;
        }

        // Assign to asc (serialized field)
        asc.GetType().GetField("abilityDataList", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public)
            ?.SetValue(asc, list);

        // If PlayerModel exists, ensure its pac or wp references are valid (best effort)
        if (playerModel != null)
        {
            var pac = playerModel.pac;
            if (pac != null)
            {
                Debug.Log("PlayerModel has PlayerSkillComponent (pac) present. No change needed.");
            }
        }

        // Save scene
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("Configured SampleScene for GAS demo and saved the scene.");
    }
}
#endif