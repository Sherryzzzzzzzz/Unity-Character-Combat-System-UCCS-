using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

public class GameplayEffectEventFactory : ITimelineEventFactory
{
    public TimelineEventType Type => TimelineEventType.GASEffect;

    public TimelineEventBase Create() => new GameplayEffectEvent();
    public TimelineEventBase CreateEvent() => new GameplayEffectEvent();

    public VisualElement CreateInspector(TimelineEventBase evt)
    {
        var gasEvt = evt as GameplayEffectEvent;
        var root = new VisualElement();
        if (gasEvt == null) return root;

        // GameplayEffect 引用
        var effectField = new ObjectField("Gameplay Effect")
        {
            objectType = typeof(GameplayEffect),
            allowSceneObjects = false,
            value = gasEvt.gameplayEffect
        };
        effectField.RegisterValueChangedCallback(e => gasEvt.gameplayEffect = e.newValue as GameplayEffect);
        root.Add(effectField);

        // EffectTarget 枚举
        var targetField = new EnumField("Effect Target", gasEvt.effectTarget);
        root.Add(targetField);

        // SearchParameters 容器（仅 AllInRange 时显示）
        var searchContainer = new VisualElement();
        searchContainer.style.marginLeft = 8;
        searchContainer.style.marginTop = 4;
        root.Add(searchContainer);

        void RebuildSearchParams()
        {
            searchContainer.Clear();
            if (gasEvt.effectTarget != EffectTargetType.AllInRange)
            {
                searchContainer.style.display = DisplayStyle.None;
                return;
            }
            searchContainer.style.display = DisplayStyle.Flex;

            var header = new Label("搜索参数");
            header.style.unityFontStyleAndWeight = FontStyle.Bold;
            searchContainer.Add(header);

            BuildSearchParametersUI(searchContainer, gasEvt.searchParameters);
        }

        targetField.RegisterValueChangedCallback(e =>
        {
            gasEvt.effectTarget = (EffectTargetType)e.newValue;
            RebuildSearchParams();
        });

        RebuildSearchParams();

        return root;
    }

    public static void BuildSearchParametersUI(VisualElement container, SearchParameters sp)
    {
        var shapeField = new EnumField("Shape", sp.Shape);
        container.Add(shapeField);

        var paramsContainer = new VisualElement();
        container.Add(paramsContainer);

        void RebuildShapeParams()
        {
            paramsContainer.Clear();
            switch (sp.Shape)
            {
                case SearchShape.Circle:
                    AddFloatField(paramsContainer, "Radius", sp.Radius, v => sp.Radius = v);
                    break;
                case SearchShape.Sector:
                    AddFloatField(paramsContainer, "Radius", sp.Radius, v => sp.Radius = v);
                    AddFloatField(paramsContainer, "Angle", sp.Angle, v => sp.Angle = v);
                    break;
                case SearchShape.Line:
                    AddFloatField(paramsContainer, "Length", sp.Length, v => sp.Length = v);
                    break;
                case SearchShape.Rectangle:
                    AddFloatField(paramsContainer, "Length", sp.Length, v => sp.Length = v);
                    AddFloatField(paramsContainer, "Width", sp.Width, v => sp.Width = v);
                    break;
            }

            AddIntField(paramsContainer, "Max Targets (0=无限)", sp.MaxTargets, v => sp.MaxTargets = v);

            var excludeSelfToggle = new Toggle("Exclude Self") { value = sp.ExcludeSelf };
            excludeSelfToggle.RegisterValueChangedCallback(e => sp.ExcludeSelf = e.newValue);
            paramsContainer.Add(excludeSelfToggle);
        }

        shapeField.RegisterValueChangedCallback(e =>
        {
            sp.Shape = (SearchShape)e.newValue;
            RebuildShapeParams();
        });

        RebuildShapeParams();
    }

    private static void AddFloatField(VisualElement container, string label, float value, System.Action<float> onChange)
    {
        var field = new FloatField(label) { value = value };
        field.RegisterValueChangedCallback(e => onChange(e.newValue));
        container.Add(field);
    }

    private static void AddIntField(VisualElement container, string label, int value, System.Action<int> onChange)
    {
        var field = new IntegerField(label) { value = value };
        field.RegisterValueChangedCallback(e => onChange(e.newValue));
        container.Add(field);
    }
}
