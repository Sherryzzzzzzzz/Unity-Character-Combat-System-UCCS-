using System.Collections;
using UnityEngine;

/// <summary>
/// Minimal DodgeAbility component that exposes a perfect-dodge window and an AttemptDodge() method.
///
/// Usage:
/// - Call StartPerfectWindow(duration) when you want to open a timing window (for example, from an enemy attack prediction).
/// - Call AttemptDodge() when the player presses the dodge input. If the dodge happens while the perfect window is active,
///   the component grants the configured perfectDodgeTag for a short duration and optionally applies a short GameplayEffect to the owner.
/// - Remove the tag after the configured tagDuration.
///
/// This component keeps changes minimal and is intended to be wired into your existing input/ability system.
/// </summary>
public class DodgeAbility : MonoBehaviour
{
    [Header("Perfect Dodge Settings")]
    [Tooltip("Default duration of the perfect timing window if StartPerfectWindow is called without a value")]
    public float defaultPerfectWindow = 0.2f;

    [Tooltip("How long the perfect_dodge tag stays on the defender when a perfect dodge succeeds")]
    public float perfectDodgeTagDuration = 0.6f;

    [Tooltip("GameplayTag to add to the defender on a successful perfect dodge (assign state.perfect_dodge)")]
    public GameplayTagSO perfectDodgeTag;

    [Tooltip("Optional GameplayEffect to apply to the defender on perfect dodge (can grant tags/cues). If null, only the tag is added.")]
    public GameplayEffect perfectDodgeSelfEffect;

    private bool _perfectWindowActive = false;
    private Coroutine _windowCoroutine;
    private Coroutine _tagRemovalCoroutine;

    private TagComponent _tagComponent;
    private AbilitySystemComponent _asc;

    private void Awake()
    {
        _tagComponent = GetComponent<TagComponent>();
        _asc = GetComponent<AbilitySystemComponent>();

        if (_tagComponent == null)
            Debug.LogWarning($"{gameObject.name}: DodgeAbility requires a TagComponent to add/remove perfect dodge tag", this);
    }

    /// <summary>
    /// Open the perfect-dodge timing window for the given duration (seconds).
    /// During this window, calls to AttemptDodge() will count as perfect dodges.
    /// </summary>
    public void StartPerfectWindow(float duration)
    {
        if (_windowCoroutine != null)
            StopCoroutine(_windowCoroutine);
        _windowCoroutine = StartCoroutine(PerfectWindowRoutine(duration <= 0f ? defaultPerfectWindow : duration));
    }

    private IEnumerator PerfectWindowRoutine(float duration)
    {
        _perfectWindowActive = true;
        yield return new WaitForSeconds(duration);
        _perfectWindowActive = false;
        _windowCoroutine = null;
    }

    /// <summary>
    /// Call this when the player attempts to dodge (e.g., from input). Returns true if the dodge was perfect.
    /// </summary>
    public bool AttemptDodge()
    {
        if (_perfectWindowActive)
        {
            OnPerfectDodge();
            return true;
        }

        // Normal dodge behavior can be handled by existing systems; this component only handles perfect-dodge detection and tag/apply.
        return false;
    }

    private void OnPerfectDodge()
    {
        Debug.Log($"{gameObject.name}: Perfect dodge succeeded");

        // Add tag immediately for hurtbox checks (HurtBoxManager checks the defender's TagComponent)
        if (_tagComponent != null && perfectDodgeTag != null)
        {
            _tagComponent.AddTag(perfectDodgeTag);

            if (_tagRemovalCoroutine != null)
                StopCoroutine(_tagRemovalCoroutine);
            _tagRemovalCoroutine = StartCoroutine(RemoveTagAfterDelay(perfectDodgeTagDuration));
        }

        // Optionally apply a self-effect (designer choice) to the defender via ASC to trigger cues/animations
        if (perfectDodgeSelfEffect != null && _asc != null)
        {
            int handle = _asc.ApplyGameplayEffect(perfectDodgeSelfEffect, _asc);
            Debug.Log($"{gameObject.name}: Applied perfectDodgeSelfEffect handle={handle}");
        }
    }

    private IEnumerator RemoveTagAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (_tagComponent != null && perfectDodgeTag != null)
        {
            _tagComponent.RemoveTag(perfectDodgeTag);
        }
        _tagRemovalCoroutine = null;
    }

    /// <summary>
    /// Helper: cancel any active perfect window and remove tag immediately.
    /// </summary>
    public void CancelPerfectWindowAndTag()
    {
        if (_windowCoroutine != null)
        {
            StopCoroutine(_windowCoroutine);
            _windowCoroutine = null;
            _perfectWindowActive = false;
        }
        if (_tagRemovalCoroutine != null)
        {
            StopCoroutine(_tagRemovalCoroutine);
            _tagRemovalCoroutine = null;
        }
        if (_tagComponent != null && perfectDodgeTag != null)
        {
            _tagComponent.RemoveTag(perfectDodgeTag);
        }
    }
}
