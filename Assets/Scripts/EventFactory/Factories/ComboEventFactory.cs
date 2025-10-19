using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

public class ComboEventFactory : ITimelineEventFactory
{
    public TimelineEventType Type => TimelineEventType.Combo;

    public TimelineEventBase Create() => new ComboEvent();

    public VisualElement CreateInspector(TimelineEventBase evt)
    {
        var combo = evt as ComboEvent;
        var root = new VisualElement();

        if (combo == null)
        {
            root.Add(new Label("ComboEvent is null"));
            return root;
        }

        // ===== 输入动作 =====
        var inputField = new ObjectField("Input Action")
        {
            objectType = typeof(InputActionReference),
            value = combo.inputAction
        };
        inputField.RegisterValueChangedCallback(e =>
        {
            combo.inputAction = e.newValue as InputActionReference;
        });
        root.Add(inputField);

        // ===== 下一个技能 =====
        var skillField = new ObjectField("Next Skill")
        {
            objectType = typeof(SkillTimelineAsset),
            value = combo.nextSkill
        };
        skillField.RegisterValueChangedCallback(e =>
        {
            combo.nextSkill = e.newValue as SkillTimelineAsset;
        });
        root.Add(skillField);

        // ===== 触发类型（快A / 慢A）=====
        var triggerTypeField = new EnumField("Trigger Type", combo.triggerType);
        triggerTypeField.Init(combo.triggerType); // 初始化枚举值
        triggerTypeField.RegisterValueChangedCallback(e =>
        {
            combo.triggerType = (ComboTriggerType)e.newValue;
        });
        root.Add(triggerTypeField);

        // 美化一下间距
        root.style.paddingTop = 4;
        root.style.paddingBottom = 4;
        root.style.marginBottom = 6;

        return root;
    }

    public void Execute(TimelineEventBase evt, GameObject previewTarget)
    {
        var combo = evt as ComboEvent;
        if (combo == null) return;

        string inputName = combo.inputAction != null ? combo.inputAction.name : "None";
        string skillName = combo.nextSkill != null ? combo.nextSkill.name : "None";
        Debug.Log($"[ComboEventFactory] Execute ComboEvent: {inputName} → {skillName}");
    }
}


