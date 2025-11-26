// 文件名: TagDebuggerWindow.cs
// 适配包含 Buff 系统的 TagComponent 版本
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Reflection; // 我们需要反射来访问私有字段
using System.Collections; // 用于访问通用列表接口

public class TagDebuggerWindow : EditorWindow
{
    private List<TagComponent> allTagComponents = new List<TagComponent>();
    private Vector2 scrollPosition;

    // --- 反射字段 ---
    // 适配你当前 TagComponent 的所有私有字段
    private FieldInfo activeTagsField;
    private FieldInfo transientTagsField;
    private FieldInfo cachedTagsField;
    private FieldInfo activeBuffsField;

    [MenuItem("Tools/Tag Debugger")]
    public static void ShowWindow()
    {
        GetWindow<TagDebuggerWindow>("Tag Debugger");
    }

    private void OnEnable()
    {
        // 获取当前版本 TagComponent 的所有字段信息
        var type = typeof(TagComponent);
        activeTagsField = type.GetField("activeTags", BindingFlags.NonPublic | BindingFlags.Instance);
        transientTagsField = type.GetField("transientTags", BindingFlags.NonPublic | BindingFlags.Instance);
        cachedTagsField = type.GetField("cachedTags", BindingFlags.NonPublic | BindingFlags.Instance);
        activeBuffsField = type.GetField("activeBuffs", BindingFlags.NonPublic | BindingFlags.Instance);

        // 自动刷新
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        SceneView.duringSceneGui += OnSceneGUI;
    }

    private void OnDisable()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        SceneView.duringSceneGui -= OnSceneGUI;
    }
    
    private void OnSceneGUI(SceneView sceneView) { if (Application.isPlaying) Repaint(); }

    private void Update()
    {
        if (Application.isPlaying)
        {
            allTagComponents.Clear();
            allTagComponents.AddRange(FindObjectsOfType<TagComponent>());
            Repaint();
        }
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Tag Component Debugger", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox("This debugger works only in Play Mode.", MessageType.Info);
            return;
        }

        if (allTagComponents.Count == 0)
        {
            EditorGUILayout.HelpBox("No active TagComponents found in the scene.", MessageType.Info);
            return;
        }

        // --- 反射字段检查 ---
        if (activeTagsField == null || transientTagsField == null || cachedTagsField == null || activeBuffsField == null)
        {
            EditorGUILayout.HelpBox("Could not access TagComponent fields via reflection. Make sure the field names ('activeTags', 'transientTags', 'cachedTags', 'activeBuffs') are correct.", MessageType.Error);
            return;
        }
        
        EditorGUILayout.LabelField($"Found {allTagComponents.Count} TagComponent(s):");
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        foreach (var target in allTagComponents)
        {
            if (target == null) continue;

            EditorGUILayout.BeginVertical("box");
            EditorGUI.indentLevel++;
            EditorGUILayout.ObjectField(target.gameObject, typeof(GameObject), true);
            
            // --- 获取并显示所有数据 ---
            
            // 1. Active Tags (永久)
            var activeTags = activeTagsField.GetValue(target) as HashSet<GameplayTagSO>;
            DisplayTagList("Active Tags (Permanent)", activeTags, new Color(0.8f, 1f, 0.8f));

            EditorGUILayout.Space(5);

            // 2. Transient Tags (瞬时)
            var transientTags = transientTagsField.GetValue(target) as HashSet<GameplayTagSO>;
            DisplayTagList("Transient Tags (This Frame Only)", transientTags, new Color(1f, 1f, 0.8f));

            EditorGUILayout.Space(5);
            
            // 3. Cached Tags (缓存)
            var cachedTagsList = cachedTagsField.GetValue(target) as IList;
            DisplayCachedTagList("Cached Tags (For Combo Window)", cachedTagsList, new Color(0.8f, 0.9f, 1f));
            
            EditorGUILayout.Space(5);

            // 4. Active Buffs (新增)
            var activeBuffsList = activeBuffsField.GetValue(target) as IList;
            DisplayBuffList("Active Buffs", activeBuffsList, new Color(1f, 0.8f, 0.8f));

            EditorGUI.indentLevel--;
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(10);
        }

        EditorGUILayout.EndScrollView();
    }
    
    // --- 辅助绘图方法 ---

    private void DisplayTagList(string label, HashSet<GameplayTagSO> tags, Color color)
    {
        EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
        if (tags == null || tags.Count == 0) { EditorGUILayout.LabelField("  - None -"); return; }
        GUI.color = color;
        foreach (var tag in tags)
        {
            if (tag == null) continue;
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.ObjectField(" ", tag, typeof(GameplayTagSO), false);
            EditorGUI.EndDisabledGroup();
        }
        GUI.color = Color.white;
    }
    
    private void DisplayCachedTagList(string label, IList tags, Color color)
    {
        EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
        if (tags == null || tags.Count == 0) { EditorGUILayout.LabelField("  - None -"); return; }
        
        var cachedTagType = typeof(TagComponent).GetNestedType("CachedTag", BindingFlags.NonPublic);
        if (cachedTagType == null) return;
        var tagField = cachedTagType.GetField("Tag");
        var timestampField = cachedTagType.GetField("Timestamp");
        if (tagField == null || timestampField == null) return;

        GUI.color = color;
        foreach (var item in tags)
        {
            var tag = tagField.GetValue(item) as GameplayTagSO;
            var timestamp = (float)timestampField.GetValue(item);
            
            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.ObjectField(" ", tag, typeof(GameplayTagSO), false);
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.LabelField($" (Expires in: {(timestamp + 0.25f - Time.time):F2}s)", GUILayout.Width(120));
            EditorGUILayout.EndHorizontal();
        }
        GUI.color = Color.white;
    }
    
    // *** 新增：用于显示 Buff 列表的方法 ***
    private void DisplayBuffList(string label, IList buffs, Color color)
    {
        EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
        if (buffs == null || buffs.Count == 0) { EditorGUILayout.LabelField("  - None -"); return; }
        
        // 使用反射获取 Buff 类的内部字段
        var buffType = typeof(Buff); // 假设 Buff 类是 public
        var dataField = buffType.GetProperty("Data");
        var timeRemainingField = buffType.GetProperty("TimeRemaining");
        var stacksField = buffType.GetProperty("CurrentStacks");

        if (dataField == null || timeRemainingField == null || stacksField == null)
        {
            EditorGUILayout.HelpBox("Cannot reflect Buff class fields.", MessageType.Warning);
            return;
        }

        GUI.color = color;
        foreach (var item in buffs)
        {
            var buffData = dataField.GetValue(item) as BuffSO;
            var timeRemaining = (float)timeRemainingField.GetValue(item);
            var stacks = (int)stacksField.GetValue(item);
            
            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.ObjectField(" ", buffData, typeof(BuffSO), false);
            EditorGUI.EndDisabledGroup();
            
            string info = "";
            if (buffData.duration > 0) info += $" (Time: {timeRemaining:F1}s)";
            if (buffData.maxStacks > 1) info += $" (Stacks: {stacks})";
            
            EditorGUILayout.LabelField(info, GUILayout.Width(150));
            EditorGUILayout.EndHorizontal();
        }
        GUI.color = Color.white;
    }

    private void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingPlayMode)
        {
            allTagComponents.Clear();
        }
        Repaint();
    }
}