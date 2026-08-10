// 受击动画集编辑器 — 以 4×4 槽位网格配置 ExpandableAnimationSet 资产。
// 选中任意 ExpandableAnimationSet（如 ScriptObjects/Enemy/Animation.asset）即可看到。
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ExpandableAnimationSet))]
public class ExpandableAnimationSetEditor : Editor
{
    private const string Directions = "FLRB"; // F前 B后 L左 R右
    private const string Strengths = "LMHB";  // L轻 M中 H重 B吹飞

    private SerializedProperty _animations;
    private Vector2 _scroll;

    private void OnEnable()
    {
        _animations = serializedObject.FindProperty("animations");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        DrawHeader();
        DrawHitSlotGrid();
        DrawSpecialSlots();
        DrawToolbar();
        DrawEntries();

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawHeader()
    {
        EditorGUILayout.LabelField("受击动画集", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "命名规则：方向(F前/B后/L左/R右) × 强度(L轻/M中/H重/B吹飞) = 16 个受击动画，\n" +
            "由 HitReactionController 按攻击来源方向 + AttackData.forceType 自动匹配。\n" +
            "Air/Land 分别用于击飞滞空姿态和落地受身。",
            MessageType.Info);
        EditorGUILayout.Space(4);
    }

    /// <summary>4×4 受击槽位：显示已配置/缺失，支持就地拖入 clip 或一键补槽</summary>
    private void DrawHitSlotGrid()
    {
        EditorGUILayout.LabelField("受击动画 16 槽位", EditorStyles.boldLabel);

        using (new EditorGUILayout.VerticalScope("box"))
        {
            foreach (var d in Directions)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(d.ToString(), EditorStyles.boldLabel, GUILayout.Width(20));
                    foreach (var s in Strengths)
                    {
                        string name = $"{d}_{s}";
                        int idx = FindEntryIndex(name);
                        using (new EditorGUILayout.VerticalScope(GUILayout.Width(72)))
                        {
                            if (idx >= 0)
                            {
                                var clipProp = _animations.GetArrayElementAtIndex(idx)
                                    .FindPropertyRelative("animationClip._Clip");
                                EditorGUILayout.LabelField(name, EditorStyles.miniBoldLabel);
                                // 就地换 clip
                                EditorGUILayout.PropertyField(clipProp, GUIContent.none, GUILayout.Height(40));
                            }
                            else
                            {
                                EditorGUILayout.LabelField(name, EditorStyles.miniBoldLabel);
                                if (GUILayout.Button("+ 添加", GUILayout.Height(40)))
                                    AddEntry(name);
                            }
                        }
                    }
                }
                EditorGUILayout.Space(2);
            }
        }
        EditorGUILayout.Space(4);
    }

    /// <summary>Air / Land / Guard_Hit / Death 等特殊槽位</summary>
    private void DrawSpecialSlots()
    {
        EditorGUILayout.LabelField("特殊动画槽位", EditorStyles.boldLabel);
        using (new EditorGUILayout.VerticalScope("box"))
        {
            DrawSpecialSlot("Air", "击飞滞空姿态（击飞时播放）");
            DrawSpecialSlot("Land", "落地受身（击飞落地时播放）");
            DrawSpecialSlot("Guard_Hit", "格挡受击");
            DrawSpecialSlot("Death", "死亡");
        }
        EditorGUILayout.Space(4);
    }

    private void DrawSpecialSlot(string name, string tooltip)
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            int idx = FindEntryIndex(name);
            EditorGUILayout.LabelField(name, GUILayout.Width(90));
            if (idx >= 0)
            {
                var clipProp = _animations.GetArrayElementAtIndex(idx)
                    .FindPropertyRelative("animationClip._Clip");
                EditorGUILayout.PropertyField(clipProp, GUIContent.none);
                if (GUILayout.Button("移除", GUILayout.Width(50)))
                    _animations.DeleteArrayElementAtIndex(idx);
            }
            else
            {
                EditorGUILayout.HelpBox(tooltip, MessageType.None);
                if (GUILayout.Button("添加", GUILayout.Width(60)))
                    AddEntry(name);
            }
        }
    }

    private void DrawToolbar()
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("补齐缺失的标准槽位"))
                FillMissingStandardSlots();
            if (GUILayout.Button("添加自定义条目"))
                AddEntry("New_Animation");
            if (GUILayout.Button("按名称排序"))
                SortByName();
        }
        EditorGUILayout.Space(4);
    }

    /// <summary>所有条目的详细列表：名称 + clip + 淡入时长 + 速度 + 移除</summary>
    private void DrawEntries()
    {
        EditorGUILayout.LabelField($"全部条目（{_animations.arraySize}）", EditorStyles.boldLabel);
        _scroll = EditorGUILayout.BeginScrollView(_scroll);
        for (int i = 0; i < _animations.arraySize; i++)
        {
            var element = _animations.GetArrayElementAtIndex(i);
            var nameProp = element.FindPropertyRelative("animationName");
            var clipProp = element.FindPropertyRelative("animationClip._Clip");
            var fadeProp = element.FindPropertyRelative("animationClip._FadeDuration");
            var speedProp = element.FindPropertyRelative("animationClip._Speed");

            using (new EditorGUILayout.HorizontalScope("box"))
            {
                EditorGUILayout.PropertyField(nameProp, GUIContent.none, GUILayout.Width(110));
                EditorGUILayout.PropertyField(clipProp, GUIContent.none);
                EditorGUILayout.LabelField("淡入", GUILayout.Width(36));
                EditorGUILayout.PropertyField(fadeProp, GUIContent.none, GUILayout.Width(46));
                EditorGUILayout.LabelField("速度", GUILayout.Width(36));
                EditorGUILayout.PropertyField(speedProp, GUIContent.none, GUILayout.Width(46));
                if (GUILayout.Button("×", GUILayout.Width(22)))
                {
                    _animations.DeleteArrayElementAtIndex(i);
                    i--;
                }
            }
        }
        EditorGUILayout.EndScrollView();
    }

    private int FindEntryIndex(string name)
    {
        if (_animations == null) return -1;
        for (int i = 0; i < _animations.arraySize; i++)
        {
            var n = _animations.GetArrayElementAtIndex(i).FindPropertyRelative("animationName").stringValue;
            if (n == name) return i;
        }
        return -1;
    }

    private void AddEntry(string name)
    {
        if (FindEntryIndex(name) >= 0)
        {
            Debug.LogWarning($"条目 '{name}' 已存在。");
            return;
        }
        _animations.arraySize++;
        var element = _animations.GetArrayElementAtIndex(_animations.arraySize - 1);
        element.FindPropertyRelative("animationName").stringValue = name;
        element.FindPropertyRelative("animationClip._FadeDuration").floatValue = 0.1f;
        element.FindPropertyRelative("animationClip._Speed").floatValue = 1f;
        serializedObject.ApplyModifiedProperties();
    }

    private void FillMissingStandardSlots()
    {
        var missing = new List<string>();
        foreach (var d in Directions)
            foreach (var s in Strengths)
                if (FindEntryIndex($"{d}_{s}") < 0)
                    missing.Add($"{d}_{s}");
        foreach (var special in new[] { "Air", "Land", "Guard_Hit", "Death" })
            if (FindEntryIndex(special) < 0)
                missing.Add(special);

        if (missing.Count == 0)
        {
            Debug.Log("所有标准槽位都已配置。");
            return;
        }

        foreach (var name in missing)
        {
            _animations.arraySize++;
            var element = _animations.GetArrayElementAtIndex(_animations.arraySize - 1);
            element.FindPropertyRelative("animationName").stringValue = name;
            element.FindPropertyRelative("animationClip._FadeDuration").floatValue = 0.1f;
            element.FindPropertyRelative("animationClip._Speed").floatValue = 1f;
        }
        serializedObject.ApplyModifiedProperties();
        Debug.Log($"已补齐 {missing.Count} 个缺失槽位：{string.Join(", ", missing)}");
    }

    private void SortByName()
    {
        // 通过 SerializedProperty 排序比较麻烦，走对象引用直接排
        var set = (ExpandableAnimationSet)target;
        if (set.animations == null) return;

        var list = set.animations;
        list.Sort((a, b) => string.Compare(a.animationName, b.animationName, System.StringComparison.Ordinal));
        EditorUtility.SetDirty(set);
        serializedObject.Update();
    }
}
