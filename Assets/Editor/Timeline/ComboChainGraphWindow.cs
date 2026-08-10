// 连招链图编辑器 — 以节点图可视化技能连招链（ComboEvent → nextSkill）。
// 复用行为树图窗口的 UIElements 自绘节点 + 贝塞尔连线方案。
// Tools → 连招链图编辑器
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

public class ComboChainGraphWindow : EditorWindow
{
    private ScrollView _canvasScroll;
    private VisualElement _canvas;
    private VisualElement _connectionsLayer;
    private VisualElement _inspector;

    private readonly List<SkillNode> _nodes = new();
    private SkillNode _selectedNode;

    private const float NODE_W = 180f;
    private const float NODE_H = 72f;
    private const float COL_GAP = 240f;
    private const float ROW_GAP = 26f;

    private static readonly Color[] DepthColors =
    {
        new(0.22f, 0.30f, 0.42f), // 起手
        new(0.22f, 0.26f, 0.34f),
        new(0.28f, 0.24f, 0.32f),
        new(0.32f, 0.24f, 0.26f),
        new(0.30f, 0.28f, 0.22f),
    };

    [MenuItem("Tools/连招链图编辑器")]
    public static void Open()
    {
        var w = GetWindow<ComboChainGraphWindow>("连招链图编辑器");
        w.minSize = new Vector2(900, 500);
    }

    public static void OpenWithSkill(SkillTimelineAsset root)
    {
        var w = GetWindow<ComboChainGraphWindow>("连招链图编辑器");
        w.minSize = new Vector2(900, 500);
        w.LoadChain(root);
    }

    private void OnEnable() => BuildUI();

    private void BuildUI()
    {
        var root = rootVisualElement;
        root.Clear();
        root.style.flexDirection = FlexDirection.Column;

        // ── 工具栏 ──
        var toolbar = new Toolbar();
        var skillField = new ObjectField("起始技能") { objectType = typeof(SkillTimelineAsset), allowSceneObjects = false };
        skillField.style.minWidth = 260;
        skillField.style.flexGrow = 1;
        skillField.RegisterValueChangedCallback(evt => LoadChain(evt.newValue as SkillTimelineAsset));
        toolbar.Add(skillField);

        var autoBtn = new ToolbarButton(() => LoadPlayerChains()) { text = "加载玩家全部连招" };
        toolbar.Add(autoBtn);
        var fitBtn = new ToolbarButton(() => { if (_canvasScroll != null) _canvasScroll.scrollOffset = Vector2.zero; }) { text = "回到起点" };
        toolbar.Add(fitBtn);
        root.Add(toolbar);

        // ── 画布 ──
        _canvasScroll = new ScrollView();
        _canvasScroll.style.flexGrow = 1;
        root.Add(_canvasScroll);

        _canvas = new VisualElement();
        _canvas.style.position = Position.Relative;
        _canvas.style.width = 4000;
        _canvas.style.height = 2000;
        _canvasScroll.contentContainer.Add(_canvas);

        _connectionsLayer = new VisualElement();
        _connectionsLayer.style.position = Position.Absolute;
        _connectionsLayer.style.top = 0;
        _connectionsLayer.style.left = 0;
        _connectionsLayer.style.width = 4000;
        _connectionsLayer.style.height = 2000;
        _connectionsLayer.generateVisualContent = DrawConnections;
        _connectionsLayer.pickingMode = PickingMode.Ignore;
        _canvas.Add(_connectionsLayer);

        // ── 右侧详情面板 ──
        _inspector = new VisualElement { style = { width = 280, paddingLeft = 6, paddingRight = 6, borderLeftWidth = 1, borderLeftColor = new Color(0.25f, 0.25f, 0.25f) } };
        var split = new VisualElement { style = { flexDirection = FlexDirection.Row, flexGrow = 1, minHeight = 0 } };
        split.Add(_canvasScroll);
        split.Add(_inspector);
        root.Add(split);

        _inspector.Add(new HelpBox("选择左侧节点查看技能详情与连招分支。", HelpBoxMessageType.Info));
    }

    // ================================================================
    // 连招链加载 + 自动布局
    // ================================================================

    private void LoadChain(SkillTimelineAsset root)
    {
        _nodes.Clear();
        _canvas.Clear();
        _canvas.Add(_connectionsLayer); // 连线层保持在最底层

        if (root == null)
        {
            _inspector.Clear();
            _inspector.Add(new HelpBox("请选择起始技能资产。", HelpBoxMessageType.Info));
            return;
        }

        var visited = new HashSet<SkillTimelineAsset>();
        BuildNodes(root, null, 0, visited);
        LayoutNodes();

        foreach (var n in _nodes)
        {
            n.element = CreateNodeElement(n);
            _canvas.Add(n.element);
        }

        _connectionsLayer.MarkDirtyRepaint();
    }

    private void LoadPlayerChains()
    {
        var player = FindFirstObjectByType<PlayerModel>();
        if (player == null)
        {
            LoadChain(null);
            _inspector.Clear();
            _inspector.Add(new HelpBox("场景中未找到 PlayerModel，无法自动加载。", HelpBoxMessageType.Warning));
            return;
        }

        var roots = new List<SkillTimelineAsset>
        {
            player.lightStart, player.heavyStart, player.combatArtStart,
            player.defendStart, player.lightSkyStart
        };

        _nodes.Clear();
        _canvas.Clear();
        _canvas.Add(_connectionsLayer);

        var visited = new HashSet<SkillTimelineAsset>();
        foreach (var r in roots)
            if (r != null)
                BuildNodes(r, null, 0, visited);

        LayoutNodes();
        foreach (var n in _nodes)
        {
            n.element = CreateNodeElement(n);
            _canvas.Add(n.element);
        }
        _connectionsLayer.MarkDirtyRepaint();
    }

    private void BuildNodes(SkillTimelineAsset skill, SkillNode parent, int depth, HashSet<SkillTimelineAsset> visited)
    {
        if (skill == null) return;

        var node = new SkillNode { skill = skill, parent = parent, depth = depth };
        _nodes.Add(node);

        // 防止循环连招无限展开
        if (!visited.Add(skill))
        {
            node.hasLoop = true;
            return;
        }

        foreach (var combo in GetComboEvents(skill))
        {
            if (combo.nextSkill != null)
                BuildNodes(combo.nextSkill, node, depth + 1, visited);
        }
    }

    /// <summary>按深度分层布局：每层一列，层内纵向排列</summary>
    private void LayoutNodes()
    {
        var byDepth = new Dictionary<int, List<SkillNode>>();
        foreach (var n in _nodes)
        {
            if (!byDepth.TryGetValue(n.depth, out var list))
            {
                list = new List<SkillNode>();
                byDepth[n.depth] = list;
            }
            list.Add(n);
        }

        foreach (var kv in byDepth)
        {
            float y = 24f;
            foreach (var n in kv.Value)
            {
                n.x = 24f + kv.Key * COL_GAP;
                n.y = y;
                y += NODE_H + ROW_GAP;
            }
        }
    }

    // ================================================================
    // 节点绘制
    // ================================================================

    private VisualElement CreateNodeElement(SkillNode n)
    {
        var el = new VisualElement();
        el.style.position = Position.Absolute;
        el.style.left = n.x;
        el.style.top = n.y;
        el.style.width = NODE_W;
        el.style.height = NODE_H;
        el.style.borderLeftWidth = 3;
        el.style.borderLeftColor = new Color(0.5f, 0.7f, 1f);

        var bg = new Color(0.14f, 0.16f, 0.2f);
        if (n.depth < DepthColors.Length) bg = DepthColors[n.depth];
        el.style.backgroundColor = bg;

        // 标题
        var title = new Label(n.skill.name);
        title.style.unityFontStyleAndWeight = FontStyle.Bold;
        title.style.unityTextAlign = TextAnchor.MiddleLeft;
        title.style.paddingLeft = 6;
        el.Add(title);

        // 动画名
        var clip = n.skill.animationClip;
        var clipLabel = new Label(clip != null ? clip.name : "未配置动画");
        clipLabel.style.fontSize = 10;
        clipLabel.style.color = new Color(0.75f, 0.75f, 0.75f);
        clipLabel.style.paddingLeft = 6;
        el.Add(clipLabel);

        // 连招分支摘要
        var combos = GetComboEvents(n.skill).ToList();
        int c = 0;
        foreach (var combo in combos)
        {
            if (c >= 2) break; // 最多显示 2 行
            var next = combo.nextSkill != null ? combo.nextSkill.name : "无";
            var tag = combo.RequiredTag != null ? combo.RequiredTag.name : "无标签";
            var branch = new Label($"{combo.StartFrame}-{combo.EndFrame} 帧 {tag} → {next}");
            branch.style.fontSize = 9;
            branch.style.color = new Color(0.6f, 0.85f, 0.6f);
            branch.style.paddingLeft = 6;
            el.Add(branch);
            c++;
        }
        if (combos.Count > 2)
        {
            var more = new Label($"… 还有 {combos.Count - 2} 个分支");
            more.style.fontSize = 9;
            more.style.paddingLeft = 6;
            el.Add(more);
        }
        if (n.hasLoop)
        {
            var loop = new Label("⚠ 循环连招");
            loop.style.fontSize = 9;
            loop.style.color = new Color(1f, 0.7f, 0.3f);
            loop.style.paddingLeft = 6;
            el.Add(loop);
        }

        el.RegisterCallback<ClickEvent>(evt => OnNodeClicked(n, el));
        return el;
    }

    private void OnNodeClicked(SkillNode n, VisualElement el)
    {
        _selectedNode = n;
        foreach (var node in _nodes)
        {
            node.element.style.borderLeftColor = node == n
                ? new Color(1f, 0.9f, 0.3f)
                : new Color(0.5f, 0.7f, 1f);
        }

        _inspector.Clear();
        _inspector.Add(Header(n.skill.name));

        var clip = n.skill.animationClip;
        _inspector.Add(new Label($"动画：{(clip != null ? clip.name : "未配置")}"));
        _inspector.Add(new Label($"是否空中技能：{(n.skill.isAirSkill ? "是" : "否")}"));

        var btnRow = new VisualElement { style = { flexDirection = FlexDirection.Row, marginTop = 6 } };
        var openBtn = new Button(() => SkillEditorTimelineWindow.OpenSkill(n.skill)) { text = "打开时间轴编辑器" };
        openBtn.style.flexGrow = 1;
        btnRow.Add(openBtn);
        _inspector.Add(btnRow);

        var player = FindFirstObjectByType<PlayerModel>();
        if (player != null)
        {
            var previewBtn = new Button(() =>
            {
                var chain = CollectSkillChain(n.skill);
                if (chain.Count > 0)
                    SkillEditorTimelineWindow.PlaySkillChain(chain, player.gameObject);
            }) { text = "在玩家上预览连招链" };
            previewBtn.style.marginTop = 4;
            previewBtn.style.flexGrow = 1;
            _inspector.Add(previewBtn);
        }

        _inspector.Add(Section("连招分支"));
        var combos = GetComboEvents(n.skill).ToList();
        if (combos.Count == 0)
        {
            _inspector.Add(new Label("（无连招分支，技能播完即结束）"));
        }
        foreach (var combo in combos)
        {
            var nextName = combo.nextSkill != null ? combo.nextSkill.name : "无";
            var tagName = combo.RequiredTag != null ? combo.RequiredTag.name : "未配置输入标签";
            var modeName = combo.comboMode == ComboEvent.ComboMode.Normal_Cacheable ? "可缓存" : "严格窗口";
            _inspector.Add(new Label($"窗口 {combo.StartFrame}-{combo.EndFrame} 帧：{tagName} → {nextName}（{modeName}）"));
        }
    }

    private void DrawConnections(MeshGenerationContext ctx)
    {
        var painter = ctx.painter2D;
        painter.strokeColor = new Color(0.45f, 0.75f, 0.5f, 0.7f);
        painter.lineWidth = 1.8f;

        foreach (var gn in _nodes)
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

    // ================================================================
    // 工具
    // ================================================================

    private static IEnumerable<ComboEvent> GetComboEvents(SkillTimelineAsset skill)
    {
        if (skill == null || skill.tracks == null) yield break;
        foreach (var track in skill.tracks)
        {
            if (track == null || track.events == null) continue;
            foreach (var evt in track.events)
                if (evt is ComboEvent combo)
                    yield return combo;
        }
    }

    private static List<SkillTimelineAsset> CollectSkillChain(SkillTimelineAsset root)
    {
        var chain = new List<SkillTimelineAsset>();
        if (root == null) return chain;

        var visited = new HashSet<SkillTimelineAsset>();
        var current = root;
        while (current != null && visited.Add(current))
        {
            chain.Add(current);
            SkillTimelineAsset next = null;
            foreach (var combo in GetComboEvents(current))
            {
                if (combo.nextSkill != null) { next = combo.nextSkill; break; }
            }
            current = next;
        }
        return chain;
    }

    private static Label Header(string text)
    {
        var label = new Label(text);
        label.style.unityFontStyleAndWeight = FontStyle.Bold;
        label.style.fontSize = 14;
        label.style.marginBottom = 6;
        label.style.color = new Color(0.8f, 0.8f, 1f);
        return label;
    }

    private static Label Section(string text)
    {
        var label = new Label(text);
        label.style.unityFontStyleAndWeight = FontStyle.Bold;
        label.style.marginTop = 10;
        label.style.marginBottom = 4;
        return label;
    }

    // ===== 内部类 =====

    public class SkillNode
    {
        public SkillTimelineAsset skill;
        public SkillNode parent;
        public int depth;
        public float x;
        public float y;
        public bool hasLoop;
        public VisualElement element;
    }
}
