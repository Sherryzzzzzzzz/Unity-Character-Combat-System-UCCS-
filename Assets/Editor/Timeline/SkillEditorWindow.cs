using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor.UIElements;

#region 引导程序和数据类 (无需修改)
[InitializeOnLoad]
public static class TimelineEventFactoryBootstrap
{
    static TimelineEventFactoryBootstrap()
    {
        EventFactoryRegistry.Register(new AttackEventFactory());
        EventFactoryRegistry.Register(new HitBoxEventFactory());
        EventFactoryRegistry.Register(new ComboEventFactory());
        EventFactoryRegistry.Register(new EffectEventFactory()); 
        EventFactoryRegistry.Register(new SoundEventFactory());
        EventFactoryRegistry.Register(new BuffEventFactory());
        EventFactoryRegistry.Register(new LoopEventFactory());
        EventFactoryRegistry.Register(new CancelEventFactory());
        EventFactoryRegistry.Register(new GameplayEffectEventFactory());
        EventFactoryRegistry.Register(new GameplayAbilityEventFactory());
        EventFactoryRegistry.Register(new TargetSearchEventFactory());
        EventFactoryRegistry.Register(new CueEventFactory());
        EventFactoryRegistry.Register(new CooldownEventFactory());
    }
}
#endregion

public class SkillEditorTimelineWindow : EditorWindow
{
    [MenuItem("Tools/Skill Editor Timeline")]
    public static void ShowWindow()
    {
        var wnd = GetWindow<SkillEditorTimelineWindow>();
        wnd.titleContent = new GUIContent("技能时间轴编辑器");
        wnd.minSize = new Vector2(900, 400);
    }

    private const float TRACK_HEADER_WIDTH = 220f;
    private const float TRACK_HEIGHT = 28f;
    private const float RULER_HEIGHT = 24f;
    
    private const string CLIP_FIELD_NAME = "animation-clip-field";


    private AnimationClip _clip;
    private GameObject _previewObj;
    private SkillTimelineAsset _asset;
    private List<TimelineData> _timelines = new List<TimelineData>();
    private int _totalFrames;
    private int _currentFrame;
    private bool _isPlaying;
    private double _playTimeSec;
    private double _lastEditorTime;
    private float _playbackSpeed = 1f;

    private VisualElement _root;
    private Toolbar _toolbar;
    private TwoPaneSplitView _splitView;
    private VisualElement _trackHeaders, _timelineContent, _ruler, _tracksRoot, _playhead;
    private VisualElement _eventInspector;
    private VisualElement _inspectorContent;
    private ScrollView _trackContentScrollView;
    private Label _frameLabel;
    private AnimationClip _defaultPoseClip;
    
    private TimelineEventBase _selectedEvent;
    private VisualElement _selectedEventClip;
    private List<TimelineEventBase> _selectedEvents = new List<TimelineEventBase>();
    private List<TimelineEventBase> _clipboard = new List<TimelineEventBase>();

    public int GetTotalFrames() => _totalFrames;
    public bool HasClip() => _clip != null;

    public void RecordUndoForDrag(string description)
    {
        if (_asset != null) Undo.RecordObject(_asset, description);
    }
    
    //运行时动态查看
    private bool _isInDebugMode = false;
    private SkillTimelineAsset _lastDebugAsset = null;
    
    private AudioSource _previewAudioSource;

    private SkillEditorSceneOverlay _sceneOverlay = new SkillEditorSceneOverlay();
    
    public void CreateGUI()
    {
        _root = rootVisualElement;
        
        string ussPath = "Assets/Editor/SkillEditorStyles.uss";
        var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(ussPath);
        if (styleSheet != null) _root.styleSheets.Add(styleSheet);

        CreateToolbar();
        CreateMainView();

        _root.RegisterCallback<KeyDownEvent>(OnKeyDown);
        
        EditorApplication.update -= OnEditorUpdate;
        EditorApplication.update += OnEditorUpdate;
        SceneView.duringSceneGui -= OnSceneGUI;
        SceneView.duringSceneGui += OnSceneGUI;
    }
    
    private void RestoreToDefaultPose()
    {
        // 1. 确保退出当前的动画模式，这会清除所有动画修改
        if (AnimationMode.InAnimationMode())
        {
            AnimationMode.StopAnimationMode();
        }

        // 2. 如果有预览对象和默认姿态动画，则手动采样它
        if (_previewObj != null && _defaultPoseClip != null)
        {
            // 重新进入一次动画模式来应用我们的采样
            AnimationMode.StartAnimationMode();
            AnimationMode.SampleAnimationClip(_previewObj, _defaultPoseClip, 0); // 采样第 0 秒 (第一帧)
            AnimationMode.StopAnimationMode(); // 立即退出，将这个姿态“固化”在场景中
        }
    }
    
    private void OnEnable()
    {
        // 【新增】初始化预览 AudioSource
        if (_previewAudioSource == null)
        {
            // 创建一个临时的、隐藏的游戏对象来挂载 AudioSource
            GameObject previewer = new GameObject("Skill Editor Audio Previewer");
            previewer.hideFlags = HideFlags.HideAndDontSave; // 确保它不会被保存到场景中
            _previewAudioSource = previewer.AddComponent<AudioSource>();
        }
    }
    
    private void OnDestroy()
    {
        if (_previewAudioSource != null)
        {
            DestroyImmediate(_previewAudioSource.gameObject);
            _previewAudioSource = null;
        }
        
        RestoreToDefaultPose();
        EditorApplication.update -= OnEditorUpdate;
        SceneView.duringSceneGui -= OnSceneGUI;
        if (AnimationMode.InAnimationMode()) AnimationMode.StopAnimationMode();
    }

    #region UI创建
    private void CreateToolbar()
    {
        _toolbar = new Toolbar();
        
        var assetField = new ObjectField("技能资源") { objectType = typeof(SkillTimelineAsset), allowSceneObjects = false, style = { width = 200 } };
        assetField.RegisterValueChangedCallback(evt => LoadAsset(evt.newValue as SkillTimelineAsset));
        _toolbar.Add(assetField);
        
        _toolbar.Add(new ToolbarButton(SaveAsset) { text = "保存" });
        _toolbar.Add(new ToolbarSpacer());

        var clipField = new ObjectField("动画片段") 
        { 
            name = CLIP_FIELD_NAME,
            objectType = typeof(AnimationClip), 
            allowSceneObjects = false, 
            style = { width = 200 } 
        };
        clipField.RegisterValueChangedCallback(evt => SetAnimationClip(evt.newValue as AnimationClip));
        _toolbar.Add(clipField);

        var objField = new ObjectField("预览对象") { objectType = typeof(GameObject), allowSceneObjects = true, style = { width = 200 } };
        objField.RegisterValueChangedCallback(evt => _previewObj = evt.newValue as GameObject);
        _toolbar.Add(objField);
        
        var defaultPoseField = new ObjectField("默认姿态") 
        {
            objectType = typeof(AnimationClip), 
            allowSceneObjects = false,
            tooltip = "当停止预览时，角色恢复到的动画姿态（如 Idle）"
        };
        defaultPoseField.RegisterValueChangedCallback(evt => _defaultPoseClip = evt.newValue as AnimationClip);
        _toolbar.Add(defaultPoseField);

        _toolbar.Add(new ToolbarSpacer());

        var playBtn = new ToolbarButton(() => { if (_isPlaying) StopPlayback(); else StartPlayback(); }) { tooltip = "播放/暂停" };
        playBtn.Add(new Image { image = EditorGUIUtility.IconContent("d_PlayButton").image, scaleMode = ScaleMode.ScaleToFit });
        _toolbar.Add(playBtn);
        
        var speedField = new FloatField("速度") { value = 1f, style = { width = 80 } };
        speedField.RegisterValueChangedCallback(evt => _playbackSpeed = Mathf.Max(0f, evt.newValue));
        _toolbar.Add(speedField);
        
        _frameLabel = new Label("时间: 0.00s | 帧: 0 / 0") { style = { unityTextAlign = TextAnchor.MiddleLeft, paddingLeft = 8, minWidth = 180 }};
        _toolbar.Add(_frameLabel);

        _toolbar.Add(new ToolbarSpacer { flex = true });

        var sceneOverlayToggle = new ToolbarToggle { text = "场景预览", value = true, tooltip = "在SceneView中显示事件范围可视化" };
        sceneOverlayToggle.RegisterValueChangedCallback(e => _sceneOverlay.Enabled = e.newValue);
        _toolbar.Add(sceneOverlayToggle);

        _toolbar.Add(new ToolbarButton(SaveSelectionAsTemplate) { text = "保存模板", tooltip = "将选中事件保存为可复用模板" });
        _toolbar.Add(new ToolbarButton(ShowLoadTemplateMenu) { text = "加载模板", tooltip = "从模板加载事件到当前轨道" });

        var typeField = new EnumField(TimelineEventType.Attack) { style = { width = 100 }};
        var addTrackBtn = new Button(() => AddTrack((TimelineEventType)typeField.value, $"新轨道 ({typeField.value})")) { text = "添加轨道" };
        
        _toolbar.Add(typeField); 
        _toolbar.Add(addTrackBtn);
        _root.Add(_toolbar);
    }
    
    private void CreateMainView()
    {
        var mainContainer = new VisualElement { style = { flexGrow = 1, flexDirection = FlexDirection.Row } };
        _splitView = new TwoPaneSplitView(0, TRACK_HEADER_WIDTH, TwoPaneSplitViewOrientation.Horizontal) { style = { flexGrow = 1 }};
        
        var leftPane = new VisualElement();
        _trackHeaders = new VisualElement { style = { position = Position.Relative }};
        leftPane.Add(new VisualElement { style = { height = RULER_HEIGHT }});
        leftPane.Add(_trackHeaders);
        _splitView.Add(leftPane);

        _timelineContent = new VisualElement { style = { flexGrow = 1 }};
        _timelineContent.focusable = true;
        
        _ruler = new VisualElement { style = { height = RULER_HEIGHT }};
        _ruler.RegisterCallback<MouseDownEvent>(evt => JumpToFrame(ScreenPosToFrame(evt.mousePosition.x)));
        _ruler.RegisterCallback<GeometryChangedEvent>(evt => DrawRulerTicks());

        _trackContentScrollView = new ScrollView(ScrollViewMode.Vertical) { style = { flexGrow = 1 } };
        _trackContentScrollView.verticalScroller.valueChanged += v => _trackHeaders.style.top = -v;
        _tracksRoot = new VisualElement { name = "tracks-root", style = { position = Position.Relative }};
        _tracksRoot.AddManipulator(new BoxSelectionManipulator(this));
        _trackContentScrollView.Add(_tracksRoot);

        _timelineContent.Add(_ruler);
        _timelineContent.Add(_trackContentScrollView);
        _splitView.Add(_timelineContent);
        
        CreatePlayhead();
        
        _eventInspector = new VisualElement();
        _eventInspector.style.width = 300;
        _eventInspector.style.borderLeftWidth = 1;
        _eventInspector.style.borderLeftColor = new Color(0.1f, 0.1f, 0.1f);
        _eventInspector.style.backgroundColor = new Color(0.21f, 0.21f, 0.21f);

        var inspectorScrollView = new ScrollView(ScrollViewMode.Vertical);
        inspectorScrollView.style.flexGrow = 1;
        inspectorScrollView.style.paddingTop = 8;
        inspectorScrollView.style.paddingBottom = 8;
        inspectorScrollView.style.paddingLeft = 8;
        inspectorScrollView.style.paddingRight = 8;
        _eventInspector.Add(inspectorScrollView);
        _inspectorContent = inspectorScrollView;

        mainContainer.Add(_splitView);
        mainContainer.Add(_eventInspector);
        _root.Add(mainContainer);
    }
    
    private void CreatePlayhead()
    {
        _playhead = new VisualElement { name = "playhead", pickingMode = PickingMode.Ignore, style = {
            position = Position.Absolute, top = 0, bottom = 0, width = 2
        }};
        var line = new VisualElement { style = { flexGrow = 1, backgroundColor = new Color(0.9f, 0.25f, 0.25f) }};
        var handle = new VisualElement { name = "playhead-handle", style = {
            width = 14, height = 14, backgroundColor = new Color(0.9f, 0.25f, 0.25f),
            borderTopLeftRadius = 7, borderTopRightRadius = 7, borderBottomLeftRadius = 7, borderBottomRightRadius = 7,
            position = Position.Absolute, top = (RULER_HEIGHT - 14) / 2
        }};
        
        _ruler.Add(handle);
        _playhead.Add(line);
        _tracksRoot.Add(_playhead);
        
        handle.AddManipulator(new Dragger(pos => JumpToFrame(ScreenPosToFrame(pos.x, _ruler))));
    }
    #endregion

    #region 数据处理 (加载/保存)
    private void LoadAsset(SkillTimelineAsset asset)
    {
        if (asset == _asset) return;
        _asset = asset;
        
        SelectEvent(null, null);
        
        if (_asset != null)
        {
            _timelines = _asset.tracks.Select(t => new TimelineData(t.type, t.name) {
                events = t.events.Select(e => e.Clone()).ToList() 
            }).ToList();
            SetAnimationClip(_asset.animationClip);
            _toolbar.Q<ObjectField>(CLIP_FIELD_NAME).SetValueWithoutNotify(_asset.animationClip);
        }
        else
        {
            _timelines.Clear();
            SetAnimationClip(null);
        }
    }
    
    private void SaveAsset()
    {
        // --- 步骤 1: 确保我们有一个可以保存的资产文件 ---
        if (_asset == null) 
        {
            // 弹出一个“另存为”窗口
            string path = EditorUtility.SaveFilePanelInProject("保存新的技能资源", "NewSkillAsset", "asset", "请选择要保存技能资源的位置");

            // 如果用户点击了“取消”或者关闭了窗口，路径会为空
            if (string.IsNullOrEmpty(path))
            {
                Debug.Log("保存操作已取消。");
                return; // 立即退出，不做任何事
            }
            
            // 创建一个新的 SkillTimelineAsset 实例并保存在磁盘上
            _asset = CreateInstance<SkillTimelineAsset>();
            AssetDatabase.CreateAsset(_asset, path);
        }

        // --- 步骤 2: 将编辑器中的数据，安全地写入到资产对象中 ---
        
        // a. 保存动画片段
        _asset.animationClip = _clip;

        // b. *** 核心修复：安全地保存所有时间轴轨道的数据 ***
        //    我们在这里检查每个轨道的 events 列表是否为 null
        _asset.tracks = _timelines.Select(timelineUIObject => 
        {
            // timelineUIObject 就是 _timelines 列表中的每一个元素 't'

            // 防御性检查：如果轨道中的事件列表是 null...
            if (timelineUIObject.events == null)
            {
                // ...就创建一个新的空列表来代替它，而不是使用 null。
                // 这样可以防止在访问 t.events 时发生 NullReferenceException。
                Debug.LogWarning($"在保存时，时间轴轨道 '{timelineUIObject.name}' 的事件列表 (events) 为空。已自动初始化为空列表。");
                timelineUIObject.events = new List<TimelineEventBase>();
            }
            
            // 现在可以安全地创建 TimelineTrackData 了
            return new TimelineTrackData 
            { 
                name = timelineUIObject.name, 
                type = timelineUIObject.type, 
                events = timelineUIObject.events // 这里的 events 保证了绝不为 null
            };

        }).ToList(); // 将所有转换后的 TimelineTrackData 收集到一个新的列表中

        // --- 步骤 3: 强制将更改写入磁盘并刷新编辑器 ---
        
        // 标记资产为“脏”，这样Unity知道它有未保存的更改
        EditorUtility.SetDirty(_asset);
        
        // 将所有内存中的资产更改写入到项目文件中
        AssetDatabase.SaveAssets();
        
        // 刷新资源数据库，确保其他窗口能看到最新的更改
        AssetDatabase.Refresh();
        
        Debug.Log($"资源 '{_asset.name}' 已成功保存到路径: {AssetDatabase.GetAssetPath(_asset)}");
        
        // 更新工具栏中的 ObjectField 以显示当前保存的资产
        if (_toolbar != null)
        {
            var objectField = _toolbar.Q<ObjectField>("技能资源");
            if (objectField != null)
            {
                objectField.SetValueWithoutNotify(_asset);
            }
        }
    }

    private void SetAnimationClip(AnimationClip clip)
    {
        _clip = clip;
        _totalFrames = _clip != null ? Mathf.Max(1, Mathf.CeilToInt(_clip.length * _clip.frameRate)) : 0;
        
        if (_toolbar != null)
            _toolbar.Q<ObjectField>(CLIP_FIELD_NAME)?.SetValueWithoutNotify(_clip);
        
        RebuildAllTracksUI();
        JumpToFrame(0);
    }
    #endregion

    #region 轨道和事件UI
    private void AddTrack(TimelineEventType type, string name)
    {
        var data = new TimelineData(type, name);
        _timelines.Add(data);
        CreateAndAddTrackUI(data, _timelines.Count - 1);
    }
    
    private void RebuildAllTracksUI()
    {
        if (_trackHeaders == null || _tracksRoot == null) return;
        _trackHeaders.Clear();
        _tracksRoot.Clear();
        if (_playhead != null) _tracksRoot.Add(_playhead);

        for (int i = 0; i < _timelines.Count; i++) { CreateAndAddTrackUI(_timelines[i], i); }

        _tracksRoot.schedule.Execute(() => {
            foreach (var timeline in _timelines)
            {
                RefreshTrackContentUI(timeline);
            }
            DrawRulerTicks();
            UpdatePlayheadPosition();
        }).StartingIn(10);
    }

    private void CreateAndAddTrackUI(TimelineData data, int index)
    {
        var header = new VisualElement { style = {
            height = TRACK_HEIGHT, 
            flexDirection = FlexDirection.Row, // 主容器是行布局
            alignItems = Align.Center,
            paddingLeft = 8, 
            backgroundColor = new Color(0.24f, 0.24f, 0.24f),
            borderBottomWidth = 1, 
            borderBottomColor = new Color(0.15f, 0.15f, 0.15f)
        }};

        // 1. 创建左侧容器，它将占据所有可用空间
        var leftContainer = new VisualElement { style = {
            flexDirection = FlexDirection.Row,
            alignItems = Align.Center,
            flexGrow = 1, // 让这个容器增长
            minWidth = 50 // 给一个最小宽度，防止被过度压缩
        }};
        header.Add(leftContainer);

        // 2. 将图标和文本框放入左侧容器
        header.Add(new Image { 
            scaleMode = ScaleMode.ScaleToFit, 
            style = { width = 16, height = 16 }
        });
        
        var titleField = new TextField { 
            value = data.name, 
            style = { 
                marginLeft = 4,
                flexGrow = 1 // 让文本框在左侧容器内自由增长
            } 
        };
        titleField.RegisterValueChangedCallback(evt => data.name = evt.newValue);
        var textInput = titleField.Q(TextField.textInputUssName);
        textInput.style.backgroundColor = Color.clear;
        textInput.style.borderTopWidth = 0; textInput.style.borderBottomWidth = 0; textInput.style.borderLeftWidth = 0; textInput.style.borderRightWidth = 0;
        leftContainer.Add(titleField);
        
        // 3. 将删除按钮直接添加到主容器 header 中
        var delBtn = new Button(() => {
            if (EditorUtility.DisplayDialog("删除轨道", $"确定要删除轨道 '{data.name}' 吗？", "确定", "取消"))
            {
                if (_timelines.Contains(data))
                {
                    _timelines.Remove(data);
                    RebuildAllTracksUI();
                }
            }
        }) { 
            text = "X", 
            style = { 
                width = 20, 
                height = 20, 
                marginRight = 4,
                flexShrink = 0 // 确保按钮不会被压缩
            }
        };
        header.Add(delBtn);
        
        _trackHeaders.Add(header);
        
        var content = new VisualElement { style = { height = TRACK_HEIGHT } };
        content.AddToClassList(index % 2 == 0 ? "track-content--even" : "track-content--odd");
        data.trackRow = content;
        content.AddManipulator(new ContextualMenuManipulator(evt => {
            evt.menu.AppendAction("添加事件", a => {
                if (_clip == null) { Debug.LogWarning("请先指定一个动画片段。"); return; }
                int frame = ScreenPosToFrame(a.eventInfo.mousePosition.x, content);
                var newEvent = CreateDefaultEventForTrack(data.type, frame);
                data.AddEvent(newEvent);
                RefreshTrackContentUI(data); 
                SelectEvent(newEvent, null); 
            });
        }));
        _tracksRoot.Add(content);
    }
    
    public void RefreshTrackContentUI(TimelineData data)
    {
        var row = data.trackRow;
        if (row == null) return;
        row.Clear();
        if (_clip == null || _totalFrames <= 0) return;
        
        if (row.resolvedStyle.width <= 1)
        {
            row.schedule.Execute(() => RefreshTrackContentUI(data)).StartingIn(10);
            return;
        }
        
        foreach(var evt in data.events)
        {
            var clipElement = CreateEventClipUI(evt, data);
            row.Add(clipElement);
            if (_selectedEvent == evt)
            {
                _selectedEventClip = clipElement;
                _selectedEventClip.AddToClassList("event-clip--selected");
            }
        }
    }
    
    private VisualElement CreateEventClipUI(TimelineEventBase evt, TimelineData track)
    {
        if (_clip == null || _totalFrames <= 0 || track.trackRow.resolvedStyle.width <= 1) return new VisualElement();
        
        float pixelsPerFrame = track.trackRow.resolvedStyle.width / _totalFrames;
        float startX = evt.StartFrame * pixelsPerFrame;
        float endX = evt.EndFrame * pixelsPerFrame;

        var clipContainer = new VisualElement();
        clipContainer.userData = evt;
        clipContainer.AddToClassList("event-clip");
        clipContainer.style.position = Position.Absolute;
        clipContainer.style.left = startX;
        clipContainer.style.width = Mathf.Max(pixelsPerFrame > 0 ? pixelsPerFrame : 2, endX - startX);
        clipContainer.style.height = TRACK_HEIGHT - 6;
        clipContainer.style.top = 3;
        
        clipContainer.AddManipulator(new EventClipManipulator(this, track, evt));
        
        var color = GetColorByType(evt.Type);
        clipContainer.style.backgroundColor = new Color(color.r, color.g, color.b, 0.8f);

        var header = new VisualElement { name = "header", pickingMode = PickingMode.Position };
        header.AddToClassList("event-clip__header");
        header.style.backgroundColor = new Color(color.r, color.g, color.b, 1f);
        
        var label = new Label(evt.GetSummary()) { style = { paddingLeft = 6, color = Color.white }, pickingMode = PickingMode.Ignore };

        clipContainer.Add(header);
        header.Add(label);
        
        clipContainer.AddManipulator(new ContextualMenuManipulator(menuEvt => menuEvt.menu.AppendAction("删除事件", a => {
            track.events.Remove(evt);
            if (_selectedEvent == evt) SelectEvent(null, null);
            RefreshTrackContentUI(track);
        })));
        
        return clipContainer;
    }
    
    public void SelectEvent(TimelineEventBase evt, VisualElement clipElement, bool additive = false)
    {
        // 【兼容性修复】使用 Blur() 方法主动让当前焦点元素失焦
        if (_root.focusController != null && _root.focusController.focusedElement != null)
        {
            (_root.focusController.focusedElement as VisualElement)?.Blur();
        }

        if (additive && evt != null)
        {
            // Shift+Click: toggle in multi-select list
            if (_selectedEvents.Contains(evt))
                _selectedEvents.Remove(evt);
            else
                _selectedEvents.Add(evt);

            _selectedEvent = _selectedEvents.Count > 0 ? _selectedEvents[_selectedEvents.Count - 1] : null;

            // Update visual selection on all tracks
            foreach (var timeline in _timelines)
            {
                if (timeline.trackRow == null) continue;
                foreach (var child in timeline.trackRow.Children())
                {
                    if (child.userData is TimelineEventBase childEvt)
                    {
                        if (_selectedEvents.Contains(childEvt))
                            child.AddToClassList("event-clip--selected");
                        else
                            child.RemoveFromClassList("event-clip--selected");
                    }
                }
            }

            if (_selectedEvents.Count > 1)
            {
                ShowMultiSelectionInspector();
            }
            else if (_selectedEvents.Count == 1)
            {
                OpenEventEditor(_selectedEvents[0]);
            }
            else
            {
                OpenEventEditor(null);
            }

            _timelineContent?.schedule.Execute(() => _timelineContent.Focus());
            return;
        }

        // Single select — clear multi-select
        _selectedEvents.Clear();

        if (_selectedEvent == evt)
        {
            _timelineContent?.Focus();
            return;
        }

        _selectedEventClip?.RemoveFromClassList("event-clip--selected");
        _selectedEvent = evt;

        if (evt != null) {
            _selectedEvents.Add(evt);
            _selectedEventClip = clipElement ?? _timelines.SelectMany(t => t.trackRow.Children())
                .FirstOrDefault(c => c.userData == evt);
            _timelineContent?.schedule.Execute(() => _timelineContent.Focus());
        } else {
            _selectedEventClip = null;
        }

        _selectedEventClip?.AddToClassList("event-clip--selected");
        OpenEventEditor(evt);
    }

    private void ShowMultiSelectionInspector()
    {
        if (_inspectorContent == null) return;
        _inspectorContent.Clear();
        _inspectorContent.userData = null;

        _inspectorContent.Add(new Label($"已选中 {_selectedEvents.Count} 个事件")
        {
            style = { unityFontStyleAndWeight = FontStyle.Bold, marginBottom = 8 }
        });

        var deleteBtn = new Button(() => DeleteSelectedEvents()) { text = "批量删除选中事件" };
        deleteBtn.style.backgroundColor = new Color(0.8f, 0.2f, 0.2f);
        _inspectorContent.Add(deleteBtn);
    }

    private void DeleteSelectedEvents()
    {
        if (_selectedEvents.Count == 0) return;
        if (_asset != null) Undo.RecordObject(_asset, "批量删除事件");

        var affectedTracks = new HashSet<TimelineData>();
        foreach (var evt in _selectedEvents)
        {
            var track = _timelines.FirstOrDefault(t => t.events.Contains(evt));
            if (track != null)
            {
                track.events.Remove(evt);
                affectedTracks.Add(track);
            }
        }
        _selectedEvents.Clear();
        _selectedEvent = null;
        _selectedEventClip = null;
        foreach (var track in affectedTracks) RefreshTrackContentUI(track);
        OpenEventEditor(null);
    }

    public void BoxSelectEvents(Rect selectionRect, VisualElement relativeTo)
    {
        _selectedEvents.Clear();
        _selectedEvent = null;

        foreach (var timeline in _timelines)
        {
            if (timeline.trackRow == null) continue;
            foreach (var child in timeline.trackRow.Children())
            {
                if (child.userData is TimelineEventBase evt)
                {
                    // Convert child bounds to relativeTo's local space
                    var childWorldBound = child.worldBound;
                    var relativeWorldBound = relativeTo.worldBound;
                    Rect childLocalRect = new Rect(
                        childWorldBound.x - relativeWorldBound.x,
                        childWorldBound.y - relativeWorldBound.y,
                        childWorldBound.width,
                        childWorldBound.height
                    );

                    if (selectionRect.Overlaps(childLocalRect))
                    {
                        _selectedEvents.Add(evt);
                        child.AddToClassList("event-clip--selected");
                    }
                    else
                    {
                        child.RemoveFromClassList("event-clip--selected");
                    }
                }
            }
        }

        if (_selectedEvents.Count > 1)
        {
            _selectedEvent = _selectedEvents[_selectedEvents.Count - 1];
            ShowMultiSelectionInspector();
        }
        else if (_selectedEvents.Count == 1)
        {
            _selectedEvent = _selectedEvents[0];
            OpenEventEditor(_selectedEvent);
        }
        else
        {
            OpenEventEditor(null);
        }
    }
    #endregion

    #region 键盘控制
    private void OnKeyDown(KeyDownEvent evt)
    {
        bool ctrl = evt.ctrlKey || evt.commandKey;
        bool handled = false;

        // Global shortcuts (work regardless of selection)
        switch (evt.keyCode)
        {
            case KeyCode.Space:
                if (_isPlaying) StopPlayback(); else StartPlayback();
                handled = true;
                break;

            case KeyCode.S when ctrl:
                SaveAsset();
                handled = true;
                break;

            case KeyCode.Z when ctrl:
                Undo.PerformUndo();
                RebuildAllTracksUI();
                handled = true;
                break;

            case KeyCode.Y when ctrl:
                Undo.PerformRedo();
                RebuildAllTracksUI();
                handled = true;
                break;

            case KeyCode.C when ctrl:
                CopySelectedEvents();
                handled = true;
                break;

            case KeyCode.V when ctrl:
                PasteEvents();
                handled = true;
                break;
        }

        if (handled)
        {
            evt.StopPropagation();
            evt.PreventDefault();
            return;
        }

        // Selection-dependent shortcuts
        if (_selectedEvent == null && _selectedEvents.Count == 0) return;

        // Delete works for both single and multi-select
        if (evt.keyCode == KeyCode.Delete || evt.keyCode == KeyCode.Backspace)
        {
            if (_selectedEvents.Count > 1)
            {
                DeleteSelectedEvents();
            }
            else if (_selectedEvent != null)
            {
                var track = _timelines.FirstOrDefault(t => t.events.Contains(_selectedEvent));
                if (track != null)
                {
                    if (_asset != null) Undo.RecordObject(_asset, "删除事件");
                    track.events.Remove(_selectedEvent);
                    SelectEvent(null, null);
                    RefreshTrackContentUI(track);
                }
            }
            evt.StopPropagation();
            evt.PreventDefault();
            return;
        }

        // Arrow keys: only for single selection
        if (_selectedEvent == null) return;
        TimelineData selectedTrack = _timelines.FirstOrDefault(t => t.events.Contains(_selectedEvent));
        if (selectedTrack == null) return;

        bool changed = false;

        switch (evt.keyCode)
        {
            case KeyCode.LeftArrow:
                if (_asset != null) Undo.RecordObject(_asset, "移动事件帧");
                if (ctrl)
                {
                    _selectedEvent.StartFrame = Mathf.Max(0, _selectedEvent.StartFrame - 1);
                }
                else if (evt.altKey)
                {
                    _selectedEvent.EndFrame = Mathf.Max(_selectedEvent.StartFrame + 1, _selectedEvent.EndFrame - 1);
                }
                else
                {
                    int duration = _selectedEvent.EndFrame - _selectedEvent.StartFrame;
                    _selectedEvent.StartFrame = Mathf.Max(0, _selectedEvent.StartFrame - 1);
                    _selectedEvent.EndFrame = _selectedEvent.StartFrame + duration;
                }
                changed = true;
                break;

            case KeyCode.RightArrow:
                if (_asset != null) Undo.RecordObject(_asset, "移动事件帧");
                if (ctrl)
                {
                    _selectedEvent.StartFrame = Mathf.Min(_selectedEvent.EndFrame - 1, _selectedEvent.StartFrame + 1);
                }
                else if (evt.altKey)
                {
                    _selectedEvent.EndFrame = Mathf.Min(GetTotalFrames(), _selectedEvent.EndFrame + 1);
                }
                else
                {
                    int duration = _selectedEvent.EndFrame - _selectedEvent.StartFrame;
                    _selectedEvent.StartFrame = Mathf.Min(_selectedEvent.StartFrame + 1, GetTotalFrames() - duration);
                    _selectedEvent.EndFrame = _selectedEvent.StartFrame + duration;
                }
                changed = true;
                break;
        }

        if (changed)
        {
            RefreshTrackContentUI(selectedTrack);
            OpenEventEditor(_selectedEvent);
            evt.StopPropagation();
            evt.PreventDefault();
        }
    }

    private void CopySelectedEvents()
    {
        _clipboard.Clear();
        var eventsToCopy = _selectedEvents.Count > 0 ? _selectedEvents :
            (_selectedEvent != null ? new List<TimelineEventBase> { _selectedEvent } : new List<TimelineEventBase>());

        foreach (var evt in eventsToCopy)
        {
            _clipboard.Add(evt.Clone());
        }
    }

    private void PasteEvents()
    {
        if (_clipboard.Count == 0) return;

        // Find the minimum start frame in clipboard to calculate offset
        int minFrame = int.MaxValue;
        foreach (var evt in _clipboard) minFrame = Mathf.Min(minFrame, evt.StartFrame);
        int offset = _currentFrame - minFrame;

        // Try to paste into the first selected track, or the first track
        TimelineData targetTrack = null;
        if (_selectedEvent != null)
            targetTrack = _timelines.FirstOrDefault(t => t.events.Contains(_selectedEvent));
        if (targetTrack == null && _timelines.Count > 0)
            targetTrack = _timelines[0];
        if (targetTrack == null) return;

        if (_asset != null) Undo.RecordObject(_asset, "粘贴事件");

        _selectedEvents.Clear();
        foreach (var clipEvt in _clipboard)
        {
            var newEvt = clipEvt.Clone();
            int duration = newEvt.EndFrame - newEvt.StartFrame;
            newEvt.StartFrame = Mathf.Max(0, newEvt.StartFrame + offset);
            newEvt.EndFrame = newEvt.StartFrame + duration;
            targetTrack.AddEvent(newEvt);
            _selectedEvents.Add(newEvt);
        }

        _selectedEvent = _selectedEvents.Count > 0 ? _selectedEvents[_selectedEvents.Count - 1] : null;
        RefreshTrackContentUI(targetTrack);

        if (_selectedEvents.Count > 1)
            ShowMultiSelectionInspector();
        else if (_selectedEvent != null)
            OpenEventEditor(_selectedEvent);
    }
    #endregion

    #region UI绘制与辅助方法
    private void DrawRulerTicks()
    {
        if (_ruler == null || _ruler.resolvedStyle.width <= 1) return;
        _ruler.Clear();
        
        var handle = new VisualElement { name = "playhead-handle", style = {
            width = 14, height = 14, backgroundColor = new Color(0.9f, 0.25f, 0.25f),
            borderTopLeftRadius = 7, borderTopRightRadius = 7, borderBottomLeftRadius = 7, borderBottomRightRadius = 7,
            position = Position.Absolute, top = (RULER_HEIGHT - 14) / 2
        }};
        handle.AddManipulator(new Dragger(pos => JumpToFrame(ScreenPosToFrame(pos.x, _ruler))));
        _ruler.Add(handle);

        if (_clip == null || _totalFrames <= 0 || _clip.length <= 0) return;
        float w = _ruler.resolvedStyle.width;
        float pixelsPerSec = w / _clip.length;
        float secStep = pixelsPerSec > 100 ? 0.25f : (pixelsPerSec > 40 ? 0.5f : 1.0f);
        
        for (float t = 0; t < _clip.length; t += secStep / 4) {
            bool isMajorTick = Mathf.Approximately(t % secStep, 0);
            bool isMinorTick = !isMajorTick && Mathf.Approximately(t % (secStep / 2), 0);
            if (!isMajorTick && !isMinorTick) continue;
            var tick = new VisualElement { style = {
                position = Position.Absolute, left = t * pixelsPerSec, width = 1,
                backgroundColor = isMajorTick ? Color.white : Color.gray,
                height = isMajorTick ? RULER_HEIGHT * 0.7f : RULER_HEIGHT * 0.4f
            }};
            tick.style.top = RULER_HEIGHT - tick.style.height.value.value;
            _ruler.Add(tick);
            if (isMajorTick && pixelsPerSec * secStep > 35) {
                _ruler.Add(new Label(t.ToString("F2")) { style = {
                    position = Position.Absolute, left = t * pixelsPerSec + 2, top = 2, fontSize = 10
                }});
            }
        }
    }
    
    private void UpdatePlayheadPosition()
    {
        if (_clip == null || _tracksRoot == null || _totalFrames <= 0 || _tracksRoot.resolvedStyle.width <= 0) return;
        float pixelsPerFrame = _tracksRoot.resolvedStyle.width / _totalFrames;
        float newLeft = _currentFrame * pixelsPerFrame;
        _playhead.style.left = newLeft;
        
        var handle = _ruler?.Q(name: "playhead-handle");
        if(handle != null) handle.style.left = newLeft - (handle.resolvedStyle.width / 2);
    }
    
    public void OpenEventEditor(TimelineEventBase evt)
    {
        if (_inspectorContent == null) return;

        if (_inspectorContent.userData as TimelineEventBase == evt && evt != null)
        {
            _inspectorContent.Q<IntegerField>("start-frame-field")?.SetValueWithoutNotify(evt.StartFrame);
            _inspectorContent.Q<IntegerField>("end-frame-field")?.SetValueWithoutNotify(evt.EndFrame);
            return;
        }

        _inspectorContent.Clear();
        _inspectorContent.userData = evt;

        if (evt == null) {
            _inspectorContent.Add(new Label("未选中任何事件。") { style = { unityTextAlign = TextAnchor.MiddleCenter, flexGrow = 1 }});
            return;
        }
        try {
            var factory = EventFactoryRegistry.GetFactory(evt.Type);
            var inspector = factory.CreateInspector(evt);
            var track = _timelines.First(t => t.events.Contains(evt));

            // --- 事件类型标题 ---
            var header = new Label(evt.Type.ToString());
            header.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.style.fontSize = 14;
            header.style.marginBottom = 6;
            _inspectorContent.Add(header);

            // --- 帧区间 ---
            var startFrameField = new IntegerField("起始帧") { value = evt.StartFrame, name = "start-frame-field" };
            startFrameField.style.marginBottom = 2;
            startFrameField.RegisterValueChangedCallback(changeEvt => {
                if (_asset != null) Undo.RecordObject(_asset, "修改事件起始帧");
                evt.StartFrame = Mathf.Clamp(changeEvt.newValue, 0, evt.EndFrame - 1);
                RefreshTrackContentUI(track);
                startFrameField.SetValueWithoutNotify(evt.StartFrame);
            });

            var endFrameField = new IntegerField("结束帧") { value = evt.EndFrame, name = "end-frame-field" };
            endFrameField.style.marginBottom = 4;
            endFrameField.RegisterValueChangedCallback(changeEvt => {
                if (_asset != null) Undo.RecordObject(_asset, "修改事件结束帧");
                evt.EndFrame = Mathf.Clamp(changeEvt.newValue, evt.StartFrame + 1, GetTotalFrames());
                RefreshTrackContentUI(track);
                endFrameField.SetValueWithoutNotify(evt.EndFrame);
            });
            _inspectorContent.Add(startFrameField);
            _inspectorContent.Add(endFrameField);

            // --- 分隔线 ---
            var separator = new VisualElement();
            separator.style.height = 1;
            separator.style.backgroundColor = new Color(0.3f, 0.3f, 0.3f);
            separator.style.marginTop = 6;
            separator.style.marginBottom = 6;
            _inspectorContent.Add(separator);

            // --- 属性标题 ---
            var propsHeader = new Label("属性");
            propsHeader.style.unityFontStyleAndWeight = FontStyle.Bold;
            propsHeader.style.marginBottom = 4;
            _inspectorContent.Add(propsHeader);

            // --- 工厂 Inspector (加间距) ---
            AddSpacingToChildren(inspector);
            _inspectorContent.Add(inspector);

            inspector.RegisterCallback<ChangeEvent<object>>(e => RefreshTrackContentUI(track));
        }
        catch (Exception ex) { Debug.LogError($"打开检视器时出错: {ex}"); _inspectorContent.Add(new Label("检视器出错")); }
    }

    /// <summary>
    /// 给 VisualElement 的直接子元素添加统一的垂直间距
    /// </summary>
    private void AddSpacingToChildren(VisualElement element)
    {
        element.RegisterCallback<GeometryChangedEvent>(evt =>
        {
            foreach (var child in element.Children())
            {
                // 跳过已有明确 marginBottom 的元素和 frame row（避免重复帧字段的间距）
                if (child.resolvedStyle.marginBottom < 1f)
                {
                    child.style.marginBottom = 4;
                }
            }
        });
    }

    private int ScreenPosToFrame(float screenX, VisualElement relativeTo = null)
    {
        if (_clip == null || _totalFrames == 0) return 0;
        var target = relativeTo ?? _timelineContent;
        if(target.resolvedStyle.width <= 0) return 0;
        
        float localX = target.worldTransform.inverse.MultiplyPoint(new Vector3(screenX, 0, 0)).x;
        if(relativeTo != null && relativeTo != _timelineContent)
        {
             localX += _trackContentScrollView.scrollOffset.x;
        }
        
        return Mathf.FloorToInt(Mathf.Clamp01(localX / target.resolvedStyle.width) * _totalFrames);
    }
    

    private TimelineEventBase CreateDefaultEventForTrack(TimelineEventType type, int frame)
    {
        if (_clip == null) return null;
        var factory = EventFactoryRegistry.GetFactory(type);
        if(factory == null) Debug.LogError("未找到事件工厂: " + type);
        var evt = factory.CreateEvent();
        if (evt == null) Debug.LogError("工厂创建事件失败: " + type);
        evt.StartFrame = frame;
        int defaultDurationFrames = _clip.frameRate > 0 ? Mathf.RoundToInt(_clip.frameRate * 0.25f) : 15;
        evt.EndFrame = frame + Mathf.Max(1, defaultDurationFrames);
        return evt;
    }

    private Color GetColorByType(TimelineEventType type)
    {
        return TimelineTrackRenderer.GetColorByType(type);
    }
    #endregion
    
    #region 播放控制与编辑器更新
    private void StartPlayback() 
    { 
        if (_clip == null || _previewObj == null) return; 
        _isPlaying = true; 
        _playTimeSec = (float)_currentFrame / _clip.frameRate; 
        _lastEditorTime = EditorApplication.timeSinceStartup; 
        if(!AnimationMode.InAnimationMode()) AnimationMode.StartAnimationMode(); 
    }

    private void StopPlayback() 
    { 
        _isPlaying = false; 
    }
    
    private void JumpToFrame(int frame)
    {
        if (_clip == null) { 
            _currentFrame = 0; _playTimeSec = 0; 
            if(_frameLabel != null) _frameLabel.text = "时间: -- | 帧: --"; 
            UpdatePlayheadPosition(); 
            return; 
        }
        _currentFrame = Mathf.Clamp(frame, 0, _totalFrames > 0 ? _totalFrames - 1 : 0);
        _playTimeSec = _totalFrames > 0 ? _currentFrame / (float)_clip.frameRate : 0;
        if(_frameLabel != null) _frameLabel.text = $"时间: {_playTimeSec:F2}s | 帧: {_currentFrame} / {_totalFrames}";
        UpdatePlayheadPosition();
        if (!AnimationMode.InAnimationMode()) AnimationMode.StartAnimationMode();
        SampleAnimationAtTime(_playTimeSec);
        TriggerEventsAtFrame(_currentFrame);
    }
    
    private void OnEditorUpdate()
    {
        if (!_isPlaying || _clip == null || _previewObj == null) 
        {
            UpdatePlayheadPosition();
            return;
        }

        double now = EditorApplication.timeSinceStartup;
        double deltaTime = now - _lastEditorTime;
        _playTimeSec += deltaTime * _playbackSpeed;
        _lastEditorTime = now;
        
        if (_playTimeSec >= _clip.length) _playTimeSec %= _clip.length;
        if (_playTimeSec < 0) _playTimeSec = 0;

        int newFrame = Mathf.FloorToInt((float)(_playTimeSec * _clip.frameRate));
        if (newFrame != _currentFrame) 
        { 
            JumpToFrame(newFrame); 
        }
        else 
        { 
            SampleAnimationAtTime(_playTimeSec); 
        }
    }

    private void SampleAnimationAtTime(double timeSec) 
    { 
        if (_clip == null || _previewObj == null || !AnimationMode.InAnimationMode()) return; 
        AnimationMode.SampleAnimationClip(_previewObj, _clip, (float)timeSec); 
        SceneView.RepaintAll();
    }
    
    private void TriggerEventsAtFrame(int frame)
    {
        if (_previewAudioSource == null) return;

        foreach (var timeline in _timelines)
        {
            foreach (var evt in timeline.events)
            {
                if (evt is SoundEvent soundEvent)
                {
                    if (frame == soundEvent.StartFrame && soundEvent.soundClip != null)
                    {
                        if (soundEvent.loop)
                        {
                            _previewAudioSource.clip = soundEvent.soundClip;
                            _previewAudioSource.volume = soundEvent.volume;
                            _previewAudioSource.loop = true;
                            _previewAudioSource.Play();
                        }
                        else
                        {
                            _previewAudioSource.PlayOneShot(soundEvent.soundClip, soundEvent.volume);
                        }
                    }
                    else if (frame == soundEvent.EndFrame && soundEvent.loop)
                    {
                        if (_previewAudioSource.isPlaying && _previewAudioSource.clip == soundEvent.soundClip)
                        {
                            _previewAudioSource.Stop();
                        }
                    }
                }
            }
        }

        SceneView.RepaintAll();
    }
    
    
    #endregion
    
    #region 场景视图与辅助方法
    private void OnSceneGUI(SceneView sceneView)
    {
        _sceneOverlay.PreviewObject = _previewObj;
        _sceneOverlay.CurrentFrame = _currentFrame;
        _sceneOverlay.Timelines = _timelines;
        _sceneOverlay.OnSceneGUI(sceneView);
    }

    private Transform FindDeepChild(Transform parent, string name)
    {
        var result = parent.Find(name);
        if (result != null) return result;
        foreach (Transform child in parent) {
            result = FindDeepChild(child, name);
            if (result != null) return result;
        }
        return null;
    }
    #endregion
    
    private class Dragger : PointerManipulator
    {
        private Action<Vector2> _onDrag;
        public Dragger(Action<Vector2> onDrag) { _onDrag = onDrag; }
        protected override void RegisterCallbacksOnTarget() {
            target.RegisterCallback<PointerDownEvent>(OnPointerDown);
            target.RegisterCallback<PointerMoveEvent>(OnPointerMove);
            target.RegisterCallback<PointerUpEvent>(OnPointerUp);
        }
        protected override void UnregisterCallbacksFromTarget() {
            target.UnregisterCallback<PointerDownEvent>(OnPointerDown);
            target.UnregisterCallback<PointerMoveEvent>(OnPointerMove);
            target.UnregisterCallback<PointerUpEvent>(OnPointerUp);
        }
        private void OnPointerDown(PointerDownEvent evt) {
            if (evt.button != 0) return;
            target.CapturePointer(evt.pointerId);
            evt.StopPropagation();
        }
        private void OnPointerMove(PointerMoveEvent evt) {
            if (target.HasPointerCapture(evt.pointerId)) {
                _onDrag?.Invoke(evt.position);
                evt.StopPropagation();
            }
        }
        private void OnPointerUp(PointerUpEvent evt) {
            if (target.HasPointerCapture(evt.pointerId)) {
                target.ReleasePointer(evt.pointerId);
                evt.StopPropagation();
            }
        }
    }

    #region 事件模板
    private void SaveSelectionAsTemplate()
    {
        var eventsToSave = _selectedEvents.Count > 0 ? _selectedEvents :
            (_selectedEvent != null ? new List<TimelineEventBase> { _selectedEvent } : new List<TimelineEventBase>());

        if (eventsToSave.Count == 0)
        {
            EditorUtility.DisplayDialog("保存模板", "请先选中要保存为模板的事件。", "确定");
            return;
        }

        string path = EditorUtility.SaveFilePanelInProject("保存事件模板", "NewEventTemplate", "asset", "选择模板保存位置");
        if (string.IsNullOrEmpty(path)) return;

        var template = ScriptableObject.CreateInstance<EventTemplate>();
        template.templateName = System.IO.Path.GetFileNameWithoutExtension(path);

        // Deep copy events and normalize frame offsets
        int minFrame = int.MaxValue;
        foreach (var evt in eventsToSave)
            minFrame = Mathf.Min(minFrame, evt.StartFrame);

        foreach (var evt in eventsToSave)
        {
            var clone = evt.Clone();
            int duration = clone.EndFrame - clone.StartFrame;
            clone.StartFrame -= minFrame;
            clone.EndFrame = clone.StartFrame + duration;
            template.events.Add(clone);
        }

        AssetDatabase.CreateAsset(template, path);
        AssetDatabase.SaveAssets();
        Debug.Log($"事件模板已保存: {path} ({template.events.Count} 个事件)");
    }

    private void ShowLoadTemplateMenu()
    {
        var guids = AssetDatabase.FindAssets("t:EventTemplate");
        if (guids.Length == 0)
        {
            EditorUtility.DisplayDialog("加载模板", "未找到任何事件模板。请先使用\"保存模板\"创建模板。", "确定");
            return;
        }

        var menu = new GenericMenu();
        foreach (var guid in guids)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            var template = AssetDatabase.LoadAssetAtPath<EventTemplate>(assetPath);
            if (template == null) continue;

            string label = !string.IsNullOrEmpty(template.templateName) ? template.templateName : template.name;
            menu.AddItem(new GUIContent(label), false, () => LoadTemplate(template));
        }
        menu.ShowAsContext();
    }

    private void LoadTemplate(EventTemplate template)
    {
        if (template == null || template.events.Count == 0) return;

        // Find target track
        TimelineData targetTrack = null;
        if (_selectedEvent != null)
            targetTrack = _timelines.FirstOrDefault(t => t.events.Contains(_selectedEvent));
        if (targetTrack == null && _timelines.Count > 0)
            targetTrack = _timelines[0];
        if (targetTrack == null)
        {
            EditorUtility.DisplayDialog("加载模板", "请先添加一个轨道。", "确定");
            return;
        }

        if (_asset != null) Undo.RecordObject(_asset, "加载事件模板");

        _selectedEvents.Clear();
        foreach (var evt in template.events)
        {
            var clone = evt.Clone();
            int duration = clone.EndFrame - clone.StartFrame;
            clone.StartFrame += _currentFrame;
            clone.EndFrame = clone.StartFrame + duration;
            targetTrack.AddEvent(clone);
            _selectedEvents.Add(clone);
        }

        _selectedEvent = _selectedEvents.Count > 0 ? _selectedEvents[_selectedEvents.Count - 1] : null;
        RefreshTrackContentUI(targetTrack);

        if (_selectedEvents.Count > 1)
            ShowMultiSelectionInspector();
        else if (_selectedEvent != null)
            OpenEventEditor(_selectedEvent);
    }
    #endregion

    // 进入调试模式
    public void EnterDebugMode(SkillTimelineAsset asset)
    {
        if (asset == null) return;
    
        _isInDebugMode = true;
    
        // 只有当资源变化时才重新加载，避免不必要的UI刷新
        if (_lastDebugAsset != asset)
        {
            LoadAsset(asset);
            _lastDebugAsset = asset;
        }

        // 禁用主内容区的交互
        _splitView.SetEnabled(false);
        _eventInspector.SetEnabled(false);
    }

// 设置调试帧
    public void SetDebugFrame(int frame)
    {
        if (!_isInDebugMode) return;
        JumpToFrame(frame);
    }

// 退出调试模式
    public void ExitDebugMode()
    {
        if (!_isInDebugMode) return;

        _isInDebugMode = false;
        _lastDebugAsset = null;
    
        // 重新启用交互
        _splitView.SetEnabled(true);
        _eventInspector.SetEnabled(true);
        
        LoadAsset(null);
    }

}