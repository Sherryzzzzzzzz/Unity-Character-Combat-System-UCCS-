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
    private ScrollView _trackContentScrollView;
    private Label _frameLabel;
    private AnimationClip _defaultPoseClip;
    
    private TimelineEventBase _selectedEvent;
    private VisualElement _selectedEventClip;
    private HashSet<string> _activeHitboxes = new HashSet<string>();

    public int GetTotalFrames() => _totalFrames;
    public bool HasClip() => _clip != null;
    
    //运行时动态查看
    private bool _isInDebugMode = false;
    private SkillTimelineAsset _lastDebugAsset = null;
    
    private AudioSource _previewAudioSource;

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
        _eventInspector.style.paddingTop = 8;
        _eventInspector.style.paddingBottom = 8;
        _eventInspector.style.paddingLeft = 8;
        _eventInspector.style.paddingRight = 8;

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
    
    public void SelectEvent(TimelineEventBase evt, VisualElement clipElement)
    {
        // 【兼容性修复】使用 Blur() 方法主动让当前焦点元素失焦
        if (_root.focusController != null && _root.focusController.focusedElement != null)
        {
            (_root.focusController.focusedElement as VisualElement)?.Blur();
        }

        if (_selectedEvent == evt)
        {
            _timelineContent?.Focus();
            return;
        }

        _selectedEventClip?.RemoveFromClassList("event-clip--selected");
        _selectedEvent = evt;

        if (evt != null) {
            _selectedEventClip = clipElement ?? _timelines.SelectMany(t => t.trackRow.Children())
                .FirstOrDefault(c => c.userData == evt);
            _timelineContent?.schedule.Execute(() => _timelineContent.Focus());
        } else {
            _selectedEventClip = null;
        }

        _selectedEventClip?.AddToClassList("event-clip--selected");
        OpenEventEditor(evt);
    }
    #endregion

    #region 键盘控制
    private void OnKeyDown(KeyDownEvent evt)
    {
        if (_selectedEvent == null) return;
        
        // 【兼容性修复】移除对 leafTarget 和 TextInput 的依赖，因为我们通过主动失焦来避免冲突
        
        TimelineData track = _timelines.FirstOrDefault(t => t.events.Contains(_selectedEvent));
        if (track == null) return;
        
        bool changed = false;

        switch (evt.keyCode)
        {
            case KeyCode.LeftArrow:
                if (evt.ctrlKey || evt.commandKey)
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
                if (evt.ctrlKey || evt.commandKey)
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
            
            case KeyCode.Delete:
            case KeyCode.Backspace:
                track.events.Remove(_selectedEvent);
                SelectEvent(null, null);
                RefreshTrackContentUI(track);
                changed = true;
                break;
        }
        
        if (changed)
        {
            RefreshTrackContentUI(track);
            OpenEventEditor(_selectedEvent);
            evt.StopPropagation();
            evt.PreventDefault();
        }
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
        if (_eventInspector == null) return;

        if (_eventInspector.userData as TimelineEventBase == evt && evt != null)
        {
            _eventInspector.Q<IntegerField>("start-frame-field")?.SetValueWithoutNotify(evt.StartFrame);
            _eventInspector.Q<IntegerField>("end-frame-field")?.SetValueWithoutNotify(evt.EndFrame);
            return;
        }

        _eventInspector.Clear();
        _eventInspector.userData = evt;

        if (evt == null) {
            _eventInspector.Add(new Label("未选中任何事件。") { style = { unityTextAlign = TextAnchor.MiddleCenter, flexGrow = 1 }});
            return;
        }
        try {
            var factory = EventFactoryRegistry.GetFactory(evt.Type);
            var inspector = factory.CreateInspector(evt);
            var track = _timelines.First(t => t.events.Contains(evt));

            var startFrameField = new IntegerField("起始帧") { value = evt.StartFrame, name = "start-frame-field" };
            startFrameField.RegisterValueChangedCallback(changeEvt => {
                evt.StartFrame = Mathf.Clamp(changeEvt.newValue, 0, evt.EndFrame - 1);
                RefreshTrackContentUI(track);
                startFrameField.SetValueWithoutNotify(evt.StartFrame);
            });

            var endFrameField = new IntegerField("结束帧") { value = evt.EndFrame, name = "end-frame-field" };
            endFrameField.RegisterValueChangedCallback(changeEvt => {
                evt.EndFrame = Mathf.Clamp(changeEvt.newValue, evt.StartFrame + 1, GetTotalFrames());
                RefreshTrackContentUI(track);
                endFrameField.SetValueWithoutNotify(evt.EndFrame);
            });
            _eventInspector.Add(startFrameField);
            _eventInspector.Add(endFrameField);
            _eventInspector.Add(new IMGUIContainer(() => EditorGUILayout.Space()));
            _eventInspector.Add(inspector);

            inspector.RegisterCallback<ChangeEvent<object>>(e => RefreshTrackContentUI(track));
        }
        catch (Exception ex) { Debug.LogError($"打开检视器时出错: {ex}"); _eventInspector.Add(new Label("检视器出错")); }
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
        switch (type)
        {
            case TimelineEventType.Attack: return new Color(0.85f, 0.35f, 0.35f);
            case TimelineEventType.HitBox: return new Color(0.28f, 0.6f, 1f);
            case TimelineEventType.Combo: return new Color(0.25f, 0.9f, 0.35f);
            case TimelineEventType.Effect: return new Color(0.9f, 0.75f, 0.2f);
            case TimelineEventType.Sound: return new Color(0.8f, 0.5f, 1f);
            case TimelineEventType.Buff: return new Color(0.5f, 0.5f, 0.5f);
            case TimelineEventType.Loop: return new Color(0.3f, 0.8f, 0.8f);
            case TimelineEventType.Cancel: return new Color(0.85f, 0.85f, 0.8f);
            default: return Color.gray;
        }
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

        // 遍历所有轨道和事件
        foreach (var timeline in _timelines)
        {
            foreach (var evt in timeline.events)
            {
                // --- 处理音效事件 ---
                if (evt is SoundEvent soundEvent)
                {
                    // 如果当前帧是音效的起始帧
                    if (frame == soundEvent.StartFrame && soundEvent.soundClip != null)
                    {
                        if (soundEvent.loop)
                        {
                            // 如果是循环音效，则使用标准的 Play
                            _previewAudioSource.clip = soundEvent.soundClip;
                            _previewAudioSource.volume = soundEvent.volume;
                            _previewAudioSource.loop = true;
                            _previewAudioSource.Play();
                        }
                        else
                        {
                            // 如果是单次音效，使用 PlayOneShot，它不会打断当前正在播放的其他音效
                            _previewAudioSource.PlayOneShot(soundEvent.soundClip, soundEvent.volume);
                        }
                    }
                    // 如果当前帧是循环音效的结束帧
                    else if (frame == soundEvent.EndFrame && soundEvent.loop)
                    {
                        // 检查当前播放的片段是否就是这个循环音效，如果是，则停止它
                        if (_previewAudioSource.isPlaying && _previewAudioSource.clip == soundEvent.soundClip)
                        {
                            _previewAudioSource.Stop();
                        }
                    }
                }
                
                // (未来可以在这里添加其他需要预览的事件，比如特效)
            }
        }
        
        // 【新增】处理HitBox，我们把 OnFrameChanged 的逻辑也移到这里，让事件处理更集中
        _activeHitboxes.Clear();
        foreach (var track in _timelines) {
            foreach (var evt in track.events) {
                // 注意这里的逻辑是“在...期间”，而不是“在...开始”
                if (frame >= evt.StartFrame && frame < evt.EndFrame) { 
                    if (evt is AttackEvent atk && !string.IsNullOrEmpty(atk.hitBoxName))
                        _activeHitboxes.Add(atk.hitBoxName);
                }
            }
        }
        SceneView.RepaintAll();
    }
    
    
    #endregion
    
    #region 场景视图与辅助方法
    private void OnSceneGUI(SceneView sceneView) 
    {
        if (_previewObj == null || _activeHitboxes.Count == 0) return;
        
        Matrix4x4 originalMatrix = Handles.matrix;
        
        foreach (var hitName in _activeHitboxes) {
            var hitTransform = FindDeepChild(_previewObj.transform, hitName);
            if (hitTransform == null) continue;
            
            var hitCollider = hitTransform.GetComponent<Collider>();
            if (hitCollider == null) continue;

            Handles.color = new Color(1f, 0.3f, 0.3f, 0.8f);
            
            Handles.matrix = hitTransform.localToWorldMatrix;
            
            if (hitCollider is BoxCollider box)
            {
                Handles.DrawWireCube(box.center, box.size);
            }
            else if (hitCollider is SphereCollider sphere)
            {
                Handles.DrawWireDisc(sphere.center, Vector3.up, sphere.radius);
                Handles.DrawWireDisc(sphere.center, Vector3.right, sphere.radius);
                Handles.DrawWireDisc(sphere.center, Vector3.forward, sphere.radius);
            }
        }
        
        Handles.matrix = originalMatrix;
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