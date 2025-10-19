using System;
using UnityEngine;
using System.Linq;
using UnityEngine.InputSystem;

public class ComboEvent : TimelineEventBase, ITimelineEventRuntime
{
    public InputActionReference inputAction;
    public SkillTimelineAsset nextSkill;
    public ComboTriggerType triggerType = ComboTriggerType.Normal;

    private Action<InputAction.CallbackContext> cachedHandler;
    private bool isListening = false;
    private PlayerAttackComponent pac;

    public override TimelineEventType Type => TimelineEventType.Combo;

    public override string GetSummary()
    {
        string inputName = inputAction ? inputAction.name : "None";
        string skillName = nextSkill ? nextSkill.name : "None";
        return $"Combo [{triggerType}] {inputName} → {skillName}";
    }

    public void OnStart(GameObject owner)
    {
        if (inputAction == null || inputAction.action == null || nextSkill == null || isListening)
            return;

        pac = owner.GetComponent<PlayerAttackComponent>();
        if (pac == null) return;

        if (!inputAction.action.enabled)
            inputAction.action.Enable();

        cachedHandler = ctx =>
        {
            if (triggerType == ComboTriggerType.Normal)
            {
                // Normal: 按下时先缓存（以便玩家提前按下也能衔接）
                pac.CacheInputAction(inputAction);
                Debug.Log($"[ComboEvent] Normal: cached input -> {inputAction.action.name}");

                // 如果玩家在窗口内按下，我们也希望立刻响应（不必等 Update 的 Start 匹配那一帧）
                // 这里直接消费并播放（保证响应更快）
                if (pac.ConsumeCachedInputIfMatch(inputAction))
                {
                    pac.PlaySkill(nextSkill);
                    Debug.Log($"[ComboEvent] Normal: immediate play nextSkill -> {nextSkill.name}");
                }
            }
            else // Strict
            {
                // Strict: 必须在窗口内按下才触发（窗口由 StartFrame - EndFrame 定义）
                int maxFrame = pac.CurrentSkillMaxFrame();
                // 使用 TimelineEventBase 的 StartFrame/EndFrame（以帧为单位）
                int startFrame = StartFrame;
                int endFrame = EndFrame;

                // 如果当前帧仍在窗口内（安全检查）
                if (pac.CurrentFrame >= startFrame && pac.CurrentFrame <= endFrame)
                {
                    // 直接播放下一段（即时触发）
                    pac.PlaySkill(nextSkill);
                    Debug.Log($"[ComboEvent] Strict: immediate play nextSkill -> {nextSkill.name}");
                }
                else
                {
                    Debug.Log($"[ComboEvent] Strict: pressed outside window ({pac.CurrentFrame}), ignored");
                }
            }
        };

        inputAction.action.performed += cachedHandler;
        isListening = true;

        Debug.Log($"[ComboEvent] Registered input listener: {inputAction.action.name} ({triggerType})");
    }

    public void OnEnd(GameObject owner)
    {
        if (!isListening || inputAction == null || cachedHandler == null) return;

        inputAction.action.performed -= cachedHandler;
        isListening = false;
        cachedHandler = null;

        Debug.Log($"[ComboEvent] Unregistered input listener: {inputAction.action?.name}");
    }

}

public enum ComboTriggerType
{
    Normal, // 可缓存（快A）
    Strict  // 需窗口实时输入（慢A）
}
