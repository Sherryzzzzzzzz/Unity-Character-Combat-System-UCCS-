using UnityEngine;

/// <summary>
/// Timeline event that activates a GameplayAbility via ASC at a specific frame.
/// This bridges the skill timeline editor with GAS ability activation,
/// enabling cooldown/cost/tag-requirement enforcement during skill playback.
/// </summary>
[System.Serializable]
public class GameplayAbilityEvent : TimelineEventBase, ITimelineEventRuntime
{
    [Header("GAS Ability")]
    [Tooltip("The GameplayAbilitySO to activate when this frame is reached.")]
    public GameplayAbilitySO abilityRef;

    [Tooltip("Event tag sent to ASC.HandleGameplayEvent(). "
           + "Abilities registered via ShouldAbilityRespondToEvent(tag) will be triggered.")]
    public GameplayTagSO eventTag;

    [Tooltip("If true, the skill will wait for the ability to end before continuing.")]
    public bool waitForEnd;

    public override TimelineEventType Type => TimelineEventType.GameplayAbility;

    public override string GetSummary()
    {
        string abilityName = abilityRef != null ? abilityRef.name : "None";
        string tagName = eventTag != null ? eventTag.name : "(no tag)";
        return $"Ability [{StartFrame}-{EndFrame}] {abilityName} tag={tagName}";
    }

    public void OnStart(GameObject owner)
    {
        if (abilityRef == null && eventTag == null)
        {
            Debug.LogWarning("GameplayAbilityEvent: both abilityRef and eventTag are null — nothing to activate");
            return;
        }

        var asc = owner.GetComponent<AbilitySystemComponent>();
        if (asc == null)
        {
            Debug.LogWarning($"GameplayAbilityEvent: owner '{owner.name}' missing AbilitySystemComponent");
            return;
        }

        // Method 1: Activate specific ability by name via spec system
        if (abilityRef != null)
        {
            int handle = asc.ActivateAbility(abilityRef.abilityName);
            if (handle > 0)
            {
                Debug.Log($"GameplayAbilityEvent: activated '{abilityRef.abilityName}' (handle={handle})");
            }
            else
            {
                Debug.LogWarning($"GameplayAbilityEvent: failed to activate '{abilityRef.abilityName}'");
            }
        }

        // Method 2: Fire event tag so registered abilities can respond
        if (eventTag != null)
        {
            var eventData = new GameplayEventData(eventTag, owner, owner);
            asc.HandleGameplayEvent(eventTag, eventData);
        }
    }

    public void OnEnd(GameObject owner)
    {
        // Cleanup if needed — most abilities handle their own lifecycle
    }

    public override TimelineEventBase Clone()
    {
        return new GameplayAbilityEvent
        {
            StartFrame = StartFrame,
            EndFrame   = EndFrame,
            abilityRef = abilityRef,
            eventTag   = eventTag,
            waitForEnd = waitForEnd
        };
    }
}
