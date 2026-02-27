using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

public class AttackEventFactory : ITimelineEventFactory
{
    public TimelineEventType Type => TimelineEventType.Attack;

    public TimelineEventBase Create()
    {
        return new AttackEvent();
    }

    public TimelineEventBase CreateEvent() => new AttackEvent();

    public VisualElement CreateInspector(TimelineEventBase evt)
    {
        var atk = evt as AttackEvent;
        var container = new VisualElement();

        // ==========================
        // HitBox 名称
        // ==========================
        var hitBoxField = new TextField("HitBox Name")
        {
            value = atk.hitBoxName
        };

        hitBoxField.RegisterValueChangedCallback(e =>
        {
            atk.hitBoxName = e.newValue;
        });

        container.Add(hitBoxField);

        // ==========================
        // AttackData 选择
        // ==========================
        var attackDataField = new ObjectField("Attack Data")
        {
            objectType = typeof(AttackData),
            allowSceneObjects = false,
            value = atk.attackData
        };

        container.Add(attackDataField);

        var attackDataContainer = new VisualElement();
        attackDataContainer.style.marginTop = 8;
        container.Add(attackDataContainer);

        void DrawAttackDataInspector()
        {
            attackDataContainer.Clear();

            if (atk.attackData == null)
                return;

            var so = new SerializedObject(atk.attackData);
            var iterator = so.GetIterator();

            iterator.NextVisible(true);

            while (iterator.NextVisible(false))
            {
                if (iterator.name == "m_Script")
                    continue;

                var propField = new PropertyField(iterator.Copy());
                propField.Bind(so);
                attackDataContainer.Add(propField);
            }

            // ==========================
            // 展开 GameplayEffect
            // ==========================

            if (atk.attackData.effect != null)
            {
                var effectLabel = new Label("Gameplay Effect");
                effectLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
                effectLabel.style.marginTop = 6;
                attackDataContainer.Add(effectLabel);

                var effectSO = new SerializedObject(atk.attackData.effect);
                var effectIterator = effectSO.GetIterator();

                effectIterator.NextVisible(true);

                while (effectIterator.NextVisible(false))
                {
                    if (effectIterator.name == "m_Script")
                        continue;

                    var propField = new PropertyField(effectIterator.Copy());
                    propField.Bind(effectSO);
                    attackDataContainer.Add(propField);
                }
            }
        }

        attackDataField.RegisterValueChangedCallback(e =>
        {
            atk.attackData = e.newValue as AttackData;
            DrawAttackDataInspector();
        });

        DrawAttackDataInspector();
        
        var originField = new Vector3Field("Preview Origin")
        {
            value = atk.localOffset
        };

        originField.RegisterValueChangedCallback(e =>
        {
            atk.localOffset = e.newValue;
        });

        container.Add(originField);

        var toggle = new Toggle("Use Preview Origin")
        {
            value = atk.useLocalOffset
        };

        toggle.RegisterValueChangedCallback(e =>
        {
            atk.useLocalOffset = e.newValue;
        });

        container.Add(toggle);

        // ==========================
        // 帧区间
        // ==========================

        var frameRow = new VisualElement();
        frameRow.style.flexDirection = FlexDirection.Row;

        var startFrameField = new IntegerField("Start Frame")
        {
            value = atk.StartFrame
        };

        startFrameField.style.flexGrow = 1;

        startFrameField.RegisterValueChangedCallback(e =>
        {
            atk.StartFrame = Mathf.Max(0, e.newValue);
            if (atk.StartFrame > atk.EndFrame)
                atk.EndFrame = atk.StartFrame;
        });

        var endFrameField = new IntegerField("End Frame")
        {
            value = atk.EndFrame
        };

        endFrameField.style.flexGrow = 1;

        endFrameField.RegisterValueChangedCallback(e =>
        {
            atk.EndFrame = Mathf.Max(0, e.newValue);
            if (atk.EndFrame < atk.StartFrame)
                atk.StartFrame = atk.EndFrame;
        });

        frameRow.Add(startFrameField);
        frameRow.Add(endFrameField);

        container.Add(frameRow);

        return container;
    }
}
