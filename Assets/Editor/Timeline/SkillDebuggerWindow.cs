// 文件名: SkillDebuggerWindow.cs (支持预选择目标)
// 必须放在 Editor 目录下

using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System; // For System.Object

public class SkillDebuggerWindow : EditorWindow
{
    // --- 数据存储 ---
    // 存储场景中所有潜在的监视目标
    private List<MonoBehaviour> _potentialTargets = new List<MonoBehaviour>();
    // 存储当前正在播放技能的目标及其信息
    private Dictionary<GameObject, SkillDebugInfo> _activeSkills = new Dictionary<GameObject, SkillDebugInfo>();
    
    // --- UI 状态 ---
    private GameObject _selectedTargetObject;
    private int _selectedTargetIndex = -1;
    private Vector2 _scrollPosition;
    
    // --- 对技能编辑器的引用 ---
    private SkillEditorTimelineWindow _timelineEditorInstance;

    [MenuItem("Tools/Runtime Skill Debugger")]
    public static void ShowWindow()
    {
        GetWindow<SkillDebuggerWindow>("技能调试器");
    }

    private void OnEnable()
    {
        // 订阅运行时事件
        SkillDebugManager.OnSkillFrameUpdate += HandleSkillUpdate;
        SkillDebugManager.OnSkillStop += HandleSkillStop;

        // 订阅编辑器事件
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        EditorApplication.hierarchyChanged += RefreshPotentialTargets; // 当场景层级变化时刷新列表

        // 立即刷新一次目标列表
        RefreshPotentialTargets();
    }

    private void OnDisable()
    {
        // 取消订阅，防止内存泄漏
        SkillDebugManager.OnSkillFrameUpdate -= HandleSkillUpdate;
        SkillDebugManager.OnSkillStop -= HandleSkillStop;
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        EditorApplication.hierarchyChanged -= RefreshPotentialTargets;
    }

    // 查找并更新场景中所有可监视的目标
    private void RefreshPotentialTargets()
    {
        _potentialTargets.Clear();
        // 查找所有 PlayerAttackComponent
        _potentialTargets.AddRange(FindObjectsOfType<PlayerSkillComponent>());
        // 查找所有 EnemySkillComponent
        _potentialTargets.AddRange(FindObjectsOfType<EnemySkillComponent>());
        
        // 如果当前选中的目标已不存在，则清空选择
        if (_selectedTargetObject != null && !_potentialTargets.Any(t => t.gameObject == _selectedTargetObject))
        {
            _selectedTargetObject = null;
            _selectedTargetIndex = -1;
        }
        
        Repaint();
    }

    private void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingPlayMode || state == PlayModeStateChange.EnteredEditMode)
        {
            _activeSkills.Clear();
            // 保持选中目标，但清空播放状态
            Repaint();
        }
    }

    private void HandleSkillUpdate(SkillDebugInfo info)
    {
        _activeSkills[info.SourceObject] = info;
        
        if (info.SourceObject == _selectedTargetObject)
        {
            SyncToTimelineEditor(info);
            Repaint(); // 只有当选中的目标更新时才重绘，提高性能
        }
    }

    private void HandleSkillStop(GameObject source)
    {
        if (_activeSkills.ContainsKey(source))
        {
            _activeSkills.Remove(source);
            
            if (source == _selectedTargetObject)
            {
                ClearTimelineEditor();
                Repaint(); // 只有当选中的目标停止时才重绘
            }
        }
    }
    
    private void OnGUI()
    {
        EditorGUILayout.LabelField("运行时技能监视", EditorStyles.boldLabel);

        // --- 目标选择区域 ---
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("监视目标", GUILayout.Width(70));

        string[] targetNames = _potentialTargets.Select(t => t.gameObject.name).ToArray();
        // 如果没有找到目标，显示提示
        if(targetNames.Length == 0)
        {
            EditorGUILayout.LabelField("场景中未找到技能组件");
        }
        else
        {
            int newIndex = EditorGUILayout.Popup(_selectedTargetIndex, targetNames);
            if (newIndex != _selectedTargetIndex)
            {
                _selectedTargetIndex = newIndex;
                _selectedTargetObject = _potentialTargets[_selectedTargetIndex].gameObject;
                
                // 切换目标时，检查它是否正在播放技能并立即同步
                if (_activeSkills.TryGetValue(_selectedTargetObject, out var info))
                {
                    SyncToTimelineEditor(info);
                }
                else
                {
                    ClearTimelineEditor();
                }
            }
        }

        if (GUILayout.Button("刷新", GUILayout.Width(50)))
        {
            RefreshPotentialTargets();
        }
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.Space();

        // --- 状态显示区域 ---
        if (_selectedTargetObject == null)
        {
            EditorGUILayout.HelpBox("请从上方选择一个监视目标。", MessageType.Info);
            return;
        }

        // 显示选中目标的详细信息
        if (Application.isPlaying && _activeSkills.TryGetValue(_selectedTargetObject, out var currentInfo))
        {
            // 目标正在播放技能
            EditorGUILayout.LabelField("当前状态:", "正在播放技能", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("技能资源:", currentInfo.SkillAsset != null ? currentInfo.SkillAsset.name : "N/A");
            EditorGUILayout.LabelField("当前帧:", $"{currentInfo.CurrentFrame} / {currentInfo.MaxFrame}");
            
            if (currentInfo.MaxFrame > 0)
            {
                Rect r = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight);
                EditorGUI.ProgressBar(r, currentInfo.CurrentFrame / (float)currentInfo.MaxFrame, "播放进度");
            }
        }
        else
        {
            // 目标未在播放技能
             EditorGUILayout.LabelField("当前状态:", "待机或未播放技能", EditorStyles.boldLabel);
             if(!Application.isPlaying)
             {
                 EditorGUILayout.HelpBox("请进入播放模式以查看实时数据。", MessageType.Info);
             }
        }
    }

    // --- 与技能编辑器的联动 ---
    private void SyncToTimelineEditor(SkillDebugInfo info)
    {
        if (_timelineEditorInstance == null || !_timelineEditorInstance) // 检查窗口是否被关闭
        {
            _timelineEditorInstance = GetWindow<SkillEditorTimelineWindow>(false, "技能时间轴编辑器", false);
        }

        if (_timelineEditorInstance != null)
        {
            // 你需要在 SkillEditorTimelineWindow 中添加这些公共方法
            _timelineEditorInstance.EnterDebugMode(info.SkillAsset);
            _timelineEditorInstance.SetDebugFrame(info.CurrentFrame);
        }
    }
    
    private void ClearTimelineEditor()
    {
        if (_timelineEditorInstance != null && _timelineEditorInstance)
        {
             _timelineEditorInstance.ExitDebugMode();
        }
    }
}