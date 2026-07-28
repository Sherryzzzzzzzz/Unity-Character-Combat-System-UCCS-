using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

public class CharacterCombatConfigWindow : EditorWindow
{
    private PlayerModel _player;
    private EnemyModel _enemy;
    private PlayerController _playerController;
    private SerializedObject _playerSo;
    private SerializedObject _enemySo;
    private SerializedObject _controllerSo;
    private VisualElement _root;
    private VisualElement _contentPanel;
    private VisualElement _playerPanel;
    private VisualElement _enemyPanel;
    private VisualElement _playerChainPanel;
    private VisualElement _enemyTreePanel;
    private int _activeTab;

    [MenuItem("Tools/角色战斗配置")]
    public static void Open()
    {
        GetWindow<CharacterCombatConfigWindow>("角色战斗配置");
    }

    private void OnEnable()
    {
        Build();
    }

    private void Build()
    {
        rootVisualElement.Clear();
        _root = rootVisualElement;
        _root.style.paddingLeft = 8;
        _root.style.paddingRight = 8;
        _root.style.paddingTop = 8;
        _root.style.paddingBottom = 8;

        var title = new Label("角色战斗配置");
        title.style.unityFontStyleAndWeight = FontStyle.Bold;
        title.style.fontSize = 16;
        _root.Add(title);

        var selector = new VisualElement { style = { flexDirection = FlexDirection.Row, marginTop = 8, marginBottom = 8 } };
        _root.Add(selector);

        var playerField = new ObjectField("玩家") { objectType = typeof(PlayerModel), allowSceneObjects = true, value = _player };
        playerField.style.flexGrow = 1;
        playerField.RegisterValueChangedCallback(evt =>
        {
            _player = evt.newValue as PlayerModel;
            _playerController = _player != null ? _player.GetComponentInParent<PlayerController>() ?? FindObjectOfType<PlayerController>() : null;
            RefreshSerializedObjects();
            RebuildPanels();
        });
        selector.Add(playerField);

        var enemyField = new ObjectField("敌人") { objectType = typeof(EnemyModel), allowSceneObjects = true, value = _enemy };
        enemyField.style.flexGrow = 1;
        enemyField.style.marginLeft = 8;
        enemyField.RegisterValueChangedCallback(evt =>
        {
            _enemy = evt.newValue as EnemyModel;
            RefreshSerializedObjects();
            RebuildPanels();
        });
        selector.Add(enemyField);

        var autoButton = new Button(AutoFindTargets) { text = "自动查找场景对象" };
        selector.Add(autoButton);

        var saveButton = new Button(SaveAll) { text = "保存" };
        saveButton.style.marginLeft = 4;
        selector.Add(saveButton);

        var tabs = new Toolbar();
        _root.Add(tabs);
        tabs.Add(MakeTabButton("玩家配置", 0));
        tabs.Add(MakeTabButton("敌人配置", 1));

        _contentPanel = new VisualElement { style = { flexGrow = 1, marginTop = 8 } };
        _root.Add(_contentPanel);

        _playerPanel = MakeColumn("玩家配置");
        _enemyPanel = MakeColumn("敌人配置");
        _playerChainPanel = MakeColumn("玩家连招预览");
        _enemyTreePanel = MakeColumn("敌人行为树");

        AutoFindTargets();
    }

    private ToolbarButton MakeTabButton(string text, int tabIndex)
    {
        return new ToolbarButton(() =>
        {
            _activeTab = tabIndex;
            ShowActiveTab();
        }) { text = text };
    }

    private void ShowActiveTab()
    {
        if (_contentPanel == null) return;
        _contentPanel.Clear();
        switch (_activeTab)
        {
            case 0:
                _contentPanel.Add(_playerPanel);
                break;
            default:
                _contentPanel.Add(_enemyPanel);
                break;
        }
    }

    private VisualElement MakeColumn(string title)
    {
        var column = new ScrollView();
        column.style.flexGrow = 1;
        column.style.marginRight = 6;
        column.style.paddingLeft = 6;
        column.style.paddingRight = 6;
        column.style.paddingTop = 6;
        column.style.borderLeftWidth = 1;
        column.style.borderRightWidth = 1;
        column.style.borderTopWidth = 1;
        column.style.borderBottomWidth = 1;
        column.style.borderLeftColor = new Color(0.25f, 0.25f, 0.25f);
        column.style.borderRightColor = new Color(0.25f, 0.25f, 0.25f);
        column.style.borderTopColor = new Color(0.25f, 0.25f, 0.25f);
        column.style.borderBottomColor = new Color(0.25f, 0.25f, 0.25f);

        var label = new Label(title);
        label.style.unityFontStyleAndWeight = FontStyle.Bold;
        label.style.marginBottom = 6;
        column.Add(label);
        return column;
    }

    private void AutoFindTargets()
    {
        if (_player == null) _player = FindObjectOfType<PlayerModel>();
        if (_enemy == null) _enemy = FindObjectOfType<EnemyModel>();
        _playerController = _player != null ? _player.GetComponentInParent<PlayerController>() ?? FindObjectOfType<PlayerController>() : FindObjectOfType<PlayerController>();
        RefreshSerializedObjects();
        RebuildPanels();
    }

    private void RefreshSerializedObjects()
    {
        _playerSo = _player != null ? new SerializedObject(_player) : null;
        _enemySo = _enemy != null ? new SerializedObject(_enemy) : null;
        _controllerSo = _playerController != null ? new SerializedObject(_playerController) : null;
    }

    private void SaveAll()
    {
        _playerSo?.ApplyModifiedProperties();
        _enemySo?.ApplyModifiedProperties();
        _controllerSo?.ApplyModifiedProperties();
        if (_player != null) EditorUtility.SetDirty(_player);
        if (_enemy != null) EditorUtility.SetDirty(_enemy);
        if (_playerController != null) EditorUtility.SetDirty(_playerController);
        if (_player != null || _enemy != null || _playerController != null)
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        RebuildPanels();
    }

    private void RebuildPanels()
    {
        BuildPlayerPanel();
        BuildEnemyPanel();
        ShowActiveTab();
    }

    private void BuildPlayerPanel()
    {
        _playerPanel.Clear();
        _playerPanel.Add(Header("玩家配置"));
        if (_playerSo == null)
        {
            _playerPanel.Add(new HelpBox("请选择场景中的 PlayerModel。", HelpBoxMessageType.Info));
            return;
        }

        _playerSo.Update();
        AddProperty(_playerPanel, _playerSo, "AnimationSet", "移动动画配置");
        AddProperty(_playerPanel, _playerSo, "walkSpeed", "行走速度");
        AddProperty(_playerPanel, _playerSo, "runSpeed", "奔跑速度");
        AddProperty(_playerPanel, _playerSo, "gravity", "重力");
        AddProperty(_playerPanel, _playerSo, "jumpHeight", "跳跃高度");

        _playerPanel.Add(Section("玩家技能槽"));
        AddProperty(_playerPanel, _playerSo, "lightStart", "轻攻击起手");
        AddProperty(_playerPanel, _playerSo, "heavyStart", "重攻击起手");
        AddProperty(_playerPanel, _playerSo, "combatArtStart", "战技起手");
        AddProperty(_playerPanel, _playerSo, "defendStart", "防御起手");
        AddProperty(_playerPanel, _playerSo, "lightSkyStart", "空中轻攻击起手");
        AddProperty(_playerPanel, _playerSo, "dodgeF", "前翻滚");
        AddProperty(_playerPanel, _playerSo, "dodgeB", "后翻滚");
        AddProperty(_playerPanel, _playerSo, "dodgeL", "左翻滚");
        AddProperty(_playerPanel, _playerSo, "dodgeR", "右翻滚");

        _playerPanel.Add(Section("输入标签"));
        AddProperty(_playerPanel, _playerSo, "LightAttackInputTag", "轻攻击标签");
        AddProperty(_playerPanel, _playerSo, "HeavyAttackInputTag", "重攻击标签");
        AddProperty(_playerPanel, _playerSo, "CombatArtInputTag", "战技标签");
        AddProperty(_playerPanel, _playerSo, "DefendInputTag", "防御标签");

        if (_controllerSo != null)
        {
            _controllerSo.Update();
            _playerPanel.Add(Section("按键绑定引用"));
            AddProperty(_playerPanel, _controllerSo, "attackActionRef", "攻击 / 长按重攻击");
            AddProperty(_playerPanel, _controllerSo, "dodgeRunActionRef", "翻滚 / 长按奔跑");
            AddProperty(_playerPanel, _controllerSo, "combatArtActionRef", "战技键");
        }

        BuildPlayerChainPanel();
        _playerPanel.Add(_playerChainPanel);
    }

    private void BuildEnemyPanel()
    {
        _enemyPanel.Clear();
        _enemyPanel.Add(Header("敌人配置"));
        if (_enemySo == null)
        {
            _enemyPanel.Add(new HelpBox("请选择场景中的 EnemyModel。", HelpBoxMessageType.Info));
            return;
        }

        _enemySo.Update();
        AddProperty(_enemyPanel, _enemySo, "AnimationSet", "敌人动画配置");
        AddProperty(_enemyPanel, _enemySo, "speed", "移动速度");
        AddProperty(_enemyPanel, _enemySo, "rotateSpeed", "转向速度");

        var skillComponent = _enemy.GetComponent<EnemySkillComponent>();
        if (skillComponent != null)
        {
            var enemySkillSo = new SerializedObject(skillComponent);
            enemySkillSo.Update();
            _enemyPanel.Add(Section("敌人技能组件"));
            AddProperty(_enemyPanel, enemySkillSo, "clashStunTag", "拼刀硬直标签");
        }

        _enemyPanel.Add(new HelpBox("敌人的具体出招通常配置在行为树的 PlaySkill 节点里。下面会可视化当前敌人的 BTreeRunner.treeAsset，并列出所有播放技能节点。", HelpBoxMessageType.Info));

        BuildEnemyTreePanel();
        _enemyPanel.Add(_enemyTreePanel);
    }

    private void BuildPlayerChainPanel()
    {
        _playerChainPanel.Clear();
        _playerChainPanel.Add(Header("连招预览"));

        var roots = new List<SkillTimelineAsset>();
        AddIfNotNull(roots, _player != null ? _player.lightStart : null);
        AddIfNotNull(roots, _player != null ? _player.heavyStart : null);
        AddIfNotNull(roots, _player != null ? _player.combatArtStart : null);
        AddIfNotNull(roots, _player != null ? _player.defendStart : null);
        AddIfNotNull(roots, _player != null ? _player.lightSkyStart : null);

        if (roots.Count == 0)
        {
            _playerChainPanel.Add(new HelpBox("请先给玩家技能槽分配技能资产，才能可视化连招链。", HelpBoxMessageType.Info));
            return;
        }

        foreach (var root in roots)
        {
            DrawSkillNode(root, new HashSet<SkillTimelineAsset>(), 0);
        }
    }

    private void DrawSkillNode(SkillTimelineAsset skill, HashSet<SkillTimelineAsset> visited, int depth)
    {
        if (skill == null) return;

        var row = new VisualElement { style = { marginLeft = depth * 16, marginTop = 4, paddingLeft = 4, paddingTop = 4, paddingBottom = 4 } };
        row.style.backgroundColor = depth == 0 ? new Color(0.16f, 0.18f, 0.22f) : new Color(0.12f, 0.12f, 0.12f);
        row.style.borderLeftWidth = 3;
        row.style.borderLeftColor = new Color(0.5f, 0.7f, 1f);

        var skillName = skill.name;
        var clipName = skill.animationClip != null ? skill.animationClip.name : "未配置动画片段";
        row.Add(new Label($"{skillName}（动画：{clipName}）"));

        var btnRow = new VisualElement { style = { flexDirection = FlexDirection.Row, marginTop = 2, marginBottom = 2 } };
        var editBtn = new Button(() => SkillEditorTimelineWindow.OpenSkill(skill)) { text = "打开编辑器" };
        editBtn.style.marginRight = 4;
        btnRow.Add(editBtn);

        var previewGo = _player != null ? _player.gameObject : null;
        if (previewGo != null)
        {
            var previewBtn = new Button(() =>
            {
                var chain = CollectSkillChain(skill);
                if (chain.Count > 0)
                    SkillEditorTimelineWindow.PlaySkillChain(chain, previewGo);
            }) { text = "在玩家上预览连招链" };
            btnRow.Add(previewBtn);
        }
        row.Add(btnRow);

        _playerChainPanel.Add(row);

        if (!visited.Add(skill))
        {
            row.Add(new HelpBox("检测到循环连招，这里停止继续展开。", HelpBoxMessageType.Warning));
            return;
        }

        foreach (var combo in GetComboEvents(skill))
        {
            var comboRow = new VisualElement { style = { marginLeft = depth * 16 + 16, marginTop = 2, paddingLeft = 4 } };
            var tagName = combo.RequiredTag != null ? combo.RequiredTag.name : "未配置输入标签";
            var nextName = combo.nextSkill != null ? combo.nextSkill.name : "未配置下一个技能";
            var modeName = combo.comboMode == ComboEvent.ComboMode.Normal_Cacheable ? "可缓存" : "严格窗口";
            comboRow.Add(new Label($"第 {combo.StartFrame}-{combo.EndFrame} 帧：{tagName} → {nextName}（{modeName}）"));
            _playerChainPanel.Add(comboRow);

            if (combo.nextSkill != null)
                DrawSkillNode(combo.nextSkill, visited, depth + 1);
        }
    }

    private void BuildEnemyTreePanel()
    {
        _enemyTreePanel.Clear();
        _enemyTreePanel.Add(Header("敌人行为树"));

        if (_enemy == null)
        {
            _enemyTreePanel.Add(new HelpBox("请先选择场景中的 EnemyModel。", HelpBoxMessageType.Info));
            return;
        }

        var runner = _enemy.GetComponent<BTreeRunner>();
        if (runner == null || runner.treeAsset == null)
        {
            _enemyTreePanel.Add(new HelpBox("未找到 BTreeRunner 组件或未分配 treeAsset。请在敌人身上挂载 BTreeRunner 并关联行为树资产。", HelpBoxMessageType.Info));
            return;
        }

        _enemyTreePanel.Add(new Label($"行为树资产：{runner.treeAsset.name}"));
        _enemyTreePanel.Add(new Label($"Tick 间隔：{runner.tickInterval}s | 启动时自动运行：{runner.runOnStart}"));

        var btnRow = new VisualElement { style = { flexDirection = FlexDirection.Row, marginTop = 4, marginBottom = 6 } };
        var openGraphBtn = new Button(() => BehaviorTreeGraphWindow.OpenWithAsset(runner.treeAsset, _enemy.gameObject, runner)) { text = "在图形编辑器打开" };
        openGraphBtn.style.marginRight = 4;
        btnRow.Add(openGraphBtn);
        _enemyTreePanel.Add(btnRow);

        _enemyTreePanel.Add(Section("行为树节点结构"));

        DrawBTNode(runner.treeAsset.rootNode, 0, runner);
    }

    private void DrawBTNode(BTNode node, int depth, BTreeRunner runner)
    {
        if (node == null) return;

        var row = new VisualElement { style = { marginLeft = depth * 16, marginTop = 3, paddingLeft = 4, paddingTop = 3, paddingBottom = 3 } };
        row.style.borderLeftWidth = 2;
        row.style.borderLeftColor = new Color(0.3f, 0.6f, 0.3f);

        var typeName = node.GetType().Name;
        var nameLabel = new Label($"{typeName}");
        nameLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        row.Add(nameLabel);

        // 显示额外信息
        if (node is BTRepeater rep)
            row.Add(new Label($"  重复次数：{rep.repeatCount}"));
        if (node is BTCondition cond)
            row.Add(new Label($"  条件类型：{cond.type}"));
        if (node is BTWait wait)
            row.Add(new Label($"  等待时长：{wait.duration}s"));
        if (node is BTA_PlaySkill playSkill)
        {
            var skillName = playSkill.skillAsset != null ? playSkill.skillAsset.name : "未赋值";
            var clipName = (playSkill.skillAsset != null && playSkill.skillAsset.animationClip != null)
                ? playSkill.skillAsset.animationClip.name : "无动画";
            row.Add(new Label($"  技能：{skillName}（{clipName}）"));

            if (playSkill.skillAsset != null)
            {
                var btnRow = new VisualElement { style = { flexDirection = FlexDirection.Row, marginTop = 2 } };
                var editBtn = new Button(() => SkillEditorTimelineWindow.OpenSkill(playSkill.skillAsset)) { text = "打开编辑器" };
                editBtn.style.marginRight = 4;
                btnRow.Add(editBtn);
                if (_enemy != null)
                {
                    var previewBtn = new Button(() =>
                    {
                        var chain = CollectSkillChain(playSkill.skillAsset);
                        if (chain.Count > 0)
                            SkillEditorTimelineWindow.PlaySkillChain(chain, _enemy.gameObject);
                        else
                            SkillEditorTimelineWindow.OpenSkill(playSkill.skillAsset, _enemy.gameObject);
                    }) { text = "在敌人上预览连招链" };
                    btnRow.Add(previewBtn);
                }
                row.Add(btnRow);
            }
        }

        _enemyTreePanel.Add(row);

        // 递归子节点
        if (node is BTComposite composite)
        {
            foreach (var child in composite.children)
                DrawBTNode(child, depth + 1, runner);
        }
        else if (node is BTDecorator decorator)
        {
            DrawBTNode(decorator.child, depth + 1, runner);
        }
    }

    private static IEnumerable<ComboEvent> GetComboEvents(SkillTimelineAsset skill)
    {
        if (skill == null || skill.tracks == null) yield break;
        foreach (var track in skill.tracks)
        {
            if (track == null || track.events == null) continue;
            foreach (var evt in track.events)
            {
                if (evt is ComboEvent combo)
                    yield return combo;
            }
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

            // 取第一个有 nextSkill 的 ComboEvent 作为下一段
            SkillTimelineAsset next = null;
            if (current.tracks != null)
            {
                foreach (var track in current.tracks)
                {
                    if (track == null || track.events == null) continue;
                    foreach (var evt in track.events)
                    {
                        if (evt is ComboEvent combo && combo.nextSkill != null)
                        {
                            next = combo.nextSkill;
                            break;
                        }
                    }
                    if (next != null) break;
                }
            }
            current = next;
        }
        return chain;
    }

    private static void AddIfNotNull(List<SkillTimelineAsset> list, SkillTimelineAsset skill)
    {
        if (skill != null && !list.Contains(skill)) list.Add(skill);
    }

    private static Label Header(string text)
    {
        var label = new Label(text);
        label.style.unityFontStyleAndWeight = FontStyle.Bold;
        label.style.fontSize = 14;
        label.style.marginBottom = 6;
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

    private static void AddProperty(VisualElement parent, SerializedObject so, string propertyName, string label)
    {
        var property = so.FindProperty(propertyName);
        if (property == null)
        {
            parent.Add(new HelpBox($"找不到属性：{propertyName}", HelpBoxMessageType.Warning));
            return;
        }

        var field = new PropertyField(property, label);
        field.Bind(so);
        parent.Add(field);
    }
}
