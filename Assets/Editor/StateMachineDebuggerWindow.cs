using UnityEngine;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine.UIElements;
using System;
using System.Collections.Generic;
using System.Linq;

public class StateMachineDebuggerWindow : EditorWindow
{
    private PlayerModel _playerModel;
    private StateMachine _playerSM;
    private StateMachine _animSM;

    private StateMachineGraphView _playerGraphView;
    private StateMachineGraphView _animGraphView;
    private VisualElement _historyContainer;
    private ScrollView _historyScrollView;
    private Label _statusLabel;

    [MenuItem("Window/State Machine Debugger")]
    public static void ShowWindow()
    {
        GetWindow<StateMachineDebuggerWindow>("State Machine Debugger");
    }

    private void OnEnable()
    {
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        if (EditorApplication.isPlaying)
            EditorApplication.delayCall += FindPlayerModel;
    }

    private void OnDisable()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        UnsubscribeEvents();
        _playerModel = null;
        _playerSM = null;
        _animSM = null;
    }

    private void CreateGUI()
    {
        rootVisualElement.Clear();

        // 顶部状态栏
        _statusLabel = new Label("等待 Play 模式...");
        _statusLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        _statusLabel.style.fontSize = 13;
        _statusLabel.style.paddingLeft = 8;
        _statusLabel.style.paddingTop = 4;
        _statusLabel.style.paddingBottom = 4;
        _statusLabel.style.backgroundColor = new Color(0.15f, 0.15f, 0.15f);
        rootVisualElement.Add(_statusLabel);

        // 双面板容器
        var splitView = new TwoPaneSplitView(0, 300, TwoPaneSplitViewOrientation.Horizontal);
        splitView.style.flexGrow = 1;

        // 左侧：Player State Machine
        var leftPanel = new VisualElement();
        leftPanel.style.flexGrow = 1;
        var leftHeader = CreatePanelHeader("Player State Machine");
        leftPanel.Add(leftHeader);
        _playerGraphView = new StateMachineGraphView();
        _playerGraphView.style.flexGrow = 1;
        leftPanel.Add(_playerGraphView);
        splitView.Add(leftPanel);

        // 右侧：Animation State Machine
        var rightPanel = new VisualElement();
        rightPanel.style.flexGrow = 1;
        var rightHeader = CreatePanelHeader("Animation State Machine");
        rightPanel.Add(rightHeader);
        _animGraphView = new StateMachineGraphView();
        _animGraphView.style.flexGrow = 1;
        rightPanel.Add(_animGraphView);
        splitView.Add(rightPanel);

        rootVisualElement.Add(splitView);

        // 底部：转换历史
        _historyContainer = new VisualElement();
        _historyContainer.style.height = 160;
        _historyContainer.style.borderTopWidth = 1;
        _historyContainer.style.borderTopColor = new Color(0.3f, 0.3f, 0.3f);
        _historyContainer.style.backgroundColor = new Color(0.18f, 0.18f, 0.18f);

        var historyHeader = new Label("转换历史");
        historyHeader.style.unityFontStyleAndWeight = FontStyle.Bold;
        historyHeader.style.fontSize = 12;
        historyHeader.style.paddingLeft = 8;
        historyHeader.style.paddingTop = 4;
        _historyContainer.Add(historyHeader);

        _historyScrollView = new ScrollView(ScrollViewMode.Vertical);
        _historyScrollView.style.flexGrow = 1;
        _historyContainer.Add(_historyScrollView);

        rootVisualElement.Add(_historyContainer);

        if (EditorApplication.isPlaying)
            FindPlayerModel();
        else
            UpdateStatus();
    }

    private VisualElement CreatePanelHeader(string title)
    {
        var header = new Label(title);
        header.style.unityFontStyleAndWeight = FontStyle.Bold;
        header.style.fontSize = 13;
        header.style.unityTextAlign = TextAnchor.MiddleCenter;
        header.style.height = 24;
        header.style.backgroundColor = new Color(0.22f, 0.22f, 0.22f);
        header.style.borderBottomWidth = 1;
        header.style.borderBottomColor = new Color(0.1f, 0.1f, 0.1f);
        return header;
    }

    private void OnPlayModeChanged(PlayModeStateChange change)
    {
        switch (change)
        {
            case PlayModeStateChange.EnteredPlayMode:
                EditorApplication.delayCall += FindPlayerModel;
                break;
            case PlayModeStateChange.ExitingPlayMode:
                UnsubscribeEvents();
                _playerModel = null;
                _playerSM = null;
                _animSM = null;
                _playerGraphView?.ClearGraph();
                _animGraphView?.ClearGraph();
                _historyScrollView?.Clear();
                UpdateStatus();
                break;
        }
    }

    private void FindPlayerModel()
    {
        _playerModel = FindObjectOfType<PlayerModel>();
        if (_playerModel != null)
        {
            _playerSM = _playerModel.DebugPlayerStateMachine;
            _animSM = _playerModel.DebugAnimationStateMachine;
            SubscribeEvents();
            RefreshAll();
        }
        UpdateStatus();
    }

    private void SubscribeEvents()
    {
        if (_playerSM != null) _playerSM.OnStateChanged += OnStateChanged;
        if (_animSM != null) _animSM.OnStateChanged += OnStateChanged;
    }

    private void UnsubscribeEvents()
    {
        if (_playerSM != null) _playerSM.OnStateChanged -= OnStateChanged;
        if (_animSM != null) _animSM.OnStateChanged -= OnStateChanged;
    }

    private void OnStateChanged()
    {
        RefreshAll();
    }

    private void RefreshAll()
    {
        if (_playerGraphView != null && _playerSM != null)
            _playerGraphView.RefreshFromStateMachine(_playerSM);
        if (_animGraphView != null && _animSM != null)
            _animGraphView.RefreshFromStateMachine(_animSM);
        RefreshHistory();
        UpdateStatus();
    }

    private void UpdateStatus()
    {
        if (_statusLabel == null) return;

        if (!EditorApplication.isPlaying)
        {
            _statusLabel.text = "进入 Play 模式后显示状态机可视化";
            _statusLabel.style.color = new Color(0.6f, 0.6f, 0.6f);
        }
        else if (_playerModel == null)
        {
            _statusLabel.text = "场景中未找到 PlayerModel";
            _statusLabel.style.color = new Color(1f, 0.6f, 0.2f);
        }
        else
        {
            string playerState = _playerSM?.CurrentStateType?.Name ?? "null";
            string animState = _animSM?.CurrentStateType?.Name ?? "null";
            _statusLabel.text = $"Player: {playerState}  |  Animation: {animState}";
            _statusLabel.style.color = new Color(0.5f, 1f, 0.5f);
        }
    }

    private void RefreshHistory()
    {
        if (_historyScrollView == null) return;
        _historyScrollView.Clear();

        var allRecords = new List<(string source, StateMachine.StateTransitionRecord record)>();

        if (_playerSM != null)
            foreach (var r in _playerSM.TransitionHistory)
                allRecords.Add(("Player", r));
        if (_animSM != null)
            foreach (var r in _animSM.TransitionHistory)
                allRecords.Add(("Anim", r));

        allRecords.Sort((a, b) => b.record.Timestamp.CompareTo(a.record.Timestamp));

        foreach (var (source, record) in allRecords)
        {
            string from = record.FromState != null ? record.FromState.Name : "(none)";
            string to = record.ToState.Name;
            string time = record.Timestamp.ToString("F2");

            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.paddingLeft = 8;
            row.style.paddingTop = 1;
            row.style.paddingBottom = 1;

            var timeLabel = new Label($"[{time}s]");
            timeLabel.style.width = 70;
            timeLabel.style.color = new Color(0.5f, 0.5f, 0.5f);
            timeLabel.style.fontSize = 11;
            row.Add(timeLabel);

            var sourceLabel = new Label($"[{source}]");
            sourceLabel.style.width = 55;
            sourceLabel.style.color = source == "Player" ? new Color(0.4f, 0.8f, 1f) : new Color(1f, 0.8f, 0.4f);
            sourceLabel.style.fontSize = 11;
            row.Add(sourceLabel);

            var transLabel = new Label($"{from}  \u2192  {to}");
            transLabel.style.fontSize = 11;
            transLabel.style.color = Color.white;
            row.Add(transLabel);

            _historyScrollView.Add(row);
        }

        if (allRecords.Count == 0)
        {
            var empty = new Label("（暂无转换记录）");
            empty.style.unityTextAlign = TextAnchor.MiddleCenter;
            empty.style.color = new Color(0.4f, 0.4f, 0.4f);
            empty.style.paddingTop = 8;
            _historyScrollView.Add(empty);
        }
    }
}

/// <summary>
/// 基于 GraphView 的状态机可视化面板
/// </summary>
public class StateMachineGraphView : GraphView
{
    private readonly Dictionary<Type, StateNode> _stateNodes = new Dictionary<Type, StateNode>();
    private Edge _transitionEdge;

    public StateMachineGraphView()
    {
        // 启用缩放和平移
        SetupZoom(ContentZoomer.DefaultMinScale, ContentZoomer.DefaultMaxScale);
        this.AddManipulator(new ContentDragger());
        this.AddManipulator(new SelectionDragger());
        this.AddManipulator(new RectangleSelector());

        // 背景网格
        var gridStyleSheet = EditorGUIUtility.Load("StyleSheets/GraphView/GridBackground.uss") as StyleSheet;
        if (gridStyleSheet != null)
            styleSheets.Add(gridStyleSheet);
        var grid = new GridBackground();
        Insert(0, grid);
        grid.StretchToParentSize();

        style.flexGrow = 1;
    }

    public void ClearGraph()
    {
        _stateNodes.Clear();
        _transitionEdge = null;
        DeleteElements(graphElements);
    }

    public void RefreshFromStateMachine(StateMachine sm)
    {
        if (sm == null) return;

        var registeredTypes = sm.RegisteredStateTypes.ToList();
        Type currentType = sm.CurrentStateType;
        Type previousType = sm.PreviousStateType;

        // 添加新节点（不删除旧的，懒加载状态会随时间增加）
        bool layoutChanged = false;
        foreach (var type in registeredTypes)
        {
            if (!_stateNodes.ContainsKey(type))
            {
                var node = new StateNode(type);
                _stateNodes[type] = node;
                AddElement(node);
                layoutChanged = true;
            }
        }

        if (layoutChanged)
            LayoutNodes();

        // 更新节点样式
        foreach (var kvp in _stateNodes)
        {
            var type = kvp.Key;
            var node = kvp.Value;

            if (type == currentType)
                node.SetState(StateNode.NodeState.Active);
            else if (type == previousType)
                node.SetState(StateNode.NodeState.Previous);
            else
                node.SetState(StateNode.NodeState.Inactive);
        }

        // 更新转换连线
        if (_transitionEdge != null)
        {
            RemoveElement(_transitionEdge);
            _transitionEdge = null;
        }

        if (previousType != null && currentType != null && previousType != currentType)
        {
            if (_stateNodes.TryGetValue(previousType, out var fromNode) &&
                _stateNodes.TryGetValue(currentType, out var toNode))
            {
                _transitionEdge = fromNode.OutputPort.ConnectTo(toNode.InputPort);
                _transitionEdge.edgeControl.inputColor = new Color(1f, 0.8f, 0.2f);
                _transitionEdge.edgeControl.outputColor = new Color(1f, 0.8f, 0.2f);
                AddElement(_transitionEdge);
            }
        }
    }

    private void LayoutNodes()
    {
        float x = 50;
        float y = 50;
        float spacingY = 80;

        foreach (var node in _stateNodes.Values)
        {
            node.SetPosition(new Rect(x, y, 180, 50));
            y += spacingY;
        }
    }

    // 禁止用户手动连线
    public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter nodeAdapter)
    {
        return new List<Port>();
    }
}

/// <summary>
/// 状态机中的单个状态节点
/// </summary>
public class StateNode : Node
{
    public enum NodeState { Inactive, Active, Previous }

    public Port InputPort { get; private set; }
    public Port OutputPort { get; private set; }
    public Type StateType { get; private set; }

    private static readonly Color ActiveColor = new Color(0.2f, 0.7f, 0.3f);
    private static readonly Color PreviousColor = new Color(0.6f, 0.6f, 0.2f);
    private static readonly Color InactiveColor = new Color(0.3f, 0.3f, 0.3f);

    public StateNode(Type stateType)
    {
        StateType = stateType;
        title = stateType.Name;

        // 不可删除、不可移动（只读可视化）
        capabilities &= ~Capabilities.Deletable;

        InputPort = Port.Create<Edge>(Orientation.Horizontal, Direction.Input, Port.Capacity.Multi, typeof(bool));
        InputPort.portName = "";
        inputContainer.Add(InputPort);

        OutputPort = Port.Create<Edge>(Orientation.Horizontal, Direction.Output, Port.Capacity.Multi, typeof(bool));
        OutputPort.portName = "";
        outputContainer.Add(OutputPort);

        RefreshExpandedState();
        RefreshPorts();

        SetState(NodeState.Inactive);
    }

    public void SetState(NodeState state)
    {
        Color color;
        switch (state)
        {
            case NodeState.Active:
                color = ActiveColor;
                break;
            case NodeState.Previous:
                color = PreviousColor;
                break;
            default:
                color = InactiveColor;
                break;
        }

        style.borderTopColor = color;
        style.borderBottomColor = color;
        style.borderLeftColor = color;
        style.borderRightColor = color;
        style.borderTopWidth = 2;
        style.borderBottomWidth = 2;
        style.borderLeftWidth = 2;
        style.borderRightWidth = 2;

        // 标题栏颜色
        var titleContainer = this.Q("title");
        if (titleContainer != null)
        {
            titleContainer.style.backgroundColor = color;
        }
    }
}
