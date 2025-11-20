using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor.UIElements;

public class ComboEventFactory : ITimelineEventFactory
{
    public TimelineEventType Type => TimelineEventType.Combo;

    public TimelineEventBase Create()
    {
        // 返回一个带有默认值的 ComboEvent 实例
        return new ComboEvent();
    }
    
    public TimelineEventBase CreateEvent() => new ComboEvent(); 

    public VisualElement CreateInspector(TimelineEventBase evt)
    {
        var combo = evt as ComboEvent;
        var root = new VisualElement();
        if (combo == null)
        {
            root.Add(new Label("事件数据不是 ComboEvent 类型"));
            return root;
        }

        // --- UI 布局和样式 ---
        root.style.paddingTop = 4;

        // 1. 所需 Tag 字段 (ObjectField)
        var tagField = new ObjectField("所需 Tag")
        {
            objectType = typeof(GameplayTagSO), // 指定类型为我们的 Tag ScriptableObject
            value = combo.RequiredTag,
            tooltip = "触发此连招所必须拥有的 Gameplay Tag 资产。"
        };
        // 注册回调，当策划在 Inspector 中修改字段时，更新事件数据
        tagField.RegisterValueChangedCallback(e =>
        {
            combo.RequiredTag = e.newValue as GameplayTagSO;
        });

        // 2. 连招模式字段 (EnumField)
        var comboModeField = new EnumField("连招模式", combo.comboMode)
        {
            tooltip = "Normal: 允许提前输入 (使用缓存)。\nStrict: 必须在连招窗口期内精确输入。"
        };
        comboModeField.RegisterValueChangedCallback(e =>
        {
            combo.comboMode = (ComboEvent.ComboMode)e.newValue;
        });

        // 3. 下一个技能字段 (ObjectField)
        var skillField = new ObjectField("下一个技能")
        {
            objectType = typeof(SkillTimelineAsset),
            value = combo.nextSkill,
            tooltip = "成功触发连招后要播放的技能资产。"
        };
        skillField.RegisterValueChangedCallback(e =>
        {
            combo.nextSkill = e.newValue as SkillTimelineAsset;
        });
        
        // --- 将所有 UI 元素添加到根容器中 ---
        root.Add(tagField);
        root.Add(comboModeField);
        root.Add(skillField);

        return root;
    }
    
    // Execute 方法在我们的运行时逻辑中不被使用，可以留空
    public void Execute(TimelineEventBase evt, GameObject previewTarget) 
    {
        // 可以在这里加一个调试按钮，但在当前系统下不是必需的
    }
}