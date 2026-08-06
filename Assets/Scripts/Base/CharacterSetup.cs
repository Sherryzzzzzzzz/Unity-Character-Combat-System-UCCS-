using UnityEngine;

/// <summary>
/// Quick character initialization helper. Drop this on an empty GameObject,
/// pick Player or Enemy type, drag in a model prefab and avatar, then hit
/// "Setup Character" to auto-attach every required component.
/// </summary>
public class CharacterSetup : MonoBehaviour
{
    public enum CharacterType
    {
        Player,
        Enemy
    }

    [Header("Character Config")]
    [Tooltip("Player or Enemy — decides which components get attached.")]
    public CharacterType type = CharacterType.Player;

    [Tooltip("Drop the visual model prefab here. It will be instantiated as a child.")]
    public GameObject modelPrefab;

    [Tooltip("Avatar for the Animator (usually comes with the model).")]
    public Avatar avatar;

    [Header("Optional References")]
    [Tooltip("If assigned, the model child will be parented here instead of the root.")]
    public Transform modelParent;

    [ContextMenu("Setup Character")]
    public void SetupCharacter()
    {
        if (this == null || gameObject == null) return;

        var root = gameObject;

        // --- Common components (both Player and Enemy) ---

        // CharacterController
        var cc = EnsureComponent<CharacterController>(root);
        cc.center = new Vector3(0f, 1f, 0f);
        cc.radius = 0.35f;
        cc.height  = 1.8f;

        // Animator
        var animator = EnsureComponent<Animator>(root);
        if (avatar != null) animator.avatar = avatar;

        // Animancer
        EnsureComponent<Animancer.AnimancerComponent>(root);

        // TagComponent
        EnsureComponent<TagComponent>(root);

        // HurtBoxManager
        EnsureComponent<HurtBoxManager>(root);

        // Parryable
        EnsureComponent<Parryable>(root);

        // AbilitySystemComponent
        EnsureComponent<AbilitySystemComponent>(root);

        // AttributeSet
        EnsureComponent<AttributeSet>(root);

        // HitReactionController
        EnsureComponent<HitReactionController>(root);

        // --- Type-specific components ---

        if (type == CharacterType.Player)
        {
            EnsureComponent<PlayerModel>(root);
            EnsureComponent<PlayerSkillComponent>(root);
            EnsureComponent<Cinemachine.CinemachineImpulseSource>(root);
            EnsureComponent<FootIKController>(root);
            EnsureComponent<TargetingSystem>(root);
            EnsureComponent<LockOnCameraSwitcher>(root);
            EnsureComponent<DodgeAbility>(root);

            Debug.Log($"[CharacterSetup] Player setup complete on '{root.name}'. {root.GetComponents<Component>().Length} components attached.");
        }
        else
        {
            EnsureComponent<EnemyModel>(root);
            EnsureComponent<EnemySkillComponent>(root);
            EnsureComponent<EnemyAnimationData>(root);
            EnsureComponent<EnemyAnimationDriver>(root);

            // ★ 敌人 AI：自研行为树（已替换 Behavior Designer）
            var btreeRunner = EnsureComponent<BTreeRunner>(root);
#if UNITY_EDITOR
            if (btreeRunner.treeAsset == null)
            {
                var aiAsset = UnityEditor.AssetDatabase.LoadAssetAtPath<BTreeAsset>(
                    "Assets/Data/AI/Enemy_Aggressive_BTree.asset");
                if (aiAsset != null)
                {
                    btreeRunner.treeAsset = aiAsset;
                    UnityEditor.EditorUtility.SetDirty(btreeRunner);
                }
            }
#endif

            Debug.Log($"[CharacterSetup] Enemy setup complete on '{root.name}'. {root.GetComponents<Component>().Length} components attached.");
        }

        // --- Instantiate model as child ---
        if (modelPrefab != null)
        {
            var parent = modelParent != null ? modelParent : root.transform;
            var existingModel = parent.Find(modelPrefab.name);
            if (existingModel == null)
            {
                var modelInstance = Instantiate(modelPrefab, parent);
                modelInstance.name = modelPrefab.name;
                Debug.Log($"[CharacterSetup] Model '{modelPrefab.name}' instantiated under '{parent.name}'");
            }
            else
            {
                Debug.Log($"[CharacterSetup] Model '{modelPrefab.name}' already exists — skipped.");
            }
        }

#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(root);
#endif
    }

    private T EnsureComponent<T>(GameObject go) where T : Component
    {
        var comp = go.GetComponent<T>();
        if (comp == null)
            comp = go.AddComponent<T>();
        return comp;
    }
}
