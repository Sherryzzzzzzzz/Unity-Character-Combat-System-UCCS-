// 文件名: BuffEventFactory.cs
using UnityEngine;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

public class BuffEventFactory : ITimelineEventFactory
{
    public TimelineEventType Type => TimelineEventType.Buff;

    public TimelineEventBase Create() => new BuffEvent();

    public TimelineEventBase CreateEvent() => new BuffEvent();

    public VisualElement CreateInspector(TimelineEventBase evt)
    {
        var buffEvt = evt as BuffEvent;
        var root = new VisualElement();
        if (buffEvt == null) return root;

        // --- GAS 模式开关 ---
        var gasToggle = new Toggle("使用 GameplayEffect 模式")
        {
            value = buffEvt.useGameplayEffect
        };
        root.Add(gasToggle);

        // --- 动态内容容器 ---
        var dynamicContainer = new VisualElement();
        dynamicContainer.style.marginTop = 4;
        root.Add(dynamicContainer);

        void RebuildDynamicUI()
        {
            dynamicContainer.Clear();

            if (buffEvt.useGameplayEffect)
            {
                // GAS 模式 — 显示 GameplayEffect 字段
                var effectField = new ObjectField("Gameplay Effect")
                {
                    objectType = typeof(GameplayEffect),
                    allowSceneObjects = false,
                    value = buffEvt.gasEffect
                };
                effectField.RegisterValueChangedCallback(e => buffEvt.gasEffect = e.newValue as GameplayEffect);
                dynamicContainer.Add(effectField);
            }
            else
            {
                // 旧模式 — 显示 BuffSO 字段
                var buffDataField = new ObjectField("Buff Data")
                {
                    objectType = typeof(BuffSO),
                    value = buffEvt.buffData
                };
                buffDataField.RegisterValueChangedCallback(e => buffEvt.buffData = e.newValue as BuffSO);
                dynamicContainer.Add(buffDataField);
            }
        }

        gasToggle.RegisterValueChangedCallback(e =>
        {
            buffEvt.useGameplayEffect = e.newValue;
            RebuildDynamicUI();
        });

        RebuildDynamicUI();

        // --- 目标类型 ---
        var targetField = new EnumField("Target", buffEvt.target);
        targetField.RegisterValueChangedCallback(e => buffEvt.target = (BuffEvent.TargetType)e.newValue);
        root.Add(targetField);

        // --- 操作类型 ---
        var actionField = new EnumField("Action", buffEvt.action);
        actionField.RegisterValueChangedCallback(e => buffEvt.action = (BuffEvent.ActionType)e.newValue);
        root.Add(actionField);

        return root;
    }

    public void Execute(TimelineEventBase evt, GameObject previewTarget)
    {
    }
}
