using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(CharacterSetup))]
public class CharacterSetupEditor : Editor
{
    public override void OnInspectorGUI()
    {
        var setup = (CharacterSetup)target;

        EditorGUILayout.HelpBox(
            "一键初始化角色：选择 Player/Enemy 类型，拖入模型 Prefab 和 Avatar，点击下方按钮即可自动挂载所有必要组件。",
            MessageType.Info);

        DrawDefaultInspector();

        EditorGUILayout.Space(12);

        GUI.backgroundColor = new Color(0.3f, 0.8f, 0.3f);
        if (GUILayout.Button("Setup Character", GUILayout.Height(36)))
        {
            Undo.RecordObject(setup.gameObject, "Setup Character");
            setup.SetupCharacter();
        }
        GUI.backgroundColor = Color.white;

        EditorGUILayout.Space(8);

        // Show what will be added
        EditorGUILayout.LabelField("Will attach:", EditorStyles.boldLabel);
        var type = setup.type;
        if (type == CharacterSetup.CharacterType.Player)
        {
            EditorGUILayout.HelpBox(
                "Player components:\n" +
                "• CharacterController, Animator, AnimancerComponent\n" +
                "• PlayerModel, PlayerSkillComponent\n" +
                "• HurtBoxManager, Parryable, TagComponent\n" +
                "• AbilitySystemComponent, AttributeSet\n" +
                "• HitReactionController, LockOnCameraSwitcher\n" +
                "• TargetingSystem, DodgeAbility\n" +
                "• CinemachineImpulseSource, FootIKController\n" +
                "• RedirectRootMotionToCharacterController",
                MessageType.None);
        }
        else
        {
            EditorGUILayout.HelpBox(
                "Enemy components:\n" +
                "• CharacterController, Animator, AnimancerComponent\n" +
                "• EnemyModel, EnemySkillComponent\n" +
                "• EnemyAnimationData, EnemyAnimationDriver\n" +
                "• BehaviorTree, HurtBoxManager, Parryable\n" +
                "• TagComponent, AbilitySystemComponent\n" +
                "• AttributeSet, HitReactionController",
                MessageType.None);
        }
    }
}
