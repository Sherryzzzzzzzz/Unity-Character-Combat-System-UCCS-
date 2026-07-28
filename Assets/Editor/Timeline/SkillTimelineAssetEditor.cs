using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(SkillTimelineAsset))]
public class SkillTimelineAssetEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space(8);

        if (GUILayout.Button("打开技能编辑器", GUILayout.Height(32)))
        {
            var asset = (SkillTimelineAsset)target;
            var previewObj = FindPreviewObject();
            SkillEditorTimelineWindow.OpenSkill(asset, previewObj);
        }
    }

    static GameObject FindPreviewObject()
    {
        // 在场景中找第一个同时有 AnimancerComponent + CharacterController 的物体
        var all = FindObjectsByType<Animancer.AnimancerComponent>(FindObjectsSortMode.None);
        foreach (var a in all)
        {
            var cc = a.GetComponent<CharacterController>();
            if (cc != null) return a.gameObject;
        }
        // fallback: 只找 AnimancerComponent
        foreach (var a in all) return a.gameObject;
        return null;
    }
}
