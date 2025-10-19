using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

public class AttackEventFactory : ITimelineEventFactory
{
    public TimelineEventType Type => TimelineEventType.Attack;

    public TimelineEventBase Create()
    {
        // 默认生成一个持续 1 帧的攻击事件
        return new AttackEvent
        {
            hitBoxName = "HitBox",
            damage = 10f,
            StartFrame = 0,
            EndFrame = 1
        };
    }

    public VisualElement CreateInspector(TimelineEventBase evt)
    {
        var atk = evt as AttackEvent;
        var container = new VisualElement();

        // ==========================
        // HitBox 名称
        // ==========================
        var hitBoxField = new TextField("HitBox Name");
        hitBoxField.value = atk.hitBoxName;
        hitBoxField.RegisterValueChangedCallback(e => atk.hitBoxName = e.newValue);
        container.Add(hitBoxField);

        // ==========================
        // 伤害值
        // ==========================
        var dmgField = new FloatField("Damage");
        dmgField.value = atk.damage;
        dmgField.RegisterValueChangedCallback(e => atk.damage = e.newValue);
        container.Add(dmgField);

        // ==========================
        // 帧区间（Start / End）
        // ==========================
        var frameRow = new VisualElement();
        frameRow.style.flexDirection = FlexDirection.Row;
        frameRow.style.marginTop = 4;
        frameRow.style.marginBottom = 4;

        var startFrameField = new IntegerField("Start Frame");
        startFrameField.value = atk.StartFrame;
        startFrameField.style.flexGrow = 1;
        startFrameField.RegisterValueChangedCallback(e =>
        {
            atk.StartFrame = Mathf.Max(0, e.newValue);
            if (atk.StartFrame > atk.EndFrame)
                atk.EndFrame = atk.StartFrame; // 保证合法区间
        });

        var endFrameField = new IntegerField("End Frame");
        endFrameField.value = atk.EndFrame;
        endFrameField.style.flexGrow = 1;
        endFrameField.RegisterValueChangedCallback(e =>
        {
            atk.EndFrame = Mathf.Max(0, e.newValue);
            if (atk.EndFrame < atk.StartFrame)
                atk.StartFrame = atk.EndFrame; // 自动交换
        });

        frameRow.Add(startFrameField);
        frameRow.Add(endFrameField);
        container.Add(frameRow);

        // ==========================
        // 提示文字
        // ==========================
        var info = new Label($"当前区间: {atk.StartFrame} → {atk.EndFrame}");
        info.style.color = new Color(0.8f, 0.8f, 0.8f);
        container.Add(info);

        // 监听刷新
        void RefreshInfo()
        {
            info.text = $"当前区间: {atk.StartFrame} → {atk.EndFrame}";
        }
        startFrameField.RegisterValueChangedCallback(_ => RefreshInfo());
        endFrameField.RegisterValueChangedCallback(_ => RefreshInfo());

        return container;
    }
}

