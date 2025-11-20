// HitBoxEventFactory.cs
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;
using UnityEditor.UIElements;
using System.Collections.Generic;
using System.Linq;

public class HitBoxEventFactory : ITimelineEventFactory
{
    public TimelineEventType Type => TimelineEventType.HitBox;

    public TimelineEventBase Create() => new HitBoxEvent();
    
    public TimelineEventBase CreateEvent() => new HitBoxEvent(); 

    public VisualElement CreateInspector(TimelineEventBase evt)
    {
        var hb = evt as HitBoxEvent;
        var root = new VisualElement();
        if (hb == null) return root;

        root.style.paddingTop = 4;
        
        // --- 1. 创建 Target Manager 字段 (保持不变) ---
        var managerField = new ObjectField("Target Manager")
        {
            objectType = typeof(HurtBoxManager),
            allowSceneObjects = true,
            value = hb.targetManager,
            tooltip = "拖入场景中带有 HurtBoxManager 的角色。如果留空，将默认使用技能的持有者。"
        };
        
        // --- 2. 核心修正：适配 PopupField 的正确构造函数 ---
        
        // a. 首先，我们需要一个初始的选项列表。
        //    如果 targetManager 为空，我们就获取 GameBodyPart 枚举的所有值作为初始列表。
        List<GameBodyPart> initialChoices = hb.targetManager != null ?
            hb.targetManager.bodyPartMappings.Select(m => m.part).ToList() :
            System.Enum.GetValues(typeof(GameBodyPart)).Cast<GameBodyPart>().ToList();

        // b. 使用正确的构造函数来创建 PopupField。
        //    PopupField<T>(List<T> choices, T defaultValue)
        var bodyPartField = new PopupField<GameBodyPart>("Body Part", initialChoices, hb.bodyPart);

        // --- 核心联动逻辑 (保持不变) ---
        System.Action<HurtBoxManager> updateBodyPartChoices = (manager) =>
        {
            if (manager != null)
            {
                // 从 manager 的映射列表中获取所有已定义的身体部位
                List<GameBodyPart> availableParts = manager.bodyPartMappings.Select(mapping => mapping.part).ToList();
                
                // 更新下拉菜单的选项
                bodyPartField.choices = availableParts;
                
                // 确保当前选中的值是合法的
                if (!availableParts.Contains(bodyPartField.value))
                {
                    // 如果当前值不合法，选择列表中的第一个作为默认值
                    bodyPartField.SetValueWithoutNotify(availableParts.FirstOrDefault());
                    hb.bodyPart = availableParts.FirstOrDefault(); // 同时更新数据模型
                }
            }
            else
            {
                // 如果没有 manager，显示所有可能的 GameBodyPart 选项
                bodyPartField.choices = System.Enum.GetValues(typeof(GameBodyPart)).Cast<GameBodyPart>().ToList();
            }
        };

        // --- 注册回调 (保持不变) ---
        managerField.RegisterValueChangedCallback(e =>
        {
            hb.targetManager = e.newValue as HurtBoxManager;
            updateBodyPartChoices(hb.targetManager);
        });
        
        bodyPartField.RegisterValueChangedCallback(e =>
        {
            hb.bodyPart = e.newValue;
        });

        // --- 初始化UI (保持不变) ---
        // (在创建时已经用 initialChoices 初始化过了，这里可以省略，但为了保险起见保留)
        updateBodyPartChoices(hb.targetManager);
        
        root.Add(managerField);
        root.Add(bodyPartField);
        
        // --- 其他字段 (保持不变) ---
        var actionField = new EnumField("Action", hb.action);
        actionField.RegisterValueChangedCallback(e => hb.action = (HitBoxEvent.ActionType)e.newValue);
        root.Add(actionField);

        var invincibleField = new Toggle("Is Invincible") { value = hb.isInvincible };
        invincibleField.RegisterValueChangedCallback(e => hb.isInvincible = e.newValue);
        root.Add(invincibleField);

        return root;
    }

    public void Execute(TimelineEventBase evt, GameObject previewTarget) { }
}