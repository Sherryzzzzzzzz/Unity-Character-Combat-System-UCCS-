using System;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// 事件 Inspector 面板 — 负责右侧属性编辑面板
/// </summary>
public class EventInspectorPanel
{
    private VisualElement _container;
    private TimelineEventBase _currentEvent;
    private Action _onEventChanged;
    private Func<List<TimelineData>> _getTimelines;
    private Func<int> _getTotalFrames;
    private Func<UnityEngine.Object> _getAsset;

    public VisualElement Container => _container;

    public EventInspectorPanel(Action onEventChanged, Func<List<TimelineData>> getTimelines, Func<int> getTotalFrames, Func<UnityEngine.Object> getAsset = null)
    {
        _onEventChanged = onEventChanged;
        _getTimelines = getTimelines;
        _getTotalFrames = getTotalFrames;
        _getAsset = getAsset;

        _container = new VisualElement();
        _container.style.width = 300;
        _container.style.borderLeftWidth = 1;
        _container.style.borderLeftColor = new Color(0.1f, 0.1f, 0.1f);
        _container.style.backgroundColor = new Color(0.21f, 0.21f, 0.21f);
        _container.style.paddingTop = 8;
        _container.style.paddingBottom = 8;
        _container.style.paddingLeft = 8;
        _container.style.paddingRight = 8;
    }

    public void ShowEvent(TimelineEventBase evt)
    {
        // 如果是同一个事件，只更新帧数字段
        if (_currentEvent == evt && evt != null)
        {
            _container.Q<IntegerField>("start-frame-field")?.SetValueWithoutNotify(evt.StartFrame);
            _container.Q<IntegerField>("end-frame-field")?.SetValueWithoutNotify(evt.EndFrame);
            return;
        }

        _container.Clear();
        _currentEvent = evt;
        _container.userData = evt;

        if (evt == null)
        {
            _container.Add(new Label("未选中任何事件。")
            {
                style = { unityTextAlign = TextAnchor.MiddleCenter, flexGrow = 1 }
            });
            return;
        }

        try
        {
            var factory = EventFactoryRegistry.GetFactory(evt.Type);
            var inspector = factory.CreateInspector(evt);
            var timelines = _getTimelines();
            var track = timelines.First(t => t.events.Contains(evt));

            var startFrameField = new IntegerField("起始帧") { value = evt.StartFrame, name = "start-frame-field" };
            startFrameField.RegisterValueChangedCallback(changeEvt =>
            {
                RecordUndoIfPossible("修改事件起始帧");
                evt.StartFrame = Mathf.Clamp(changeEvt.newValue, 0, evt.EndFrame - 1);
                _onEventChanged?.Invoke();
                startFrameField.SetValueWithoutNotify(evt.StartFrame);
            });

            var endFrameField = new IntegerField("结束帧") { value = evt.EndFrame, name = "end-frame-field" };
            endFrameField.RegisterValueChangedCallback(changeEvt =>
            {
                RecordUndoIfPossible("修改事件结束帧");
                evt.EndFrame = Mathf.Clamp(changeEvt.newValue, evt.StartFrame + 1, _getTotalFrames());
                _onEventChanged?.Invoke();
                endFrameField.SetValueWithoutNotify(evt.EndFrame);
            });

            _container.Add(startFrameField);
            _container.Add(endFrameField);
            _container.Add(new IMGUIContainer(() => EditorGUILayout.Space()));
            _container.Add(inspector);

            inspector.RegisterCallback<ChangeEvent<object>>(e => _onEventChanged?.Invoke());
        }
        catch (Exception ex)
        {
            Debug.LogError($"打开检视器时出错: {ex}");
            _container.Add(new Label("检视器出错"));
        }
    }

    public void ShowMultiSelection(int count, Action onDeleteAll)
    {
        _container.Clear();
        _currentEvent = null;
        _container.userData = null;

        _container.Add(new Label($"已选中 {count} 个事件")
        {
            style = { unityFontStyleAndWeight = FontStyle.Bold, marginBottom = 8 }
        });

        var deleteBtn = new Button(() => onDeleteAll?.Invoke()) { text = "批量删除选中事件" };
        deleteBtn.style.backgroundColor = new Color(0.8f, 0.2f, 0.2f);
        _container.Add(deleteBtn);
    }

    private void RecordUndoIfPossible(string description)
    {
        if (_getAsset == null) return;
        var asset = _getAsset();
        if (asset != null) Undo.RecordObject(asset, description);
    }
}
