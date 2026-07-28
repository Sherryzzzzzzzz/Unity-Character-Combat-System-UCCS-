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
        var hitBoxField = new TextField("碰撞盒名称")
        {
            value = atk.hitBoxName
        };

        hitBoxField.RegisterValueChangedCallback(e =>
        {
            atk.hitBoxName = e.newValue;
        });

        container.Add(hitBoxField);

        // ==========================
        // Target System 模式
        // ==========================
        var targetSystemToggle = new Toggle("使用 Target System")
        {
            value = atk.useTargetSystem,
            tooltip = "启用后使用 SearchParameters/TargetData 路径替代直接 Physics 调用"
        };
        targetSystemToggle.RegisterValueChangedCallback(e => atk.useTargetSystem = e.newValue);
        container.Add(targetSystemToggle);

        // ==========================
        // AttackData 选择
        // ==========================
        var attackDataField = new ObjectField("攻击数据")
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
                var effectLabel = new Label("游戏效果");
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

        // ── 攻击检测偏移 ──
        var offsetHeader = new Label("攻击检测偏移");
        offsetHeader.style.unityFontStyleAndWeight = FontStyle.Bold;
        offsetHeader.style.marginTop = 6;
        offsetHeader.style.marginBottom = 2;
        container.Add(offsetHeader);

        var useOffsetToggle = new Toggle("启用偏移")
        {
            value = atk.useLocalOffset,
            tooltip = "开启后攻击检测球心 = 角色位置 + 旋转后的偏移量"
        };
        useOffsetToggle.RegisterValueChangedCallback(e => atk.useLocalOffset = e.newValue);
        container.Add(useOffsetToggle);

        var originField = new Vector3Field("检测偏移量")
        {
            value = atk.localOffset,
            tooltip = "攻击检测球心/胶囊体中心的局部空间偏移（相对于角色朝向）"
        };
        originField.RegisterValueChangedCallback(e => atk.localOffset = e.newValue);
        container.Add(originField);

        if (atk.attackData != null)
        {
            var hitPosField = new Vector3Field("击中位置偏移")
            {
                value = atk.attackData.hitPosition,
                tooltip = "受击者被击中后的击退起点偏移"
            };
            hitPosField.RegisterValueChangedCallback(e =>
            {
                atk.attackData.hitPosition = e.newValue;
                EditorUtility.SetDirty(atk.attackData);
            });
            container.Add(hitPosField);
        }

        return container;
    }
}
