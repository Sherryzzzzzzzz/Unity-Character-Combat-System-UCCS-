using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Reflection; // 我们需要反射来访问私有字段

public class TagDebuggerWindow : EditorWindow
{
    private TagComponent targetTagComponent;
    private Vector2 scrollPosition;

    // --- 用于通过反射访问 TagComponent 的私有字段 ---
    private FieldInfo activeTagsField;
    private FieldInfo transientTagsField;
    private FieldInfo cachedTagsField; // 额外增加：显示缓存的 Tag

    // 通过菜单栏打开窗口
    [MenuItem("Tools/Tag Debugger")]
    public static void ShowWindow()
    {
        // 获取或创建一个新的窗口实例
        GetWindow<TagDebuggerWindow>("Tag Debugger");
    }

    private void OnEnable()
    {
        // 当窗口被打开或代码重编译时，尝试获取字段信息
        // 使用反射是因为 TagComponent 中的字段是 private 的
        var type = typeof(TagComponent);
        activeTagsField = type.GetField("activeTags", BindingFlags.NonPublic | BindingFlags.Instance);
        transientTagsField = type.GetField("transientTags", BindingFlags.NonPublic | BindingFlags.Instance);
        
        // 额外增加：获取缓存 Tag 的字段信息
        cachedTagsField = type.GetField("cachedTags", BindingFlags.NonPublic | BindingFlags.Instance);

        // 设置窗口在播放模式下自动更新
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    private void OnDisable()
    {
        // 清理委托，防止内存泄漏
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
    }

    // 当播放模式改变时，确保我们停止或开始更新
    private void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingPlayMode)
        {
            // 退出播放模式时，清空目标
            targetTagComponent = null;
        }
        Repaint(); // 刷新窗口UI
    }

    private void Update()
    {
        // 只在播放模式下才需要实时更新
        if (Application.isPlaying)
        {
            // 如果没有目标，尝试自动查找第一个
            if (targetTagComponent == null)
            {
                targetTagComponent = FindObjectOfType<TagComponent>();
            }
            
            // 强制重绘窗口来显示最新的 Tag 状态
            Repaint();
        }
    }

    private void OnGUI()
    {
        // 标题
        EditorGUILayout.LabelField("Tag Component Debugger", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        // --- 目标对象选择 ---
        EditorGUILayout.LabelField("Target GameObject", EditorStyles.label);
        targetTagComponent = (TagComponent)EditorGUILayout.ObjectField(targetTagComponent, typeof(TagComponent), true);
        
        // 如果没有手动指定目标，提供一个按钮来自动查找
        if (targetTagComponent == null)
        {
            if (GUILayout.Button("Find First TagComponent in Scene"))
            {
                targetTagComponent = FindObjectOfType<TagComponent>();
            }
            EditorGUILayout.HelpBox("No target selected. Drag a GameObject with a TagComponent here, or click the button to find one.", MessageType.Info);
            return; // 没有目标，后续UI不显示
        }
        
        EditorGUILayout.Space();
        EditorGUILayout.HelpBox("Window updates in real-time during Play Mode.", MessageType.None);
        EditorGUILayout.Space();

        // 开始可滚动区域
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        // --- 反射字段检查 ---
        if (activeTagsField == null || transientTagsField == null || cachedTagsField == null)
        {
            EditorGUILayout.HelpBox("Could not access TagComponent fields via reflection. Make sure the field names ('activeTags', 'transientTags', 'cachedTags') are correct.", MessageType.Error);
            EditorGUILayout.EndScrollView();
            return;
        }

        // --- 获取并显示 Tag 数据 ---
        // 我们需要告诉 Unity，接下来的代码可能会改变 GUI 的状态，即使目标对象没有被标记为 Dirty
        if (targetTagComponent != null) EditorGUI.BeginDisabledGroup(true); // 让显示的字段不可编辑

        // 1. 显示永久性 Tag (Active Tags)
        var activeTags = activeTagsField.GetValue(targetTagComponent) as HashSet<GameplayTagSO>;
        DisplayTagList("Active Tags (Permanent)", activeTags, new Color(0.8f, 1f, 0.8f)); // 绿色

        EditorGUILayout.Space(10);

        // 2. 显示瞬时 Tag (Transient Tags)
        var transientTags = transientTagsField.GetValue(targetTagComponent) as HashSet<GameplayTagSO>;
        DisplayTagList("Transient Tags (This Frame Only)", transientTags, new Color(1f, 1f, 0.8f)); // 黄色

        EditorGUILayout.Space(10);
        
        // 3. 额外增加：显示缓存的 Tag
        // 注意：cachedTags 是 List<CachedTag>，我们需要特殊处理
        var cachedTagsList = cachedTagsField.GetValue(targetTagComponent) as System.Collections.IList;
        DisplayCachedTagList("Cached Tags (For Combo Window)", cachedTagsList, new Color(0.8f, 0.9f, 1f)); // 蓝色

        if (targetTagComponent != null) EditorGUI.EndDisabledGroup();

        EditorGUILayout.EndScrollView();
    }

    // 辅助方法，用于绘制一个 Tag 列表
    private void DisplayTagList(string label, HashSet<GameplayTagSO> tags, Color color)
    {
        EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
        
        if (tags == null || tags.Count == 0)
        {
            EditorGUILayout.LabelField("  - None -");
            return;
        }

        GUI.color = color;
        foreach (var tag in tags)
        {
            // 使用一个 ObjectField 来显示 Tag，这样我们甚至可以点击它来 Ping 项目中的资产
            EditorGUILayout.ObjectField(" ", tag, typeof(GameplayTagSO), false);
        }
        GUI.color = Color.white; // 恢复默认颜色
    }
    
    // 辅助方法，用于绘制缓存的 Tag 列表
    private void DisplayCachedTagList(string label, System.Collections.IList tags, Color color)
    {
        EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
        
        if (tags == null || tags.Count == 0)
        {
            EditorGUILayout.LabelField("  - None -");
            return;
        }
        
        // 获取 CachedTag 类型的字段信息
        var cachedTagType = typeof(TagComponent).GetNestedType("CachedTag", BindingFlags.NonPublic);
        var tagField = cachedTagType.GetField("Tag");
        var timestampField = cachedTagType.GetField("Timestamp");

        GUI.color = color;
        foreach (var item in tags)
        {
            var tag = tagField.GetValue(item) as GameplayTagSO;
            var timestamp = (float)timestampField.GetValue(item);
            
            // 显示 Tag 资产和一个额外的时间信息
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.ObjectField(" ", tag, typeof(GameplayTagSO), false);
            EditorGUILayout.LabelField($" (Expires in: {(timestamp + 0.25f - Time.time):F2}s)", GUILayout.Width(120));
            EditorGUILayout.EndHorizontal();
        }
        GUI.color = Color.white;
    }
}