// 文件名: LoopEventFactory.cs
using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using UnityEditor.UIElements;

public class LoopEventFactory : ITimelineEventFactory
{
    public TimelineEventType Type => TimelineEventType.Loop;
    public TimelineEventBase CreateEvent() => new LoopEvent();

    public VisualElement CreateInspector(TimelineEventBase evt)
    {
        var root = new VisualElement();
        var loopEvt = evt as LoopEvent;
        if (loopEvt == null) return root;

        var proxy = ScriptableObject.CreateInstance<LoopEventEditorProxy>();
        proxy.TargetEvent = loopEvt;
        var serializedObject = new SerializedObject(proxy);
        
        var imguiContainer = new IMGUIContainer(() =>
        {
            serializedObject.Update();
            
            var targetEventProp = serializedObject.FindProperty("TargetEvent");
            var conditionsProp = targetEventProp.FindPropertyRelative("breakConditions");
            
            EditorGUILayout.LabelField("Break Conditions", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("When all conditions below are met, the loop will break and the animation will continue.", MessageType.Info);
            
            // 使用 PropertyField 自动绘制列表，它会自动查找并使用 BranchConditionDrawer
            EditorGUILayout.PropertyField(conditionsProp, true);

            if (GUI.changed)
            {
                serializedObject.ApplyModifiedProperties();
            }
        });
        
        root.Add(imguiContainer);
        
        root.RegisterCallback<DetachFromPanelEvent>(e => { if (proxy != null) Object.DestroyImmediate(proxy); });

        return root;
    }
    
    // 代理 ScriptableObject
    private class LoopEventEditorProxy : ScriptableObject
    {
        public LoopEvent TargetEvent;
    }
    
    // 其他接口方法
    public TimelineEventBase Create() => CreateEvent();
    public void Execute(TimelineEventBase evt, GameObject previewTarget) {}
}