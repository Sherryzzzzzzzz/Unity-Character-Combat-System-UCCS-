// SkillEditorTimelineWindow.cs

using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
using System.Collections.Generic;
using System.Drawing;
using Color = UnityEngine.Color;
using FontStyle = UnityEngine.FontStyle;
using System.Linq;


[InitializeOnLoad]
public static class TimelineEventFactoryBootstrap
{
    static TimelineEventFactoryBootstrap()
    {
        EventFactoryRegistry.Register(new AttackEventFactory());
        EventFactoryRegistry.Register(new HitBoxEventFactory());
        EventFactoryRegistry.Register(new ComboEventFactory());
    }
}

public class SkillEditorTimelineWindow : EditorWindow
{
    [MenuItem("Tools/Skill Editor Timeline")]
    public static void ShowWindow()
    {
        var wnd = GetWindow<SkillEditorTimelineWindow>();
        wnd.titleContent = new GUIContent("Skill Editor Timeline");
        wnd.minSize = new Vector2(800, 360);
    }

    
    // animation
    private AnimationClip _clip;
    private GameObject _previewObj;
    private int _totalFrames = 0;
    private double _playTimeSec = 0.0;    // 播放时间（秒）
    private int _lastFiredFrame = -1;
    // 播放速度倍数 (1 = 正常速度, 0.5 = 慢放, 2 = 加速)
    private float _playbackSpeed = 1f;
    //保存文件
    private SkillTimelineAsset _asset;

    // UI
    private VisualElement _root;
    private VisualElement _controlsRow;
    private VisualElement _ruler;         // 上面的尺子，点击跳帧
    private VisualElement _tracksRoot;    // 放所有轨道的容器
    private VisualElement _playhead;      // 红色竖线
    private Label _frameLabel;

    // timeline data
    private List<TimelineData> _timelines = new List<TimelineData>();

    // playback
    private bool _isPlaying = false;
    private double _lastEditorTime = 0.0;
    private HashSet<string> _firedThisPlay = new HashSet<string>(); // "trackIndex:frame"
    
    // inspector 用来编辑选中的事件参数（UI 面板）
    private VisualElement _eventInspector;
    
    //debug 用来给console来显示当前事件
    private List<TimelineEventBase> _debugFiredEvents = new List<TimelineEventBase>();
    
    //当前绘制的碰撞体
    private HashSet<string> _activeHitboxes = new HashSet<string>();
    
    private int _currentFrame = 0;

    public void CreateGUI()
    {
        _root = rootVisualElement;
        _root.style.flexDirection = FlexDirection.Column;
        _root.style.paddingLeft = 6;
        _root.style.paddingTop = 6;
        _root.style.paddingRight = 6;
        
        _controlsRow = new VisualElement();
        _controlsRow.style.flexDirection = FlexDirection.Row;
        _controlsRow.style.alignItems = Align.Center;
        _controlsRow.style.flexWrap = Wrap.Wrap; 
        _controlsRow.style.height = 20;
        _root.Add(_controlsRow);
        
        var clipField = new ObjectField("Animation Clip") { objectType = typeof(AnimationClip) };
        clipField.style.width = 260;
        clipField.RegisterValueChangedCallback(evt =>
        {
            _clip = evt.newValue as AnimationClip;
            if (_clip != null)
            {
                _totalFrames = Mathf.Max(1, Mathf.CeilToInt(_clip.length * _clip.frameRate));
                RebuildAllTracksUI();
            }
        });
        _controlsRow.Add(clipField);

        var objField = new ObjectField("Preview Object") { objectType = typeof(GameObject) };
            objField.style.width = 200;
            objField.RegisterValueChangedCallback(evt =>
            {
                _previewObj = evt.newValue as GameObject;
            });
            _controlsRow.Add(objField);
        
        var playBtn = new Button(() =>
            {
                if (_isPlaying) StopPlayback();
                else StartPlayback();
            }) { text = "▶ Play / || Pause" };
            _controlsRow.Add(playBtn);
        
            var assetField = new ObjectField("Timeline Asset") { objectType = typeof(SkillTimelineAsset) };
            assetField.style.width = 240;
            assetField.RegisterValueChangedCallback(evt =>
            {
                _asset = evt.newValue as SkillTimelineAsset;
                if (_asset == null)
                {
                    _timelines.Clear();
                    _tracksRoot.Clear();
                    _clip = null;
                    _totalFrames = 0;
                    RebuildAllTracksUI();
                    return;
                }

                // 同步 clip
                _clip = _asset.animationClip;
                clipField.value = _clip;
                _totalFrames = _clip != null ? Mathf.Max(1, Mathf.CeilToInt(_clip.length * _clip.frameRate)) : 0;

                // 把 SO 的 tracks -> 编辑器内部 _timelines（浅拷贝事件引用）
                _timelines.Clear();
                if (_asset.tracks != null)
                {
                    foreach (var t in _asset.tracks)
                    {
                        var timeline = new TimelineData(t.type, t.name);
                        if (t.events != null)
                            timeline.events.AddRange(t.events);
                        _timelines.Add(timeline);
                    }
                }

                // 先清 UI，再为每个 timeline 创建 UI
                _tracksRoot.Clear();
                foreach (var timeline in _timelines)
                {
                    CreateTrackUI(timeline); // CreateTrackUI 会把 timeline.trackRow 添加到 _tracksRoot
                }

                RebuildAllTracksUI();
            });
            _controlsRow.Add(assetField);

        var saveBtn = new Button(() =>
        {
            if (_asset == null)
            {
                string path = EditorUtility.SaveFilePanelInProject(
                    "Save Skill Timeline Asset",
                    "NewSkillTimeline",
                    "asset",
                    "Select location to save the timeline asset"
                );
                if (string.IsNullOrEmpty(path)) return;

                _asset = ScriptableObject.CreateInstance<SkillTimelineAsset>();
                AssetDatabase.CreateAsset(_asset, path);
                AssetDatabase.SaveAssets();
                Selection.activeObject = _asset;
            }

            // 把 _timelines 的内容抄回到 asset
            _asset.animationClip = _clip;
            _asset.tracks.Clear();
            foreach (var t in _timelines)
            {
                var track = new TimelineTrackData
                {
                    name = t.name,
                    type = t.type,
                };
                track.events.AddRange(t.events);
                _asset.tracks.Add(track);
            }

            EditorUtility.SetDirty(_asset);
            AssetDatabase.SaveAssets();
            Debug.Log("Skill timeline saved.");
        }){text = "Save"};
        saveBtn.style.width = 100;
        saveBtn.style.height = 20;
        _controlsRow.Add(saveBtn);

        _frameLabel = new Label("Frame: 0 / 0");
        _frameLabel.style.marginLeft = 8;
        _controlsRow.Add(_frameLabel);

        var typeField = new EnumField(TimelineEventType.Attack);
            typeField.style.width = 100;
            _controlsRow.Add(typeField);

        var addTrackBtn = new Button(() =>
            {
                var selectedType = (TimelineEventType)typeField.value;
                AddTrack(selectedType, $"Track {_timelines.Count + 1}");
            }) { text = "Add Track" };
            _controlsRow.Add(addTrackBtn);

        var speedField = new FloatField("Speed") { value = 1f };
            speedField.style.width = 120;
            speedField.style.flexGrow = 1; 
            speedField.style.minWidth = 80;
        
            speedField.RegisterValueChangedCallback(evt =>
            {
                _playbackSpeed = Mathf.Max(0f, evt.newValue);
            });
        
            _controlsRow.Add(speedField);
            
    
        clipField.style.marginRight = 6;
        objField.style.marginRight = 6;
        playBtn.style.marginRight = 6;
        speedField.style.marginRight = 6;
        
        // 整体横向排布：左边时间轴 + 右边inspector
        var contentRow = new VisualElement();
        contentRow.style.flexDirection = FlexDirection.Row;
        contentRow.style.flexGrow = 1;
        contentRow.style.marginTop = 6;
        _root.Add(contentRow);

        // ===== 左侧时间轴区域（ruler + tracks） =====
        var timelineContainer = new VisualElement();
        timelineContainer.style.flexDirection = FlexDirection.Column;
        timelineContainer.style.flexGrow = 1; // 占满左边剩余空间
        contentRow.Add(timelineContainer);

        // ruler
        _ruler = new VisualElement();
        _ruler.style.height = 20;
        _ruler.style.flexGrow = 1; // 宽度自动跟随 timelineContainer
        _ruler.style.marginTop = 6;
        _ruler.style.backgroundColor = new Color(0.12f, 0.12f, 0.12f);
        timelineContainer.Add(_ruler);

        _ruler.RegisterCallback<MouseDownEvent>(evt =>
        {
            if (_clip == null) return;
            float w = Mathf.Max(1f, _ruler.worldBound.width);
            float pixelsPerFrame = w / _totalFrames;
            int frame = Mathf.Clamp(Mathf.FloorToInt(evt.localMousePosition.x / pixelsPerFrame), 0, _totalFrames - 1);
            JumpToFrame(frame);
        });

        _ruler.RegisterCallback<GeometryChangedEvent>(evt => { DrawRulerTicks(); });

        // tracks
        _tracksRoot = new VisualElement();
        _tracksRoot.style.flexDirection = FlexDirection.Column;
        _tracksRoot.style.flexGrow = 1; // 宽度和 ruler 一致
        _tracksRoot.style.backgroundColor = new Color(0.08f, 0.08f, 0.08f);
        timelineContainer.Add(_tracksRoot);

        // ===== 右侧 Inspector =====
        _eventInspector = new VisualElement();
        _eventInspector.style.width = 300; // 固定宽度
        _eventInspector.style.marginLeft = 8;
        _eventInspector.style.backgroundColor = new Color(0.06f, 0.06f, 0.06f);
        _eventInspector.style.paddingLeft = 6;
        _eventInspector.style.paddingTop = 6;
        contentRow.Add(_eventInspector);


        #region 红线

        /// ===== playhead (红线) =====
        _playhead = new VisualElement();
        _playhead.style.position = Position.Absolute;
        _playhead.style.top = 30;   // 从 _controlsRow 底部开始
        _playhead.style.bottom = 0;                 // 到整个 tracks 区域底部
        _playhead.style.left = 0;                   // 初始位置最左
        _playhead.style.width = 2;
        _playhead.style.backgroundColor = Color.red;
        _playhead.pickingMode = PickingMode.Ignore;

        _root.Add(_playhead);
        _playhead.BringToFront();   // 保证在 ruler 和 tracks 之上

        #endregion

    
    
        var frameRow = new VisualElement();
        frameRow.style.flexGrow = 1f;
        _tracksRoot.Add(frameRow);
    
        // ===== editor update =====
        EditorApplication.update -= OnEditorUpdate;
        EditorApplication.update += OnEditorUpdate;
        SceneView.duringSceneGui += OnSceneGUI;
    }


    private void OnDestroy()
    {
        EditorApplication.update -= OnEditorUpdate;
        SceneView.duringSceneGui -= OnSceneGUI;
        AnimationMode.StopAnimationMode();
    }

    #region 添加轨道

    // ===== track / UI 构建 =====
    private void AddTrack(TimelineEventType type, string name)
    {
        var data = new TimelineData(type, name);
        _timelines.Add(data);

        // 复用 CreateTrackUI 来构造 UI，避免重复代码
        CreateTrackUI(data);

        // 确保 UI 刷新
        RefreshEventTrackUI(data);
    }


    #endregion

    // Rebuild UI for all tracks (call when clip changes)
    private void RebuildAllTracksUI()
    {
        DrawRulerTicks();

        // 清掉已有 UI，全部由 CreateTrackUI 重新创建（避免重复注册回调）
        _tracksRoot.Clear();

        foreach (var t in _timelines)
        {
            // 保证 trackRow 重新生成并加入容器
            t.trackRow = null;
            CreateTrackUI(t);
        }

        // 最后一次刷新绘制（确保使用最新的宽度/像素比）
        foreach (var t in _timelines)
            RefreshEventTrackUI(t);
    }
    

    #region 刻度尺

    // Draw small tick marks on the ruler (simple approach)
    private void DrawRulerTicks()
    {
        _ruler.Clear();
        if (_clip == null || _totalFrames <= 0) return;

        float w = Mathf.Max(1f, _ruler.worldBound.width);
        float pixelsPerFrame = w / _totalFrames;

        // Draw minor ticks and bigger ticks each 10 frames
        for (int i = 0; i < _totalFrames; i++)
        {
            var tick = new VisualElement();
            tick.style.position = Position.Absolute;
            tick.style.left = i * pixelsPerFrame;
            tick.style.width = 1;
            tick.style.backgroundColor = (i % 10 == 0) ? new Color(0.7f, 0.7f, 0.7f) : new Color(0.35f, 0.35f, 0.35f);
            tick.style.height = (i % 10 == 0) ? 16 : 8;
            tick.style.top = 2;
            _ruler.Add(tick);
        }
    }

    #endregion
    
    // 打开事件编辑窗口
    private void OpenEventEditor(TimelineEventBase evt, TimelineData track)
    {
        _eventInspector.Clear();

        try
        {
            var factory = EventFactoryRegistry.GetFactory(evt.Type);
            if (factory == null)
            {
                Debug.LogError($"[SkillEditor] No factory registered for event type {evt.Type}");
                _eventInspector.Add(new Label($"No factory for type {evt.Type}"));
                return;
            }

            var inspector = factory.CreateInspector(evt);
            if (inspector == null)
            {
                Debug.LogError($"[SkillEditor] Factory {factory.GetType().Name} returned null inspector for {evt.Type}");
                _eventInspector.Add(new Label($"Factory {factory.GetType().Name} returned null inspector"));
                return;
            }

            _eventInspector.Add(inspector);
            
            if (inspector == null)
            {
                _eventInspector.Add(new Label("Inspector not implemented."));
                return;
            }

        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[SkillEditor] Failed to open inspector for event {evt?.Type}: {ex}");
            _eventInspector.Add(new Label("Inspector error, see Console"));
        }
        
    }
    

    // Refresh a single track's event UI (clear + rebuild markers)
    private void RefreshEventTrackUI(TimelineData data)
    {
        if (data.trackRow == null) return;
        var row = data.trackRow;
        row.Clear();

        if (_clip == null || _totalFrames <= 0) return;

        float w = Mathf.Max(1f, row.worldBound.width);
        float pixelsPerFrame = w / _totalFrames;

        // 背景
        var bg = new VisualElement();
        bg.style.position = Position.Absolute;
        bg.style.left = 0;
        bg.style.right = 0;
        bg.style.top = 0;
        bg.style.bottom = 0;
        bg.style.backgroundColor = new Color(0.12f, 0.12f, 0.12f);
        row.Add(bg);

        if (data.events.Count == 0) return;

        var evt = data.events[0];
        float startX = evt.StartFrame * pixelsPerFrame;
        float endX = evt.EndFrame * pixelsPerFrame;
        float width = Mathf.Max(6, Mathf.Abs(endX - startX));

        // 绘制长条（与方块同色）
        var color = GetColorByType(data.type);
        var region = new VisualElement();
        region.style.position = Position.Absolute;
        region.style.left = Mathf.Min(startX, endX);
        region.style.width = width;
        region.style.height = 16;
        region.style.top = 6;
        region.style.backgroundColor = color;
        region.style.borderTopLeftRadius = 3;
        region.style.borderBottomLeftRadius = 3;
        region.style.borderTopRightRadius = 3;
        region.style.borderBottomRightRadius = 3;
        row.Add(region);

        // 绘制起点方块
        var startMarker = new VisualElement();
        startMarker.style.position = Position.Absolute;
        startMarker.style.left = startX - 3;
        startMarker.style.width = 6;
        startMarker.style.height = 18;
        startMarker.style.top = 5;
        startMarker.style.backgroundColor = color;
        startMarker.tooltip = $"Start: {evt.StartFrame}";
        row.Add(startMarker);

        // 绘制终点方块
        var endMarker = new VisualElement();
        endMarker.style.position = Position.Absolute;
        endMarker.style.left = endX - 3;
        endMarker.style.width = 6;
        endMarker.style.height = 18;
        endMarker.style.top = 5;
        endMarker.style.backgroundColor = color;
        endMarker.tooltip = $"End: {evt.EndFrame}";
        row.Add(endMarker);
    }

    
    private void CreateTrackUI(TimelineData data)
    {
        // 清理旧UI
        if (data.trackRow != null)
        {
            try { _tracksRoot.Remove(data.trackRow); } catch { }
            data.trackRow = null;
        }

        var trackRoot = new VisualElement();
        trackRoot.style.flexDirection = FlexDirection.Column;
        trackRoot.style.marginTop = 6;
        trackRoot.style.marginBottom = 6;
        trackRoot.style.paddingLeft = 4;
        trackRoot.style.paddingRight = 4;

        // title row
        var titleRow = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center } };
        var titleLabel = new Label(data.name + $" ({data.type})")
        {
            style = { unityFontStyleAndWeight = FontStyle.Bold, flexGrow = 1 }
        };
        titleRow.Add(titleLabel);

        var delBtn = new Button(() =>
        {
            _timelines.Remove(data);
            _tracksRoot.Remove(trackRoot);
        })
        { text = "X", tooltip = "Delete track" };
        titleRow.Add(delBtn);
        trackRoot.Add(titleRow);

        // frame row (the clickable area)
        var frameRow = new VisualElement();
        frameRow.style.height = 28;
        frameRow.style.position = Position.Relative;
        frameRow.style.backgroundColor = new Color(0.12f, 0.12f, 0.12f);
        frameRow.style.paddingLeft = 2;
        frameRow.style.paddingRight = 2;
        trackRoot.Add(frameRow);

        // === 点击轨道：更新 StartFrame / EndFrame ===
        frameRow.RegisterCallback<MouseDownEvent>(evt =>
        {
            if (_clip == null || _totalFrames <= 0) return;

            float w = Mathf.Max(1f, frameRow.worldBound.width);
            float pixelsPerFrame = w / _totalFrames;
            int clickedFrame = Mathf.Clamp(Mathf.FloorToInt(evt.localMousePosition.x / pixelsPerFrame), 0, _totalFrames - 1);

            TimelineEventBase evtObj = null;

            // 如果此轨道还没有事件，则创建一个新的（通过工厂方法）
            if (data.events.Count == 0)
            {
                evtObj = CreateDefaultEventForTrack(data.type, clickedFrame);
                evtObj.StartFrame = clickedFrame;
                evtObj.EndFrame = clickedFrame + 1;
                data.AddEvent(evtObj);
            }
            else
            {
                evtObj = data.events[0] as TimelineEventBase;
                // 点击在事件范围外时，判断是往前扩展还是往后缩
                if (clickedFrame < evtObj.StartFrame)
                    evtObj.StartFrame = clickedFrame; // 往左扩展
                else if (clickedFrame > evtObj.EndFrame)
                    evtObj.EndFrame = clickedFrame; // 往右扩展
                else
                {
                    // 点击区间内部 -> 根据点击位置决定收缩方向
                    int mid = (evtObj.StartFrame + evtObj.EndFrame) / 2;
                    if (clickedFrame <= mid)
                        evtObj.StartFrame = Mathf.Min(evtObj.EndFrame, clickedFrame); // 收缩左边
                    else
                        evtObj.EndFrame = Mathf.Max(evtObj.StartFrame, clickedFrame); // 收缩右边
                }
            }

            RefreshEventTrackUI(data);
            OpenEventEditor(evtObj, data);
        });

        // 尺寸变化时刷新绘制
        frameRow.RegisterCallback<GeometryChangedEvent>(e => RefreshEventTrackUI(data));

        data.trackRow = frameRow;
        _tracksRoot.Add(trackRoot);
        RefreshEventTrackUI(data);
    }



    
    
    // 根据轨道类型生成一个默认事件（这里使用你自己定义的事件类名）
    private TimelineEventBase CreateDefaultEventForTrack(TimelineEventType type, int frame)
    {
        switch (type)
        {
            case TimelineEventType.Attack:
                return new AttackEvent { StartFrame = frame, EndFrame = frame };
            case TimelineEventType.HitBox:
                return new HitBoxEvent { StartFrame = frame, EndFrame = frame };
            case TimelineEventType.Combo:
                return new ComboEvent { StartFrame = frame, EndFrame = frame };
            default:
                throw new System.ArgumentOutOfRangeException(nameof(type), $"Unsupported type: {type}");
        }
    }





    // ===== playback / sampling / event firing =====
    private void StartPlayback()
    {
        if (_clip == null || _previewObj == null) return;
        _isPlaying = true;
        _playTimeSec = 0.0;
        _lastEditorTime = EditorApplication.timeSinceStartup;
        _firedThisPlay.Clear();
        AnimationMode.StartAnimationMode();
        SampleAnimationAtTime(_playTimeSec);
        UpdatePlayheadPosition();
    }

    private void StopPlayback()
    {
        _isPlaying = false;
        AnimationMode.StopAnimationMode();
        JumpToFrame(_lastFiredFrame);
    }
    

    // 跳到指定帧（来自点击尺子）
    private void JumpToFrame(int frame)
    {
        if (_clip == null || _previewObj == null) return;

        _playTimeSec = frame / (float)_clip.frameRate;
        _lastFiredFrame = -1;
        _frameLabel.text = $"Frame: {frame} / {_totalFrames}";
        UpdatePlayheadPosition();

        if (!AnimationMode.InAnimationMode())
            AnimationMode.StartAnimationMode();

        SampleAnimationAtTime(_playTimeSec);
        ComputeActiveHitboxesUpToFrame(frame);
        OnFrameChanged(frame);
    }

    private void ComputeActiveHitboxesUpToFrame(int frame)
    {
        _activeHitboxes.Clear();

        foreach (var track in _timelines)
        {
            foreach (var e in track.events)
            {
                if (e is AttackEvent atk)
                {
                    // 当前帧位于攻击事件区间内 → 激活
                    if (frame >= atk.StartFrame && frame <= atk.EndFrame)
                    {
                        if (!string.IsNullOrEmpty(atk.hitBoxName))
                            _activeHitboxes.Add(atk.hitBoxName);
                    }
                }
            }
        }
    }




    #region 播放

    private void SampleAnimationAtTime(double timeSec)
    {
        if (_clip == null || _previewObj == null) return;
        AnimationMode.SampleAnimationClip(_previewObj, _clip, (float)timeSec);
    }

    // playhead position based on first track row width (all track rows share same width)
    private void UpdatePlayheadPosition()
    {
        if (_clip == null) return;

        float availableWidth = 0f;

        // prefer ruler width; if ruler not sized yet, try to use first track's row width
        if (_ruler != null && _ruler.worldBound.width > 2f) availableWidth = _ruler.worldBound.width;
        else
        {
            foreach (var t in _timelines)
            {
                if (t.trackRow != null && t.trackRow.worldBound.width > 2f)
                {
                    availableWidth = t.trackRow.worldBound.width;
                    break;
                }
            }
        }

        if (availableWidth <= 0f) return;

        float pixelsPerFrame = availableWidth / Mathf.Max(1, _totalFrames);
        float x = Mathf.Clamp((float)(_playTimeSec * _clip.frameRate) * pixelsPerFrame, 0f, availableWidth);
        _playhead.style.left = x;
    }
    
    private void OnEditorUpdate()
    {
        // keep updating playhead position even when not playing (so jump works)
        if (_clip != null)
        {
            UpdatePlayheadPosition();
        }

        // 一直更新红线位置（暂停也要更新）
        if (_clip != null)
        {
            UpdatePlayheadPosition();
        }

        // 如果没在播放，就不更新时间，但仍允许跳帧
        if (!_isPlaying) return;

        if (_clip == null || _previewObj == null) return;

        double now = EditorApplication.timeSinceStartup;
        double delta = now - _lastEditorTime;
        _lastEditorTime = now;

        _playTimeSec += delta * _playbackSpeed;

        // loop
        if (_playTimeSec >= _clip.length)
        {
            _playTimeSec %= _clip.length;
            _firedThisPlay.Clear(); // 重播时重置已触发事件
        }

        // compute current frame
        int frameIndex = Mathf.FloorToInt((float)(_playTimeSec * _clip.frameRate));
        frameIndex = Mathf.Clamp(frameIndex, 0, Mathf.Max(0, _totalFrames - 1));

        // trigger events on frame change
        if (frameIndex != _lastFiredFrame)
        {
            OnFrameChanged(frameIndex);
            _lastFiredFrame = frameIndex;
        }

        // sample animation continuously at playTimeSec
        SampleAnimationAtTime(_playTimeSec);
    }

    #endregion

    #region 事件相关

    private Color GetColorByType(TimelineEventType type)
    {
        switch (type)
        {
            case TimelineEventType.Attack: return new Color(0.85f, 0.35f, 0.35f);
            case TimelineEventType.HitBox: return new Color(0.28f, 0.6f, 1f);
            case TimelineEventType.Combo: return new Color(0.25f, 0.9f, 0.35f);
            default: return Color.gray;
        }
    }
    
    private void OnFrameChanged(int frameIndex)
    {
        _currentFrame = frameIndex;
        _frameLabel.text = $"Frame: {frameIndex} / {_totalFrames}";

        for (int i = 0; i < _timelines.Count; i++)
        {
            var t = _timelines[i];
            if (t.events.Count == 0) continue;

            var evt = t.events[0];
            int start = evt.StartFrame;
            int end = evt.EndFrame;
            bool inRange = frameIndex >= start && frameIndex <= end;
            string key = $"{i}:{evt.GetType().Name}";

            if (inRange && !_firedThisPlay.Contains(key))
            {
                _firedThisPlay.Add(key);
                Debug.Log($"[SkillEditor] ▶ Enter {evt.GetType().Name} ({evt.GetSummary()}) on Track '{t.name}' [{start}-{end}]");

                if (evt is AttackEvent atk)
                    _activeHitboxes.Add(atk.hitBoxName);
            }
            else if (!inRange && _firedThisPlay.Contains(key))
            {
                _firedThisPlay.Remove(key);
                Debug.Log($"[SkillEditor] ⏹ Exit {evt.GetType().Name} ({evt.GetSummary()}) on Track '{t.name}' [{start}-{end}]");

                if (evt is AttackEvent atk)
                    _activeHitboxes.Remove(atk.hitBoxName);
            }
        }
    }

    #endregion
    
    //显示和debug
    private void OnSceneGUI(SceneView sceneView)
    {
        if (_previewObj == null || _timelines == null) return;

        _activeHitboxes.Clear();

        foreach (var track in _timelines)
        {
            foreach (var evt in track.events)
            {
                if (evt is AttackEvent atk)
                {
                    if (_currentFrame >= atk.StartFrame && _currentFrame <= atk.EndFrame)
                    {
                        if (!string.IsNullOrEmpty(atk.hitBoxName))
                            _activeHitboxes.Add(atk.hitBoxName);
                    }
                }
            }
        }

        foreach (var hitName in _activeHitboxes)
        {
            var hitTransform = FindDeepChild(_previewObj.transform, hitName);
            if (hitTransform == null) continue;

            var hit = hitTransform.GetComponent<Collider>();
            if (hit == null) continue;

            Handles.color = new Color(1f, 0.3f, 0.3f, 0.8f);
            var b = hit.bounds;
            Handles.DrawWireCube(b.center, b.size);
            Handles.DrawAAPolyLine(2f, new Vector3[]
            {
                b.center + Vector3.up * b.extents.y,
                b.center - Vector3.up * b.extents.y
            });
        }

        sceneView.Repaint();
    }


    
    private Transform FindDeepChild(Transform parent, string name)
    {
        foreach (Transform child in parent)
        {
            if (child.name == name)
                return child;
            var result = FindDeepChild(child, name);
            if (result != null)
                return result;
        }
        return null;
    }

}
