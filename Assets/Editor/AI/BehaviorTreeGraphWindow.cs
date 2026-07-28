using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

public class BehaviorTreeGraphWindow : EditorWindow
{
    private BTreeAsset _asset;
    private BTreeRunner _runner;
    private GameObject _previewTarget;

    private ScrollView _canvasScroll;
    private VisualElement _canvas;
    private VisualElement _connectionsLayer;
    private VisualElement _inspector;

    private readonly List<GraphNode> _graphNodes = new();
    private GraphNode _selectedNode;
    private bool _dirtyLayout = true;

    private const float NODE_W = 160f;
    private const float NODE_H = 56f;
    private const float COL_GAP = 200f;
    private const float ROW_GAP = 24f;
    private static readonly Color[] DepthColors = {
        new(0.18f, 0.18f, 0.24f),
        new(0.22f, 0.20f, 0.18f),
        new(0.18f, 0.22f, 0.20f),
        new(0.22f, 0.18f, 0.22f),
        new(0.18f, 0.22f, 0.22f),
    };

    [MenuItem("Tools/行为树图编辑器")]
    public static void Open()
    {
        var w = GetWindow<BehaviorTreeGraphWindow>("行为树图编辑器");
        w.minSize = new Vector2(800, 500);
    }

    public static void OpenWithAsset(BTreeAsset asset, GameObject previewTarget = null, BTreeRunner runner = null)
    {
        var w = GetWindow<BehaviorTreeGraphWindow>("行为树图编辑器");
        w.minSize = new Vector2(800, 500);
        w.LoadAsset(asset, previewTarget, runner);
    }

    private void OnEnable()
    {
        BuildUI();
    }

    private void BuildUI()
    {
        var root = rootVisualElement;
        root.Clear();
        root.style.flexDirection = FlexDirection.Column;

        // toolbar
        var toolbar = new Toolbar();
        root.Add(toolbar);

        var assetField = new ObjectField("行为树资产") { objectType = typeof(BTreeAsset), allowSceneObjects = false, style = { width = 240 } };
        assetField.RegisterValueChangedCallback(evt => LoadAsset(evt.newValue as BTreeAsset, null, null));
        toolbar.Add(assetField);

        toolbar.Add(new ToolbarSpacer());
        var saveBtn = new ToolbarButton(SaveAsset) { text = "保存修改" };
        toolbar.Add(saveBtn);

        toolbar.Add(new ToolbarSpacer { flex = true });
        var fitBtn = new ToolbarButton(AutoLayout) { text = "自动布局" };
        toolbar.Add(fitBtn);

        // 组合节点
        var addSeqBtn = new ToolbarButton(() => AddChildNode<BTSequence>()) { text = "+Sequence" };
        toolbar.Add(addSeqBtn);
        var addSelBtn = new ToolbarButton(() => AddChildNode<BTSelector>()) { text = "+Selector" };
        toolbar.Add(addSelBtn);
        // 装饰节点
        var addRepBtn = new ToolbarButton(() => AddChildNode<BTRepeater>()) { text = "+循环" };
        toolbar.Add(addRepBtn);
        var addInvBtn = new ToolbarButton(() => AddChildNode<BTInverter>()) { text = "+取反" };
        toolbar.Add(addInvBtn);
        var addCoolBtn = new ToolbarButton(() => AddChildNode<BTCooldown>()) { text = "+冷却" };
        toolbar.Add(addCoolBtn);
        // 动作节点
        var addSkillBtn = new ToolbarButton(() => AddChildNode<BTA_PlaySkill>()) { text = "+技能" };
        toolbar.Add(addSkillBtn);
        var addMoveBtn = new ToolbarButton(() => AddChildNode<BTA_MoveTo>()) { text = "+移动" };
        toolbar.Add(addMoveBtn);
        var addWaitBtn = new ToolbarButton(() => AddChildNode<BTWait>()) { text = "+等待" };
        toolbar.Add(addWaitBtn);
        var addCondBtn = new ToolbarButton(() => AddChildNode<BTCondition>()) { text = "+条件" };
        toolbar.Add(addCondBtn);
        var addBBBtn = new ToolbarButton(() => AddChildNode<BTA_SetBlackboard>()) { text = "+黑板" };
        toolbar.Add(addBBBtn);
        var addSubBtn = new ToolbarButton(() => AddChildNode<BTSubTree>()) { text = "+子树" };
        toolbar.Add(addSubBtn);
        var delBtn = new ToolbarButton(DeleteSelectedNode) { text = "删除选中" };
        toolbar.Add(delBtn);

        // body: canvas + inspector
        var body = new TwoPaneSplitView(0, 260, TwoPaneSplitViewOrientation.Horizontal);
        body.style.flexGrow = 1;
        root.Add(body);

        _canvasScroll = new ScrollView(ScrollViewMode.VerticalAndHorizontal);
        _canvasScroll.style.flexGrow = 1;
        _canvasScroll.horizontalScrollerVisibility = ScrollerVisibility.Auto;
        _canvasScroll.verticalScrollerVisibility = ScrollerVisibility.Auto;
        body.Add(_canvasScroll);

        // canvas 用正常流布局，让 ScrollView 能正确测量内容尺寸
        _canvas = new VisualElement();
        _canvas.style.width = 3000;
        _canvas.style.height = 3000;
        _canvas.style.flexShrink = 0;
        _canvasScroll.Add(_canvas);

        _connectionsLayer = new VisualElement();
        _connectionsLayer.style.position = Position.Absolute;
        _connectionsLayer.style.top = 0;
        _connectionsLayer.style.left = 0;
        _connectionsLayer.style.width = 3000;
        _connectionsLayer.style.height = 3000;
        _connectionsLayer.generateVisualContent = DrawConnections;
        _connectionsLayer.pickingMode = PickingMode.Ignore;
        _canvas.Add(_connectionsLayer);

        var nodeContainer = new VisualElement();
        nodeContainer.style.position = Position.Absolute;
        nodeContainer.style.top = 0;
        nodeContainer.style.left = 0;
        nodeContainer.style.width = 3000;
        nodeContainer.style.height = 3000;
        _canvas.Add(nodeContainer);

        _inspector = new ScrollView();
        _inspector.style.flexGrow = 1;
        _inspector.style.paddingLeft = 6;
        _inspector.style.paddingRight = 6;
        _inspector.style.paddingTop = 6;
        body.Add(_inspector);

        // store canvas ref in nodeContainer userData for later
        _canvas.userData = nodeContainer;

        if (_asset != null) LoadAsset(_asset, _previewTarget, _runner);
        else RefreshCanvas();
    }

    private void LoadAsset(BTreeAsset asset, GameObject previewTarget, BTreeRunner runner)
    {
        _asset = asset;
        _previewTarget = previewTarget;
        _runner = runner;
        if (_asset != null)
            _dirtyLayout = true;
        if (_canvas == null)
            BuildUI();
        else
            RefreshCanvas();
    }

    private void SaveAsset()
    {
        if (_asset == null) return;
        EditorUtility.SetDirty(_asset);
        AssetDatabase.SaveAssets();
    }

    private void RefreshCanvas()
    {
        if (_canvas == null) return;
        var nodeContainer = _canvas.userData as VisualElement;
        if (nodeContainer == null) return;

        // 记录当前选中的节点 GUID，刷新后恢复选中状态
        string selectedGuid = _selectedNode?.node?.guid;

        nodeContainer.Clear();
        _graphNodes.Clear();
        _selectedNode = null;

        if (_asset == null || _asset.rootNode == null)
        {
            _connectionsLayer.MarkDirtyRepaint();
            RefreshInspector();
            return;
        }

        if (_dirtyLayout)
        {
            AutoLayout();
            _dirtyLayout = false;
        }

        // create graph nodes
        var nodeLookup = new Dictionary<BTNode, GraphNode>();
        TraverseTree(_asset.rootNode, null, nodeLookup);

        // create UI elements
        foreach (var gn in _graphNodes)
        {
            var el = CreateNodeElement(gn);
            gn.element = el;
            nodeContainer.Add(el);
        }

        // 恢复选中状态
        if (!string.IsNullOrEmpty(selectedGuid))
        {
            var reselect = _graphNodes.FirstOrDefault(g => g.node.guid == selectedGuid);
            if (reselect != null)
                SelectNode(reselect);
        }

        _connectionsLayer.MarkDirtyRepaint();
    }

    private void TraverseTree(BTNode node, GraphNode parent, Dictionary<BTNode, GraphNode> lookup)
    {
        if (node == null) return;

        var gn = new GraphNode { node = node, parent = parent };
        _graphNodes.Add(gn);
        lookup[node] = gn;

        if (node is BTComposite comp)
        {
            foreach (var child in comp.children)
                TraverseTree(child, gn, lookup);
        }
        else if (node is BTDecorator dec && dec.child != null)
        {
            TraverseTree(dec.child, gn, lookup);
        }
    }

    private VisualElement CreateNodeElement(GraphNode gn)
    {
        var el = new VisualElement();
        el.style.position = Position.Absolute;
        el.style.left = gn.x;
        el.style.top = gn.y;
        el.style.width = NODE_W;
        el.style.height = NODE_H;
        el.style.paddingLeft = 6;
        el.style.paddingRight = 6;
        el.style.paddingTop = 4;
        el.style.paddingBottom = 4;
        el.style.borderTopLeftRadius = 6;
        el.style.borderTopRightRadius = 6;
        el.style.borderBottomLeftRadius = 6;
        el.style.borderBottomRightRadius = 6;
        el.style.borderLeftWidth = 3;
        el.style.borderRightWidth = 1;
        el.style.borderTopWidth = 1;
        el.style.borderBottomWidth = 1;
        el.style.flexDirection = FlexDirection.Column;
        el.style.justifyContent = Justify.Center;

        int depth = GetDepth(gn);
        Color bg = DepthColors[depth % DepthColors.Length];
        el.style.backgroundColor = bg;
        el.style.borderLeftColor = GetNodeAccent(gn.node);
        el.style.borderRightColor = new Color(0.2f, 0.2f, 0.2f);
        el.style.borderTopColor = new Color(0.2f, 0.2f, 0.2f);
        el.style.borderBottomColor = new Color(0.2f, 0.2f, 0.2f);

        var typeLabel = new Label(GetNodeDisplayName(gn.node));
        typeLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        typeLabel.style.fontSize = 11;
        typeLabel.style.color = Color.white;
        typeLabel.style.overflow = Overflow.Hidden;
        el.Add(typeLabel);

        var detailLabel = new Label(GetNodeDetail(gn.node));
        detailLabel.style.fontSize = 9;
        detailLabel.style.color = new Color(0.7f, 0.7f, 0.7f);
        detailLabel.style.overflow = Overflow.Hidden;
        el.Add(detailLabel);

        el.RegisterCallback<MouseDownEvent>(e =>
        {
            if (e.button == 0)
            {
                SelectNode(gn);
                e.StopPropagation();
            }
        });

        el.RegisterCallback<ContextClickEvent>(e =>
        {
            SelectNode(gn);
            ShowContextMenu(gn, e.mousePosition);
            e.StopPropagation();
        });

        var dragManipulator = new NodeDragManipulator(gn, this);
        el.AddManipulator(dragManipulator);

        return el;
    }

    private static int GetDepth(GraphNode gn)
    {
        int d = 0;
        var p = gn.parent;
        while (p != null) { d++; p = p.parent; }
        return d;
    }

    private static string GetNodeDisplayName(BTNode node) => node switch
    {
        BTRepeater r => r.repeatCount == -1 ? "循环(∞)" : $"循环(x{r.repeatCount})",
        BTInverter => "取反(NOT)",
        BTSucceeder => "总是成功",
        BTCooldown c => $"冷却({c.cooldownTime:F1}s)",
        BTWait w => $"等待({w.duration:F1}s)",
        BTCondition c => $"条件({c.type})",
        BTA_PlaySkill s => s.skillAsset != null ? $"技能({s.skillAsset.name})" : "技能(空)",
        BTA_MoveTo m => $"移动({m.mode})",
        BTA_SetBlackboard sb => $"设黑板({sb.key})",
        BTA_SetAnimationState sa => $"设动画({sa.targetState})",
        BTA_WaitForCondition => "等待条件",
        BTSubTree st => st.subTreeAsset != null ? $"子树({st.subTreeAsset.name})" : "子树(空)",
        BTSequence => "Sequence →",
        BTSelector => "Selector ?",
        BTRandomSelector => "Random ?",
        BTPrioritySelector => "Priority !!",
        _ => node.GetType().Name
    };

    private static string GetNodeDetail(BTNode node) => node switch
    {
        BTA_PlaySkill s => (s.skillAsset != null && s.skillAsset.animationClip != null)
            ? s.skillAsset.animationClip.name : "",
        BTRepeater r => r.repeatCount == -1 ? "无限重复子节点" : $"重复{r.repeatCount}次",
        BTCondition c => c.Describe(),
        BTRandomSelector rs => rs.weights.Count > 0
            ? $"随机(权重:{string.Join(",", rs.weights.Take(3))})" : "随机(等权重)",
        BTCooldown cd => $"CD={cd.cooldownTime}s",
        BTInverter => "NOT",
        BTSucceeder => "强制成功",
        BTA_MoveTo mt => mt.mode.ToString(),
        BTA_SetBlackboard sb => $"bool={sb.boolValue}",
        BTA_WaitForCondition wfc => $"模式:{wfc.condition} 超时:{wfc.timeout}s",
        BTSubTree st => st.subTreeAsset != null ? $"→{st.subTreeAsset.name}" : "无引用",
        BTPrioritySelector ps => $"优先级重评估({ps.children.Count}子)",
        _ => ""
    };

    private static Color GetNodeAccent(BTNode node) => node switch
    {
        BTSequence => new Color(0.3f, 0.7f, 0.3f),
        BTSelector => new Color(0.3f, 0.3f, 0.7f),
        BTRandomSelector => new Color(0.7f, 0.5f, 0.3f),
        BTPrioritySelector => new Color(0.6f, 0.2f, 0.6f),
        BTRepeater => new Color(0.7f, 0.7f, 0.2f),
        BTInverter => new Color(0.7f, 0.3f, 0.3f),
        BTSucceeder => new Color(0.3f, 0.7f, 0.3f),
        BTCooldown => new Color(0.3f, 0.5f, 0.7f),
        BTWait => new Color(0.4f, 0.6f, 0.6f),
        BTSubTree => new Color(0.7f, 0.4f, 0.7f),
        BTA_PlaySkill => new Color(0.9f, 0.3f, 0.3f),
        BTA_MoveTo => new Color(0.3f, 0.6f, 0.9f),
        BTA_SetBlackboard => new Color(0.6f, 0.5f, 0.2f),
        BTA_SetAnimationState => new Color(0.5f, 0.3f, 0.8f),
        BTA_WaitForCondition => new Color(0.5f, 0.6f, 0.3f),
        BTCondition => new Color(0.8f, 0.6f, 0.1f),
        BTDecorator => new Color(0.7f, 0.7f, 0.3f),
        BTAction => new Color(0.3f, 0.7f, 0.7f),
        _ => new Color(0.5f, 0.5f, 0.5f)
    };

    // ===== 自动布局 (左到右) =====

    private void AutoLayout()
    {
        if (_asset == null || _asset.rootNode == null) return;

        // compute subtree sizes
        var subtreeSizes = new Dictionary<BTNode, int>();
        ComputeSubtreeSize(_asset.rootNode, subtreeSizes);

        // position nodes
        var yOffsets = new Dictionary<int, float>();
        LayoutNode(_asset.rootNode, 0, 60f, yOffsets, subtreeSizes);

        void ComputeSubtreeSize(BTNode node, Dictionary<BTNode, int> sizes)
        {
            if (node == null) return;
            if (node is BTComposite comp)
            {
                int total = 0;
                foreach (var c in comp.children) { ComputeSubtreeSize(c, sizes); total += sizes[c]; }
                sizes[node] = Mathf.Max(total, 1);
            }
            else if (node is BTDecorator dec && dec.child != null)
            {
                ComputeSubtreeSize(dec.child, sizes);
                sizes[node] = sizes[dec.child];
            }
            else
            {
                sizes[node] = 1;
            }
        }

        float LayoutNode(BTNode node, int depth, float startY, Dictionary<int, float> yOff, Dictionary<BTNode, int> sizes)
        {
            if (node == null) return startY;
            int size = sizes.GetValueOrDefault(node, 1);

            float totalHeight = size * (NODE_H + ROW_GAP) - ROW_GAP;
            float centerY = startY + totalHeight / 2f;

            if (!yOff.ContainsKey(depth)) yOff[depth] = 0;
            float x = 40f + depth * COL_GAP;

            node.editorPosition = new Vector2(x, centerY - NODE_H / 2f);

            if (node is BTComposite comp)
            {
                float y = startY;
                foreach (var c in comp.children)
                    y = LayoutNode(c, depth + 1, y, yOff, sizes);
                return y;
            }
            else if (node is BTDecorator dec && dec.child != null)
            {
                return LayoutNode(dec.child, depth + 1, startY, yOff, sizes);
            }
            return startY + totalHeight;
        }
    }

    // ===== 连线绘制 =====

    private void DrawConnections(MeshGenerationContext ctx)
    {
        var painter = ctx.painter2D;
        painter.strokeColor = new Color(0.35f, 0.55f, 0.35f, 0.6f);
        painter.lineWidth = 1.5f;

        foreach (var gn in _graphNodes)
        {
            if (gn.parent == null) continue;
            float x1 = gn.parent.x + NODE_W;
            float y1 = gn.parent.y + NODE_H / 2f;
            float x2 = gn.x;
            float y2 = gn.y + NODE_H / 2f;

            painter.BeginPath();
            painter.MoveTo(new Vector2(x1, y1));
            float cp = (x2 - x1) * 0.5f;
            painter.BezierCurveTo(
                new Vector2(x1 + cp, y1),
                new Vector2(x2 - cp, y2),
                new Vector2(x2, y2));
            painter.Stroke();
        }
    }

    // ===== 交互 =====

    private void SelectNode(GraphNode gn)
    {
        _selectedNode = gn;
        // visual highlight
        foreach (var g in _graphNodes)
        {
            if (g.element == null) continue;
            g.element.style.borderRightWidth = g == gn ? 3f : 1f;
            g.element.style.borderBottomWidth = g == gn ? 3f : 1f;
            g.element.style.borderTopWidth = g == gn ? 3f : 1f;
            g.element.style.borderRightColor = g == gn ? Color.white : new Color(0.2f, 0.2f, 0.2f);
            g.element.style.borderBottomColor = g == gn ? Color.white : new Color(0.2f, 0.2f, 0.2f);
            g.element.style.borderTopColor = g == gn ? Color.white : new Color(0.2f, 0.2f, 0.2f);
        }
        RefreshInspector();
    }

    private void ShowContextMenu(GraphNode gn, Vector2 screenPos)
    {
        var menu = new GenericMenu();
        if (gn.node is BTComposite)
        {
            menu.AddItem(new GUIContent("添加子节点/组合/Sequence"), false, () => AddChildNode<BTSequence>(gn));
            menu.AddItem(new GUIContent("添加子节点/组合/Selector"), false, () => AddChildNode<BTSelector>(gn));
            menu.AddItem(new GUIContent("添加子节点/组合/RandomSelector"), false, () => AddChildNode<BTRandomSelector>(gn));
            menu.AddItem(new GUIContent("添加子节点/组合/优先级Selector"), false, () => AddChildNode<BTPrioritySelector>(gn));
            menu.AddSeparator("添加子节点/");
            menu.AddItem(new GUIContent("添加子节点/动作/技能"), false, () => AddChildNode<BTA_PlaySkill>(gn));
            menu.AddItem(new GUIContent("添加子节点/动作/移动"), false, () => AddChildNode<BTA_MoveTo>(gn));
            menu.AddItem(new GUIContent("添加子节点/动作/设黑板"), false, () => AddChildNode<BTA_SetBlackboard>(gn));
            menu.AddItem(new GUIContent("添加子节点/动作/设动画"), false, () => AddChildNode<BTA_SetAnimationState>(gn));
            menu.AddItem(new GUIContent("添加子节点/动作/等待条件"), false, () => AddChildNode<BTA_WaitForCondition>(gn));
            menu.AddSeparator("添加子节点/");
            menu.AddItem(new GUIContent("添加子节点/装饰/Repeater"), false, () => AddChildNode<BTRepeater>(gn));
            menu.AddItem(new GUIContent("添加子节点/装饰/Inverter"), false, () => AddChildNode<BTInverter>(gn));
            menu.AddItem(new GUIContent("添加子节点/装饰/Succeeder"), false, () => AddChildNode<BTSucceeder>(gn));
            menu.AddItem(new GUIContent("添加子节点/装饰/Cooldown"), false, () => AddChildNode<BTCooldown>(gn));
            menu.AddItem(new GUIContent("添加子节点/装饰/等待"), false, () => AddChildNode<BTWait>(gn));
            menu.AddItem(new GUIContent("添加子节点/条件"), false, () => AddChildNode<BTCondition>(gn));
            menu.AddItem(new GUIContent("添加子节点/子树"), false, () => AddChildNode<BTSubTree>(gn));
        }
        else if (gn.node is BTDecorator)
        {
            menu.AddItem(new GUIContent("设置子节点/Sequence"), false, () => AddChildNode<BTSequence>(gn));
            menu.AddItem(new GUIContent("设置子节点/Selector"), false, () => AddChildNode<BTSelector>(gn));
            menu.AddSeparator("设置子节点/");
            menu.AddItem(new GUIContent("设置子节点/技能"), false, () => AddChildNode<BTA_PlaySkill>(gn));
            menu.AddItem(new GUIContent("设置子节点/移动"), false, () => AddChildNode<BTA_MoveTo>(gn));
            menu.AddItem(new GUIContent("设置子节点/等待"), false, () => AddChildNode<BTWait>(gn));
            menu.AddItem(new GUIContent("设置子节点/条件"), false, () => AddChildNode<BTCondition>(gn));
            menu.AddItem(new GUIContent("设置子节点/子树"), false, () => AddChildNode<BTSubTree>(gn));
        }
        menu.AddSeparator("");
        if (gn.parent != null)
            menu.AddItem(new GUIContent("删除节点"), false, DeleteSelectedNode);
        else
            menu.AddDisabledItem(new GUIContent("删除节点(根节点)"));
        menu.ShowAsContext();
    }

    private void AddChildNode<T>(GraphNode parentGn = null) where T : BTNode, new()
    {
        var target = parentGn ?? _selectedNode;
        if (_asset == null) return;

        var newNode = new T();
        newNode.guid = System.Guid.NewGuid().ToString();

        // 计算新节点的位置（在目标节点右侧，垂直偏移避免重叠）
        if (target != null)
        {
            newNode.editorPosition = target.node.editorPosition + new Vector2(COL_GAP, 0f);
        }
        else
        {
            newNode.editorPosition = new Vector2(40f, 100f);
        }

        if (target != null)
        {
            if (target.node is BTComposite comp)
                comp.children.Add(newNode);
            else if (target.node is BTDecorator dec)
            {
                // 覆盖已有子节点时给出提示
                if (dec.child != null)
                    Debug.LogWarning($"覆盖了 {dec.GetType().Name} 的现有子节点");
                dec.child = newNode;
            }
            else return;
        }
        else
        {
            _asset.rootNode = newNode;
        }

        EditorUtility.SetDirty(_asset);
        RefreshCanvas();
    }

    private void DeleteSelectedNode()
    {
        if (_selectedNode == null || _selectedNode.parent == null || _asset == null) return;

        var child = _selectedNode.node;
        var parent = _selectedNode.parent.node;
        if (parent is BTComposite comp)
            comp.children.Remove(child);
        else if (parent is BTDecorator dec)
            dec.child = null;

        EditorUtility.SetDirty(_asset);
        RefreshCanvas();
    }

    public void OnNodeDragged(GraphNode gn, float dx, float dy)
    {
        gn.node.editorPosition += new Vector2(dx, dy);
        if (gn.element != null) { gn.element.style.left = gn.node.editorPosition.x; gn.element.style.top = gn.node.editorPosition.y; }
        _connectionsLayer.MarkDirtyRepaint();
        if (_asset != null) EditorUtility.SetDirty(_asset);
    }

    // ===== 属性面板 =====

    private void RefreshInspector()
    {
        _inspector.Clear();
        if (_selectedNode == null)
        {
            _inspector.Add(new Label("选择一个节点查看属性") { style = { color = Color.gray } });
            return;
        }

        var node = _selectedNode.node;
        _inspector.Add(HeaderLabel(node.GetType().Name));

        if (node is BTA_PlaySkill ps)
        {
            var f = new ObjectField("技能资产") { objectType = typeof(SkillTimelineAsset), value = ps.skillAsset, allowSceneObjects = false };
            f.RegisterValueChangedCallback(e => { ps.skillAsset = e.newValue as SkillTimelineAsset; MarkDirty(); });
            _inspector.Add(f);

            if (ps.skillAsset != null)
            {
                var btn = new Button(() => SkillEditorTimelineWindow.OpenSkill(ps.skillAsset, _previewTarget)) { text = "在编辑器打开" };
                _inspector.Add(btn);
            }
        }
        else if (node is BTA_MoveTo mt)
        {
            var ef = new EnumField("移动模式", mt.mode);
            ef.RegisterValueChangedCallback(e => { mt.mode = (BTA_MoveTo.MoveMode)e.newValue; MarkDirty(); });
            _inspector.Add(ef);
            var rf = new FloatField("半径") { value = mt.radius };
            rf.RegisterValueChangedCallback(e => { mt.radius = e.newValue; MarkDirty(); });
            _inspector.Add(rf);
        }
        else if (node is BTWait w)
        {
            var f = new FloatField("等待秒数") { value = w.duration };
            f.RegisterValueChangedCallback(e => { w.duration = e.newValue; MarkDirty(); });
            _inspector.Add(f);
        }
        else if (node is BTCondition cond)
        {
            var ef = new EnumField("条件类型", cond.type);
            ef.RegisterValueChangedCallback(e => { cond.type = (BTCondition.ConditionType)e.newValue; MarkDirty(); });
            _inspector.Add(ef);
            var df = new FloatField("距离") { value = cond.distanceValue };
            df.RegisterValueChangedCallback(e => { cond.distanceValue = e.newValue; MarkDirty(); });
            _inspector.Add(df);
        }
        else if (node is BTRepeater rep)
        {
            var f = new IntegerField("重复次数(-1=无限)") { value = rep.repeatCount };
            f.RegisterValueChangedCallback(e => { rep.repeatCount = e.newValue; MarkDirty(); });
            _inspector.Add(f);
        }
        else if (node is BTCooldown cd)
        {
            var f = new FloatField("冷却秒数") { value = cd.cooldownTime };
            f.RegisterValueChangedCallback(e => { cd.cooldownTime = e.newValue; MarkDirty(); });
            _inspector.Add(f);
        }
        else if (node is BTA_SetBlackboard sb)
        {
            var kf = new TextField("黑板键") { value = sb.key ?? "" };
            kf.RegisterValueChangedCallback(e => { sb.key = e.newValue; MarkDirty(); });
            _inspector.Add(kf);
            var bf = new Toggle("布尔值") { value = sb.boolValue };
            bf.RegisterValueChangedCallback(e => { sb.boolValue = e.newValue; MarkDirty(); });
            _inspector.Add(bf);
        }
        else if (node is BTA_SetAnimationState sa)
        {
            var ef = new EnumField("目标状态", sa.targetState);
            ef.RegisterValueChangedCallback(e => { sa.targetState = (EnemyAnimationState)e.newValue; MarkDirty(); });
            _inspector.Add(ef);
        }
        else if (node is BTA_WaitForCondition wfc)
        {
            var ef = new EnumField("等待条件", wfc.condition);
            ef.RegisterValueChangedCallback(e => { wfc.condition = (BTA_WaitForCondition.WaitCondition)e.newValue; MarkDirty(); });
            _inspector.Add(ef);
            var kf = new TextField("黑板键") { value = wfc.blackboardKey ?? "" };
            kf.RegisterValueChangedCallback(e => { wfc.blackboardKey = e.newValue; MarkDirty(); });
            _inspector.Add(kf);
            var tf = new FloatField("超时(秒)") { value = wfc.timeout };
            tf.RegisterValueChangedCallback(e => { wfc.timeout = e.newValue; MarkDirty(); });
            _inspector.Add(tf);
        }
        else if (node is BTSubTree st)
        {
            var f = new ObjectField("子树资产") { objectType = typeof(BTreeAsset), value = st.subTreeAsset, allowSceneObjects = false };
            f.RegisterValueChangedCallback(e => { st.subTreeAsset = e.newValue as BTreeAsset; MarkDirty(); });
            _inspector.Add(f);
        }
        else if (node is BTRandomSelector rs)
        {
            _inspector.Add(new Label($"子节点数: {rs.children.Count}") { style = { fontSize = 11 } });
            _inspector.Add(new Label($"权重: {(rs.weights.Count > 0 ? string.Join(", ", rs.weights) : "等权重")}") { style = { fontSize = 10, color = new Color(0.6f, 0.6f, 0.6f) } });
        }
        else if (node is BTInverter)
        {
            _inspector.Add(new Label("反转子节点结果: Success→Failure, Failure→Success") { style = { color = new Color(0.6f, 0.6f, 0.6f), fontSize = 11 } });
        }
        else if (node is BTSucceeder)
        {
            _inspector.Add(new Label("无论子节点返回什么，总是返回 Success") { style = { color = new Color(0.6f, 0.6f, 0.6f), fontSize = 11 } });
        }

        // 通用
        if (!string.IsNullOrEmpty(node.guid) && node.guid.Length >= 8)
            _inspector.Add(new Label($"GUID: {node.guid.Substring(0, 8)}...") { style = { fontSize = 9, color = new Color(0.4f, 0.4f, 0.4f), marginTop = 16 } });

        if (_previewTarget != null)
        {
            var previewBtn = new Button(() =>
            {
                var chain = CollectSkillChainBackup(node);
                if (chain.Count > 0)
                    SkillEditorTimelineWindow.PlaySkillChain(chain, _previewTarget);
            }) { text = "预览此节点及后续技能", style = { marginTop = 4 } };
            _inspector.Add(previewBtn);
        }
    }

    private void MarkDirty()
    {
        if (_asset != null) EditorUtility.SetDirty(_asset);

        // 局部队列：只更新当前选中节点的标签，不重建整个画布
        if (_selectedNode?.element != null)
        {
            var el = _selectedNode.element;
            // 更新节点显示名（第一个 label）
            if (el.childCount >= 1 && el.ElementAt(0) is Label typeLabel)
                typeLabel.text = GetNodeDisplayName(_selectedNode.node);
            // 更新节点详情（第二个 label）
            if (el.childCount >= 2 && el.ElementAt(1) is Label detailLabel)
                detailLabel.text = GetNodeDetail(_selectedNode.node);
        }
    }

    private static List<SkillTimelineAsset> CollectSkillChainBackup(BTNode node)
    {
        var chain = new List<SkillTimelineAsset>();
        var visited = new HashSet<BTNode>();
        Traverse(node, visited, chain);
        return chain;

        static void Traverse(BTNode n, HashSet<BTNode> vis, List<SkillTimelineAsset> c)
        {
            if (n == null || !vis.Add(n)) return;
            if (n is BTA_PlaySkill ps && ps.skillAsset != null)
                c.Add(ps.skillAsset);
            if (n is BTComposite comp)
                foreach (var ch in comp.children) Traverse(ch, vis, c);
            else if (n is BTDecorator dec) Traverse(dec.child, vis, c);
        }
    }

    private static Label HeaderLabel(string text)
    {
        return new Label(text) { style = { unityFontStyleAndWeight = FontStyle.Bold, fontSize = 13, marginBottom = 6, color = new Color(0.8f, 0.8f, 1f) } };
    }

    // ===== 内部类 =====

    public class GraphNode
    {
        public BTNode node;
        public GraphNode parent;
        public VisualElement element;
        public float x => node.editorPosition.x;
        public float y => node.editorPosition.y;
    }

    private class NodeDragManipulator : PointerManipulator
    {
        private readonly GraphNode _gn;
        private readonly BehaviorTreeGraphWindow _w;
        private bool _isPointerDown;
        private bool _isDragging;
        private Vector2 _pointerDownPos;
        private Vector2 _lastDragPos;
        private const float DRAG_THRESHOLD = 5f;

        public NodeDragManipulator(GraphNode gn, BehaviorTreeGraphWindow w) { _gn = gn; _w = w; }

        protected override void RegisterCallbacksOnTarget()
        {
            target.RegisterCallback<PointerDownEvent>(OnDown);
            target.RegisterCallback<PointerMoveEvent>(OnMove);
            target.RegisterCallback<PointerUpEvent>(OnUp);
            target.RegisterCallback<PointerCaptureOutEvent>(OnCaptureOut);
        }

        protected override void UnregisterCallbacksFromTarget()
        {
            target.UnregisterCallback<PointerDownEvent>(OnDown);
            target.UnregisterCallback<PointerMoveEvent>(OnMove);
            target.UnregisterCallback<PointerUpEvent>(OnUp);
            target.UnregisterCallback<PointerCaptureOutEvent>(OnCaptureOut);
        }

        private void OnDown(PointerDownEvent e)
        {
            if (e.button != 0) return;
            _isPointerDown = true;
            _isDragging = false;
            _pointerDownPos = e.position;
            _lastDragPos = e.position;
            e.StopPropagation();
        }

        private void OnMove(PointerMoveEvent e)
        {
            if (!_isPointerDown) return;

            // 拖拽阈值：超过 5px 才开始拖拽，短距离点击不阻止 ScrollView 滚动
            if (!_isDragging)
            {
                if (Vector2.Distance(e.position, _pointerDownPos) < DRAG_THRESHOLD)
                    return;
                _isDragging = true;
                target.CapturePointer(e.pointerId);
            }

            if (!target.HasPointerCapture(e.pointerId)) return;
            float dx = e.position.x - _lastDragPos.x;
            float dy = e.position.y - _lastDragPos.y;
            _w.OnNodeDragged(_gn, dx, dy);
            _lastDragPos = e.position;
            e.StopPropagation();
        }

        private void OnUp(PointerUpEvent e)
        {
            _isPointerDown = false;
            if (_isDragging && target.HasPointerCapture(e.pointerId))
            {
                target.ReleasePointer(e.pointerId);
            }
            _isDragging = false;
            e.StopPropagation();
        }

        private void OnCaptureOut(PointerCaptureOutEvent e)
        {
            _isPointerDown = false;
            _isDragging = false;
        }
    }
}
