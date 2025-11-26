using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

[CustomPropertyDrawer(typeof(BranchCondition))]
public class BranchConditionDrawer : PropertyDrawer
{
    public override VisualElement CreatePropertyGUI(SerializedProperty property)
    {
        // 创建根容器
        var container = new VisualElement();

        // 获取所有需要操作的属性
        var typeProp = property.FindPropertyRelative("type");
        var requiredTagProp = property.FindPropertyRelative("requiredTag");
        var inputActionProp = property.FindPropertyRelative("inputAction");
        
        // 创建对应的 UI 字段
        var typeField = new PropertyField(typeProp, "Condition");
        var requiredTagField = new PropertyField(requiredTagProp, "Tag");
        var inputActionField = new PropertyField(inputActionProp, "Input Action");

        // 添加到容器
        container.Add(typeField);

        // --- 核心：根据类型动态显示/隐藏字段 ---
        
        System.Action<ConditionType> updateVisibility = (currentType) =>
        {
            // 判断当前条件类型是否与输入相关
            bool isInputType = currentType == ConditionType.InputWasPressed || 
                               currentType == ConditionType.InputIsPressed || 
                               currentType == ConditionType.InputWasReleased;
            
            // 判断当前条件类型是否是 Tag 类型
            bool isTagType = currentType == ConditionType.HasInputTag;
            
            // 根据类型设置字段的可见性
            inputActionField.style.display = isInputType ? DisplayStyle.Flex : DisplayStyle.None;
            requiredTagField.style.display = isTagType ? DisplayStyle.Flex : DisplayStyle.None;
        };

        // 注册回调：当 type 字段的值发生变化时，调用 updateVisibility
        typeField.RegisterValueChangeCallback(evt => 
        {
            // 从 SerializedProperty 中获取最新的枚举值
            var newType = (ConditionType)evt.changedProperty.enumValueIndex;
            updateVisibility(newType);
        });
        
        // 在UI创建时，立即调用一次以设置正确的初始状态
        updateVisibility((ConditionType)typeProp.enumValueIndex);

        // 将字段添加到容器的末尾
        container.Add(inputActionField);
        container.Add(requiredTagField);

        return container;
    }

    // 重写这个方法，以确保当使用 ListView 时，我们的自定义UI能正确显示
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.PropertyField(position, property, label, true);
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        return EditorGUI.GetPropertyHeight(property, true);
    }
}