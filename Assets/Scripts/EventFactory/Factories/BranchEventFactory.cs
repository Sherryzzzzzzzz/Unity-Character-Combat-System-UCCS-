// 文件名: BranchEventFactory.cs (已修复所有错误)
using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
using System.Collections.Generic;

public class BranchEventFactory : ITimelineEventFactory
{
    public TimelineEventType Type => TimelineEventType.Branch;

    public TimelineEventBase Create() => new BranchEvent();
    
    public TimelineEventBase CreateEvent() => Create(); 

    public VisualElement CreateInspector(TimelineEventBase evt)
    {
        var branchEvt = evt as BranchEvent;
        var root = new VisualElement();
        if (branchEvt == null) return root;

        // --- 1. 条件列表 ---
        var conditionsLabel = new Label("Conditions (All must be met)") { style = { unityFontStyleAndWeight = FontStyle.Bold } };
        root.Add(conditionsLabel);

        var conditionsContainer = new VisualElement();
        root.Add(conditionsContainer);
        
        System.Action redrawConditions = null;
        
        redrawConditions = () => 
        {
            conditionsContainer.Clear();
            
            for (int i = 0; i < branchEvt.conditions.Count; i++)
            {
                int currentIndex = i; 
                var condition = branchEvt.conditions[currentIndex];
                
                var row = new VisualElement() { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, marginTop = 5 }};

                var typeField = new EnumField(condition.type) { style = { width = 150 } };
                typeField.RegisterValueChangedCallback(e => 
                {
                    condition.type = (ConditionType)e.newValue;
                    redrawConditions?.Invoke();
                });

                var tagField = new ObjectField() { objectType = typeof(GameplayTagSO), value = condition.requiredTag, style = { flexGrow = 1 } };
                tagField.RegisterValueChangedCallback(e => condition.requiredTag = e.newValue as GameplayTagSO);

                var inputActionField = new ObjectField() { objectType = typeof(UnityEngine.InputSystem.InputActionReference), value = condition.inputAction, style = { flexGrow = 1 } };
                inputActionField.RegisterValueChangedCallback(e => condition.inputAction = e.newValue as UnityEngine.InputSystem.InputActionReference);
                
                var deleteButton = new Button(() => {
                    branchEvt.conditions.RemoveAt(currentIndex);
                    redrawConditions?.Invoke();
                }) { text = "X", style = { width = 20, flexShrink = 0 } };
                
                row.Add(typeField);
                
                if (condition.type == ConditionType.HasInputTag)
                {
                    row.Add(tagField);
                }
                else if (condition.type == ConditionType.InputIsPressed || condition.type == ConditionType.InputWasPressed || condition.type == ConditionType.InputWasReleased)
                {
                    row.Add(inputActionField);
                }
                
                row.Add(deleteButton);
                conditionsContainer.Add(row);
            }
        };

        var addButton = new Button(() => {
            branchEvt.conditions.Add(new BranchCondition());
            redrawConditions?.Invoke();
        }) { text = "Add Condition" };
        root.Add(addButton);


        // --- 2. 动作配置 ---
        root.Add(new Label(" ") { style = { height = 10 } });
        var actionLabel = new Label("Action") { style = { unityFontStyleAndWeight = FontStyle.Bold } };
        root.Add(actionLabel);

        var actionTypeField = new EnumField("Action Type", branchEvt.action);
        actionTypeField.RegisterValueChangedCallback(e => 
        {
            branchEvt.action = (BranchActionType)e.newValue;
            // 回调逻辑现在移到了下面
        });
        root.Add(actionTypeField);

        var targetFrameField = new IntegerField("Target Frame") { value = branchEvt.targetFrame };
        // *** 核心修复：IntegerField 的回调参数是 ChangeEvent<int> ***
        // *** 正确获取值的方式同样是 e.newValue ***
        targetFrameField.RegisterValueChangedCallback(e => 
        {
            branchEvt.targetFrame = e.newValue; // <-- 使用 e.newValue 而不是 e.value
        });
        root.Add(targetFrameField);
        
        // 动态显示 Target Frame 字段的逻辑
        System.Action<BranchActionType> toggleTargetFrameVisibility = (action) => {
            targetFrameField.style.display = (action == BranchActionType.JumpToFrame || action == BranchActionType.JumpToEvent) 
                ? DisplayStyle.Flex : DisplayStyle.None;
        };
        // 确保当 actionTypeField 改变时，也触发可见性更新
        actionTypeField.RegisterValueChangedCallback(e => toggleTargetFrameVisibility((BranchActionType)e.newValue));
        
        // --- 3. 初始化 ---
        redrawConditions?.Invoke(); // 第一次绘制UI
        toggleTargetFrameVisibility(branchEvt.action); // 第一次设置动作UI的可见性

        return root;
    }
    
    public void Execute(TimelineEventBase evt, GameObject previewTarget) { }
}